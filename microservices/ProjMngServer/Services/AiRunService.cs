using System.Data;
using System.Security.Cryptography;
using System.Text;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>실행기가 쓰는 쪽 — 집어가기 · 하트비트 · 로그 · 완료.</summary>
/// 사람용 AiTaskService 와 달리 run 별 1회용 토큰으로 인증한다.
public sealed class AiRunService(
    IConfiguration configuration, AiTaskNotifier notifier, AiRunSummaryWriter summaries,
    AiRunFileWriter files, AiTaskTitler titler, AiTaskQueue queue,
    IServiceScopeFactory scopeFactory, ILogger<AiRunService> logger)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    /// <summary>임대 기간. 실행기가 이 안에 하트비트를 보내야 한다.</summary>
    private readonly int _leaseSeconds = configuration.GetValue("AiTasks:LeaseSeconds", 60);

    /// <summary>한 실행에 쌓을 수 있는 로그 줄 수.</summary>
    private readonly int _maxEvents = configuration.GetValue("AiTasks:MaxEventsPerRun", 5000);

    /// <summary>한 줄의 길이 상한(글자).</summary>
    private readonly int _maxEventLength = configuration.GetValue("AiTasks:MaxEventLength", 4000);

    /// <summary>재시도 대기 지연(초). 실패 후 바로 재시도하지 않고 일정 시간 대기한다.</summary>
    private readonly int _retryDelaySeconds =
        Math.Max(0, configuration.GetValue("AiTasks:RetryDelaySeconds", 15));

    /// <summary>끝난 뒤 요약과 제목을 기다리는 상한(초). 넘기면 적힌 값으로 앱푸시가 나가고 결과 메일은 미뤄진다.</summary>
    private readonly int _postRunWaitSeconds =
        Math.Clamp(configuration.GetValue("AiTasks:PostRunWaitSeconds", defaultValue: 30), 1, 300);

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    // ── 집어가기 ────────────────────────────────────────────

    /// <summary>요청된 작업을 한 문장으로 집는다. FOR UPDATE SKIP LOCKED 로 기다리지 않고, 같은 대상의 두 건이 겹치지 않게 대상 잠금도 함께 본다.</summary>
    public async Task<List<AiClaim>> ClaimAsync(
        string runnerName, IReadOnlyList<string> kinds, int capacity)
    {
        if (capacity <= 0)
        {
            return [];
        }

        using var db = Open();
        db.Open();

        using var tx = db.BeginTransaction();

        // ① 집을 수 있는 것을 고른다. 대상이 이미 돌고 있으면 건너뛴다.
        var rows = (await db.QueryAsync<AiTask>("""
            SELECT a.task_key        AS TaskKey,
                   a.title           AS Title,
                   a.contents        AS Contents,
                   a.target_key      AS TargetKey,
                   a.target_ref      AS TargetRef,
                   a.runner_kind     AS RunnerKind,
                   a.timeout_minutes AS TimeoutMinutes,
                   a.auto_push       AS AutoPush,
                   a.attempt_count   AS AttemptCount,
                   a.attempt_max     AS AttemptMax
              FROM projmng.ai_task a
              JOIN projmng.ai_target b ON b.target_key = a.target_key
             WHERE a.is_deleted   = false
               AND a.request_flag = 'requested'
               AND a.task_status IN ('idle', 'queued')
               AND (a.requested_at IS NULL OR a.requested_at <= now())
               AND a.runner_kind  = ANY(@kinds)
               AND a.runner_kind = ANY(string_to_array(replace(b.runner_kinds, ' ', ''), ','))
               AND b.is_enabled   = true
               AND b.is_deleted   = false
               AND b.running_run_key IS NULL
               -- **장비가 맞아야 한다.** DB 는 한 벌인데 대상 경로는 장비마다
               -- 다르다 — 개발 장비의 /home/quri/… 를 운영 실행기가 집어 가서
               -- 「대상 폴더가 없습니다」로 실패한 적이 있다(실제로 밟음).
               -- 비어 있으면 아무 장비나 집는다(모든 장비에 같은 경로가 있는 대상).
               AND (b.runner_nm IS NULL OR b.runner_nm = @runnerName)
             ORDER BY a.priority DESC, a.requested_at
             FOR UPDATE OF a SKIP LOCKED
             LIMIT @capacity
            """, new { kinds = kinds.ToArray(), capacity, runnerName }, tx)).ToList();

        var claims = new List<AiClaim>();

        foreach (var task in rows)
        {
            // ② 상태를 준비중으로. 여기서부터 이 건은 내 것이다.
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task
                   SET task_status   = 'preparing',
                       started_at    = now(),
                       attempt_count = attempt_count + 1,
                       row_version   = row_version + 1
                 WHERE task_key = @TaskKey
                """, new { task.TaskKey }, tx);

            // ③ 실행 한 줄을 만든다. **그때 준 지시문을 스냅샷으로 남긴다** —
            //    본문을 고쳐도 지난 실행이 무엇이었는지 남아야 한다.
            var token = NewToken();

            // 다시 시도하는 것이면 지난 실패를 지시문 앞에 얹는다(RetryPrompt). 같은 글을 같은 자리에서 또 읽으면 같은 이유로 또 실패한다.
            // attempt_count 는 ②에서 이미 올렸으므로 여기서 읽은 값에 1을 더한 것이 이번 시도 번호다.
            var attempt = task.AttemptCount + 1;

            var previous = attempt <= 1 ? null : await db.QuerySingleOrDefaultAsync<RetryPrompt.Previous>(
                PreviousRunSql, new { task.TaskKey }, tx);

            var instruction = RetryPrompt.Compose(
                task.Contents, attempt, Math.Max(task.AttemptMax, 1), previous);

            // 시킬 때 지어 둔 이 차수의 제목을 옮긴다. 아직 없으면 null 이고, 그때는 모델 응답 시점에 이 줄에 적는다.
            var runKey = await db.ExecuteScalarAsync<long>("""
                INSERT INTO projmng.ai_task_run
                     ( task_key, seq, run_status, lease_expires_at, token_hash,
                       started_at, instruction, runner_nm, turn_title )
                SELECT @TaskKey,
                       COALESCE(MAX(seq), 0) + 1,
                       'preparing',
                       now() + make_interval(secs => @lease),
                       @hash,
                       now(),
                       @instruction,
                       @runnerName,
                       ( SELECT next_turn_title FROM projmng.ai_task
                          WHERE task_key = @TaskKey )
                  FROM projmng.ai_task_run WHERE task_key = @TaskKey
                RETURNING run_key
                """, new
            {
                task.TaskKey,
                lease = _leaseSeconds,
                hash = Hash(token),
                instruction,
                runnerName,
            }, tx);

            // ④ 대상을 잠근다. 이 값이 있으면 다른 작업이 그 대상을 못 집는다.
            await db.ExecuteAsync("""
                UPDATE projmng.ai_target SET running_run_key = @runKey
                 WHERE target_key = @TargetKey
                """, new { runKey, task.TargetKey }, tx);

            // 옮겨 담은 차수 제목은 여기서 비운다. 안 비우면 다음 차수가 그 제목을 물려받는다.
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task
                   SET last_run_key    = @runKey,
                       next_turn_title = NULL
                 WHERE task_key = @TaskKey
                """, new { runKey, task.TaskKey }, tx);

            var target = await db.QuerySingleAsync<AiTarget>("""
                SELECT target_key AS TargetKey, target_nm AS TargetNm,
                       target_kind AS TargetKind, target_path AS TargetPath,
                       repo_url AS RepoUrl, default_ref AS DefaultRef,
                       credential_ref AS CredentialRef, isolation_mode AS IsolationMode,
                       max_size_mb AS MaxSizeMb, allow_push AS AllowPush,
                       push_ref AS PushRef, gate_mode AS GateMode
                  FROM projmng.ai_target WHERE target_key = @TargetKey
                """, new { task.TargetKey }, tx);

            claims.Add(new AiClaim
            {
                RunKey = runKey,
                Token = token,
                TaskKey = task.TaskKey,
                Title = task.Title,
                Instruction = instruction,
                RunnerKind = task.RunnerKind,
                TimeoutMinutes = task.TimeoutMinutes,
                AutoPush = task.AutoPush,
                TargetRef = task.TargetRef,
                Target = target,

                // 붙은 파일의 **목록만** 싣는다. 바이트는 실행기가 따로 받는다.
                Files = [.. await db.QueryAsync<AiClaimFile>("""
                    SELECT file_key     AS FileKey,
                           file_nm      AS FileNm,
                           content_type AS ContentType,
                           byte_size    AS ByteSize,
                           is_image     AS IsImage
                      FROM projmng.ai_task_file
                     WHERE task_key = @TaskKey
                     ORDER BY file_key
                    """, new { task.TaskKey }, tx)],
            });
        }

        tx.Commit();

        if (claims.Count > 0)
        {
            logger.LogInformation("{Runner} 가 {Count}건을 집었습니다: {Keys}",
                runnerName, claims.Count, string.Join(",", claims.Select(c => c.TaskKey)));
        }

        return claims;
    }

    /// <summary>
    /// 이 작업의 가장 최근에 끝난 실행 한 줄. finished_at IS NOT NULL 로 거르지 않으면 방금 만든 자기 줄을 지난 실행으로 읽는다.
    /// </summary>
    private const string PreviousRunSql = """
        SELECT seq AS Seq, run_status AS Status, exit_code AS ExitCode,
               error_summary AS Error, result_text AS Result
          FROM projmng.ai_task_run
         WHERE task_key = @TaskKey
           AND finished_at IS NOT NULL
         ORDER BY seq DESC
         LIMIT 1
        """;

    // ── 보고 ────────────────────────────────────────────────

    /// <summary>
    /// 살아 있다고 알린다. 임대를 연장하고 취소 요청이 왔는지 돌려준다. 취소를 응답에 싣는 까닭은 실행기가 받는 포트가 없기 때문이다.
    /// </summary>
    public async Task<AiHeartbeat?> HeartbeatAsync(long runKey, string token)
    {
        using var db = Open();

        if (!await AuthorizeAsync(db, runKey, token))
        {
            return null;
        }

        var row = await db.QuerySingleOrDefaultAsync<(string Flag, string Status)>("""
            UPDATE projmng.ai_task_run
               SET lease_expires_at = now() + make_interval(secs => @lease),
                   run_status = CASE WHEN run_status = 'preparing' THEN 'running'
                                     ELSE run_status END
             WHERE run_key = @runKey
            RETURNING ( SELECT request_flag FROM projmng.ai_task
                         WHERE task_key = projmng.ai_task_run.task_key ),
                      ( SELECT task_status FROM projmng.ai_task
                         WHERE task_key = projmng.ai_task_run.task_key )
            """, new { runKey, lease = _leaseSeconds });

        // 작업 쪽 상태도 실행중으로 따라 올린다.
        await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET task_status = 'running'
             WHERE task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
               AND task_status = 'preparing'
            """, new { runKey });

        return new AiHeartbeat { CancelRequested = row.Flag == AiTaskFlag.CancelRequested };
    }

    /// <summary>
    /// 로그 줄을 쌓는다. (run_key, seq) 유일 제약 덕에 같은 구간을 두 번 보내도 한 번만 들어간다.
    /// 상한을 넘으면 조용히 버리지 않고 한 줄을 남긴다 — 아무 말 없이 끊기면 AI 가 멈춘 것으로 읽힌다.
    /// </summary>
    public async Task<bool> AppendLogsAsync(long runKey, string token, IReadOnlyList<AiLogLine> lines)
    {
        using var db = Open();

        if (!await AuthorizeAsync(db, runKey, token))
        {
            return false;
        }

        var count = await db.ExecuteScalarAsync<int>(
            "SELECT log_line_count FROM projmng.ai_task_run WHERE run_key = @runKey",
            new { runKey });

        if (count >= _maxEvents)
        {
            return true;
        }

        var room = _maxEvents - count;
        var take = lines.Take(room).ToList();

        foreach (var line in take)
        {
            // 적재 직전에 가린다. DB 에 들어간 뒤에 가리면 화면·메일·백업으로 이미 새어 나간다.
            var text = SecretMask.Apply(line.Text) ?? string.Empty;

            if (text.Length > _maxEventLength)
            {
                text = text[.._maxEventLength] + " …(잘림)";
            }

            await db.ExecuteAsync("""
                INSERT INTO projmng.ai_task_log (run_key, seq, stream, log_text, log_at)
                VALUES (@runKey, @Seq, @Stream, @text, now())
                ON CONFLICT (run_key, seq) DO NOTHING
                """, new { runKey, line.Seq, Stream = line.Stream ?? "stdout", text });
        }

        await db.ExecuteAsync("""
            UPDATE projmng.ai_task_run
               SET log_line_count = ( SELECT COUNT(*) FROM projmng.ai_task_log WHERE run_key = @runKey )
             WHERE run_key = @runKey
            """, new { runKey });

        if (lines.Count > room)
        {
            await db.ExecuteAsync("""
                INSERT INTO projmng.ai_task_log (run_key, seq, stream, log_text, log_at)
                VALUES (@runKey, @seq, 'system',
                        '[로그 한도] 이 실행의 로그가 상한에 닿아 이후 줄은 남기지 않습니다.', now())
                ON CONFLICT (run_key, seq) DO NOTHING
                """, new { runKey, seq = int.MaxValue });
        }

        return true;
    }

    /// <summary>
    /// 끝났다고 보고한다.
    /// </summary>
    public async Task<bool> CompleteAsync(long runKey, string token, AiCompleteRequest done)
    {
        using var db = Open();
        db.Open();

        if (!await AuthorizeAsync(db, runKey, token))
        {
            return false;
        }

        using var tx = db.BeginTransaction();

        var status = done.Status switch
        {
            "succeeded" or "failed" or "timeout" or "canceled" or "interrupted" => done.Status,
            _ => done.ExitCode == 0 ? AiTaskStatus.Succeeded : AiTaskStatus.Failed,
        };

        // 결과문은 화면과 메일로 나가므로 여기서 반드시 가린다. 메일은 한 번 나가면 회수할 수 없다.
        done.ResultText = SecretMask.Apply(done.ResultText);
        done.Error = SecretMask.Apply(done.Error);
        done.DiffStat = SecretMask.Apply(done.DiffStat);

        // 경로 목록도 같은 손길을 거친다 — 작업공간 경로에 사람 이름이나 토큰이 섞일 수 있다.
        done.ChangedFiles = SecretMask.Apply(done.ChangedFiles);

        await db.ExecuteAsync("""
            UPDATE projmng.ai_task_run
               SET run_status    = @status,
                   finished_at   = now(),
                   exit_code     = @ExitCode,
                   error_summary = @Error,
                   git_branch    = @Branch,
                   base_sha      = @BaseSha,
                   diff_stat     = @DiffStat,
                   changed_files = @ChangedFiles,
                   result_text   = @ResultText,
                   session_id    = @SessionId,
                   session_kind  = @SessionKind,
                   workspace_path = @WorkspacePath,
                   lease_expires_at = NULL
             WHERE run_key = @runKey
            """, new
        {
            runKey, status, done.ExitCode, done.Error, done.Branch, done.BaseSha,
            done.DiffStat, done.ChangedFiles, done.ResultText, done.SessionId, done.SessionKind,
            done.WorkspacePath,
        }, tx);

        // 자동 재시도는 실패이고 상한이 남았을 때만 한다. 연락 끊김(interrupted)은 CLI 가 아직 돌 수 있어 제외하고,
        // 원본 직접 대상은 지난 시도의 수정 위에서 다시 도니 작업공간이 실행마다 갈리는 대상만 한다.
        // 지난 실패를 지시문에 얹는 일은 집어 갈 때(ClaimAsync) 한다 — 여기서 얹으면 사람이 읽는 본문이 바뀐다.
        var counters = await db.QuerySingleAsync<Counters>("""
            SELECT t.task_key AS TaskKey, t.attempt_count AS Count, t.attempt_max AS Max,
                   COALESCE(g.isolation_mode,
                            CASE WHEN g.target_kind = 'repo' THEN 'worktree' ELSE 'copy' END)
                     AS Isolation,
                   t.pending_continue_addition AS PendingAddition,
                   t.pending_continue_runner_kind AS PendingRunnerKind,
                   t.pending_continue_user_id AS PendingUserId
              FROM projmng.ai_task t
              JOIN projmng.ai_target g ON g.target_key = t.target_key
             WHERE t.task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
            """, new { runKey }, tx);

        var retry = status is AiTaskStatus.Failed or AiTaskStatus.Timeout
                    && counters.Count < Math.Max(counters.Max, 1)
                    && !string.Equals(counters.Isolation, "inplace", StringComparison.OrdinalIgnoreCase);

        // 작업 쪽 요청여부를 되돌린다 — 그대로 두면 감시자가 같은 건을 또 집는다. 다시 시도할 때만 요청을 다시 세운다.
        await db.ExecuteAsync("""
            UPDATE projmng.ai_task t
               SET task_status                  = CASE WHEN @retry THEN 'queued' ELSE @status END,
                   request_flag                 = CASE WHEN @retry THEN 'requested' ELSE 'none' END,
                   pending_continue_addition    = CASE WHEN @status = 'succeeded' THEN NULL ELSE t.pending_continue_addition END,
                   pending_continue_runner_kind = CASE WHEN @status = 'succeeded' THEN NULL ELSE t.pending_continue_runner_kind END,
                   pending_continue_user_id     = CASE WHEN @status = 'succeeded' THEN NULL ELSE t.pending_continue_user_id END,
                   pending_continue_at          = CASE WHEN @status = 'succeeded' THEN NULL ELSE t.pending_continue_at END,

                   -- **다시 넣은 것은 지연 시간 뒤로 요청 시각을 찍는다.** 안 찍으면
                   -- 처음 보낸 시각이 그대로 남아, 감시자가 보기에 이미
                   -- 집어가기 제한 시간(6.8)을 넘긴 건이 된다 — 실행기가
                   -- 곧바로 집어 가는데도 `last_error` 가 「집어 가지
                   -- 않았습니다」로 덮여 **왜 실패했는지가 지워진다.**
                   -- 지연 시간 동안에는 ClaimAsync 가 집어 가지 않는다.
                   requested_at   = CASE WHEN @retry THEN now() + make_interval(secs => @retryDelay)
                                         ELSE t.requested_at END,
                   finished_at    = now(),
                   duration_ms    = EXTRACT(EPOCH FROM (now() - t.started_at)) * 1000,
                   last_exit_code = @ExitCode,
                   last_error     = @Error,
                   pushed_commit  = COALESCE(@PushedCommit, t.pushed_commit),
                   previous_tag   = COALESCE(@PreviousTag, t.previous_tag),
                   workspace_path = @WorkspacePath,
                   git_branch     = @Branch,
                   turn_no        = t.turn_no + 1,
                   row_version    = t.row_version + 1
             WHERE t.task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
            """, new
        {
            runKey, status, retry, retryDelay = _retryDelaySeconds, done.ExitCode, done.Error,
            done.PushedCommit, done.PreviousTag, done.WorkspacePath, done.Branch,
        }, tx);

        // 대상 잠금을 푼다. **이것을 빠뜨리면 그 대상이 영영 막힌다.**
        await db.ExecuteAsync("""
            UPDATE projmng.ai_target SET running_run_key = NULL WHERE running_run_key = @runKey
            """, new { runKey }, tx);

        tx.Commit();

        logger.LogInformation("실행 {RunKey} 가 {Status} 로 끝났습니다 (exit {Exit}).",
            runKey, status, done.ExitCode);

        if (retry)
        {
            logger.LogInformation(
                "실행 {RunKey} 가 {Status} 라 다시 시도합니다 ({Count}/{Max}번째까지, {Delay}초 뒤).",
                runKey, status, counters.Count, Math.Max(counters.Max, 1), _retryDelaySeconds);

            // **종을 울린다.** 지연 시간이 있으면 대기 후 울리고, 없으면 바로 울린다.
            // 안 울려도 폴링이 받아 준다 — 큐는 거들 뿐이다.
            if (_retryDelaySeconds > 0)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(_retryDelaySeconds));
                    queue.Ring(counters.TaskKey);
                });
            }
            else
            {
                queue.Ring(counters.TaskKey);
            }

            // 중간 실패는 알리지 않는다(마지막 판정만). mail_sent_at 을 여기서 찍지 않으면 따라잡기가 안 보낸 건으로 주워 가서,
            // 알리지 않기로 한 중간 실패 메일이 뒤늦게 나간다. 찍는 것은 보낸 시각이 아니라 「이 실행의 메일 길은 끝났다」는 표시다.
            await MarkMailDoneAsync(runKey);

            return true;
        }

        // 알림·요약·제목은 **기다리지 않는다.** 실행기의 완료 보고가 그 시간만큼
        // 늦어질 이유가 없다 — 그 사이 실행 슬롯이 묶인다.
        _ = SummarizeThenNotifyAsync(runKey);

        if (!retry && status == AiTaskStatus.Succeeded && !string.IsNullOrWhiteSpace(counters.PendingAddition))
        {
            logger.LogInformation("작업 {TaskKey} 가 성공 완료되어 예약된 이어서 지시를 자동 실행합니다.", counters.TaskKey);
            _ = Task.Run(async () =>
            {
                try
                {
                    // 완료 후 요약/알림 처리가 먼저 시작될 수 있도록 짧게 대기
                    await Task.Delay(1000);
                    using var scope = scopeFactory.CreateScope();
                    var taskService = scope.ServiceProvider.GetRequiredService<AiTaskService>();
                    var contResult = await taskService.ContinueAsync(
                        counters.TaskKey, counters.PendingAddition, counters.PendingRunnerKind, counters.PendingUserId);
                    if (contResult.IsOk)
                    {
                        var reqResult = await taskService.RequestAsync(counters.TaskKey, counters.PendingUserId);
                        logger.LogInformation("작업 {TaskKey} 예약된 이어서 지시 자동 요청 완료: ok={Ok}",
                            counters.TaskKey, reqResult.IsOk);
                    }
                    else
                    {
                        logger.LogWarning("작업 {TaskKey} 예약된 이어서 지시 실패: {Conflict}",
                            counters.TaskKey, contResult.ConflictMessage);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "작업 {TaskKey} 예약된 이어서 지시 처리 중 예외 발생", counters.TaskKey);
                }
            });
        }

        return true;
    }

    /// <summary>
    /// 끝난 실행의 뒤처리. 요약과 제목을 상한까지 기다렸다가 앱푸시 · 결과 메일 순으로 보낸다.
    /// 앱푸시가 제목을 기다리는 까닭: 「빠른 지시」의 제목은 지시문을 끊은 글이라, 기다리지 않으면 알림창과 목록의 제목이 갈린다.
    /// </summary>
    private async Task SummarizeThenNotifyAsync(long runKey)
    {
        try
        {
            // ① 처리 요약 + 제목 — **상한을 걸고** 기다린다.
            var summarized = await PrepareSummaryAndTitleAsync(runKey);

            // ② 앱푸시 — 제목이 붙은 뒤에 쏜다.
            // 푸시가 notify_error 칸을 비우므로, 메일보다 먼저 쏴야 메일이 남긴 사유를 지우지 않는다.
            await notifier.SendPushAsync(runKey);

            // ③ 결과 메일 — **①이 빈손이면 보내지 않고 미룬다.**
            await notifier.SendMailAsync(runKey, deferUntilSummary: true);

            if (!summarized)
            {
                logger.LogInformation(
                    "실행 {RunKey} 는 {Sec}초 안에 처리 요약이 오지 않아 결과 메일을 미뤘습니다.",
                    runKey, _postRunWaitSeconds);
            }
        }
        catch (Exception ex)
        {
            // 셋 다 스스로 삼키도록 돼 있지만, **여기는 아무도 지켜보지 않는
            // 갈래**라 새어 나온 것이 있으면 로그로 끝내야 한다.
            logger.LogWarning(ex, "끝난 실행의 뒤처리에 실패했습니다 (run {RunKey}).", runKey);
        }
    }

    /// <summary>
    /// 처리 요약과 제목을 상한까지 기다려 적는다. 요약이 손에 들어왔으면 true — 그 값으로 결과 메일을 지금 보낼지 미룰지가 갈린다.
    /// 요약과 제목 되받기의 상한은 나눠 쓴다. 요약이 상한을 다 쓰면 되받기는 곧장 돌아와 앱푸시가 상한의 두 배를 기다리지 않는다.
    /// </summary>
    private async Task<bool> PrepareSummaryAndTitleAsync(long runKey)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_postRunWaitSeconds));

        try
        {
            // 처리 파일 합치기와 요약은 나란히 보낸다 — 차례로 부르면 앱푸시가 그 둘을 더한 만큼 늦는다.
            var merging = files.EnsureAsync(runKey, cts.Token);

            // 요약 한 번에 제목까지 온다. 여기서 제목 칸이 채워지면 아래는
            // 모델을 안 부르고 돌아온다(`title_run_key` 도장).
            var summary = await summaries.EnsureAsync(runKey, ct: cts.Token);

            // 요약이 제목을 못 붙인 건의 되받기 — 짧은 제목 한 줄이라 빠르다.
            await titler.TitleAsync(runKey, cts.Token);

            // **메일이 이것을 읽는다**(「처리 파일」 칸). 여기서 안 기다리면
            // 메일에는 git 목록만 실리고 화면에만 합쳐진 것이 뜬다 — 같은
            // 실행이 두 말을 하는 자리다.
            await merging;

            return summary is not null;
        }
        catch (OperationCanceledException)
        {
            // 상한에 걸렸다. **여기서 돌아서지 않는다** — 부르는 쪽이 앱푸시를
            // 쏘고 메일을 미루는 것으로 이어 간다.
            return false;
        }
    }

    /// <summary>
    /// 이 실행의 메일 길을 끝났다고 적는다(ai_task_run.mail_sent_at). 보낼 일이 없다고 판정한 자리에서 부른다 — 적지 않으면 따라잡기가 안 보낸 건으로 주워 간다.
    /// </summary>
    private async Task MarkMailDoneAsync(long runKey)
    {
        try
        {
            using var db = Open();

            await db.ExecuteAsync("""
                UPDATE projmng.ai_task_run
                   SET mail_sent_at = now()
                 WHERE run_key = @runKey
                   AND mail_sent_at IS NULL
                """, new { runKey });
        }
        catch (Exception ex)
        {
            // 못 적어도 완료 처리는 그대로 간다. 최악이 「중간 실패 메일 한 통」이다.
            logger.LogWarning(ex, "중간 실패의 메일 도장을 찍지 못했습니다 (run {RunKey}).", runKey);
        }
    }

    /// <summary>재시도 판정에 필요한 값. 한 번에 읽으려고 묶었다.</summary>
    private sealed class Counters
    {
        public long TaskKey { get; set; }

        /// <summary>지금까지 몇 번 집어 갔나. 집어 갈 때 1씩 오른다.</summary>
        public int Count { get; set; }

        /// <summary>최대 몇 번까지.</summary>
        public int Max { get; set; }

        /// <summary>
        /// 대상의 격리 방식. 원본 직접(isolation_mode)이면 자동 재시도를 하지 않는다. 비어 있으면 격리된 기본값으로 본다.
        /// </summary>
        public string? Isolation { get; set; }
        public string? PendingAddition { get; set; }
        public string? PendingRunnerKind { get; set; }
        public string? PendingUserId { get; set; }
    }

    // ── 조회 (화면이 쓴다) ──────────────────────────────────

    public async Task<List<AiTaskRun>> RunsAsync(long taskKey)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiTaskRun>("""
            SELECT run_key AS RunKey, task_key AS TaskKey, seq AS Seq,
                   run_status AS RunStatus, started_at AS StartedAt,
                   finished_at AS FinishedAt, exit_code AS ExitCode,
                   error_summary AS ErrorSummary, git_branch AS GitBranch,
                   base_sha AS BaseSha, diff_stat AS DiffStat,
                   changed_files AS ChangedFiles,
                   log_line_count AS LogLineCount, result_text AS ResultText,
                   instruction AS Instruction, summary_text AS SummaryText,
                   turn_title AS TurnTitle
              FROM projmng.ai_task_run
             WHERE task_key = @taskKey
             ORDER BY seq DESC
            """, new { taskKey });

        return [.. rows];
    }

    /// <summary>
    /// 로그 꼬리. <paramref name="fromSeq"/> 보다 큰 줄만 준다 — 화면이
    /// 2~3초마다 이것을 불러 이어 붙인다.
    /// </summary>
    public async Task<List<AiLogLine>> LogsAsync(long runKey, int fromSeq, int limit = 500)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiLogLine>("""
            SELECT seq AS Seq, stream AS Stream, log_text AS Text, log_at AS LogAt
              FROM projmng.ai_task_log
             WHERE run_key = @runKey AND seq > @fromSeq
             ORDER BY seq
             LIMIT @limit
            """, new { runKey, fromSeq, limit });

        return [.. rows];
    }

    // ── 안쪽 ────────────────────────────────────────────────

    /// <summary>
    /// 실행기가 이 실행에 붙은 파일 하나를 받아 간다. 실행기는 게이트웨이를 지나지 않으므로 run 토큰으로 연다.
    /// 조건은 이 실행 작업에 붙은 파일로 한정한다 — file_key 만 보면 토큰 하나로 남의 지시의 파일을 받을 수 있다.
    /// </summary>
    /// <returns>토큰이 다르거나 그 실행의 파일이 아니면 <c>null</c>.</returns>
    public async Task<(string FileNm, string ContentType, byte[] Bytes)?> RunFileAsync(
        long runKey, string token, long fileKey)
    {
        using var db = Open();

        if (!await AuthorizeAsync(db, runKey, token))
        {
            return null;
        }

        var row = await db.QuerySingleOrDefaultAsync<RunFile>("""
            SELECT f.file_nm      AS FileNm,
                   f.content_type AS ContentType,
                   f.content      AS Content
              FROM projmng.ai_task_file f
              JOIN projmng.ai_task_run  r ON r.task_key = f.task_key
             WHERE r.run_key  = @runKey
               AND f.file_key = @fileKey
            """, new { runKey, fileKey });

        return row is null ? null : (row.FileNm, row.ContentType, row.Content ?? []);
    }

    /// <summary>실행기에게 내보낼 파일 한 줄. 이 서비스 안에서만 쓴다.</summary>
    private sealed class RunFile
    {
        public string FileNm { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[]? Content { get; set; }
    }

    private static async Task<bool> AuthorizeAsync(IDbConnection db, long runKey, string token)
    {
        var hash = await db.ExecuteScalarAsync<string?>("""
            SELECT token_hash FROM projmng.ai_task_run
             WHERE run_key = @runKey AND finished_at IS NULL
            """, new { runKey });

        return hash is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(Hash(token)));
    }

    private static string NewToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>실행기가 집어 간 일 한 건. <b>토큰은 이때 한 번만 나간다.</b></summary>
public sealed class AiClaim
{
    public long RunKey { get; set; }

    /// <summary>이 실행에서만 쓰는 1회용 토큰. 서버에는 해시만 남는다.</summary>
    public string Token { get; set; } = string.Empty;

    public long TaskKey { get; set; }
    public string? Title { get; set; }

    /// <summary>AI 에게 줄 지시문.</summary>
    public string? Instruction { get; set; }

    public string? RunnerKind { get; set; }
    public int TimeoutMinutes { get; set; }
    public bool AutoPush { get; set; }
    public string? TargetRef { get; set; }

    public AiTarget? Target { get; set; }

    /// <summary>
    /// 지시에 함께 올라온 파일들. 바이트는 여기 없고 실행기가 번호로 하나씩 받아 간다.
    /// 실어 보내면 집어가기 한 번이 사진 몇 장을 통째로 끌고 온다 — 집어가기는 1초마다 불리는 자리다.
    /// </summary>
    public List<AiClaimFile> Files { get; set; } = [];
}

/// <summary>집어 간 일에 붙어 온 파일 한 개.</summary>
public sealed class AiClaimFile
{
    public long FileKey { get; set; }
    public string FileNm { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long ByteSize { get; set; }
    public bool IsImage { get; set; }
}

public sealed class AiHeartbeat
{
    /// <summary>사람이 취소를 눌렀다. 실행기는 프로세스를 죽이고 보고한다.</summary>
    public bool CancelRequested { get; set; }
}

public sealed class AiLogLine
{
    public int Seq { get; set; }
    public string? Stream { get; set; }
    public string? Text { get; set; }
    public DateTime? LogAt { get; set; }
}

public sealed class AiCompleteRequest
{
    public string? Status { get; set; }
    public int ExitCode { get; set; }
    public string? Error { get; set; }
    public string? Branch { get; set; }
    public string? BaseSha { get; set; }
    public string? DiffStat { get; set; }

    /// <summary>
    /// 이 실행이 건드린 파일 목록. 한 줄에 <c>상태\t경로</c> 다
    /// (<c>AiTaskRunner.Workspace.ChangedFilesAsync</c>).
    /// </summary>
    public string? ChangedFiles { get; set; }

    public string? ResultText { get; set; }
    public string? SessionId { get; set; }
    public string? SessionKind { get; set; }
    public string? WorkspacePath { get; set; }
    public string? PushedCommit { get; set; }
    public string? PreviousTag { get; set; }
}

/// <summary>실행 한 번의 기록.</summary>
public sealed class AiTaskRun
{
    public long RunKey { get; set; }
    public long TaskKey { get; set; }
    public int Seq { get; set; }
    public string? RunStatus { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? ErrorSummary { get; set; }
    public string? GitBranch { get; set; }
    public string? BaseSha { get; set; }
    public string? DiffStat { get; set; }

    /// <summary>
    /// 「처리 파일」 — 이 실행이 건드린 파일 목록. 한 줄에 <c>상태\t경로</c>.
    /// <b>없을 수 있다</b>(이 칸이 생기기 전의 건 · git 아닌 대상).
    /// </summary>
    public string? ChangedFiles { get; set; }

    public int LogLineCount { get; set; }

    /// <summary>AI 의 마지막 답. 화면과 메일이 둘 다 이것을 읽는다.</summary>
    public string? ResultText { get; set; }

    /// <summary>그때 실제로 준 지시문.</summary>
    public string? Instruction { get; set; }

    public string? SummaryText { get; set; }

    /// <summary>
    /// 이 차수의 제목. 「지시와 결과」 탭의 접힌 머리줄이 읽는다. 없을 수 있다 — 모델이 끝내 답하지 않은 건이 비어 있다.
    /// </summary>
    public string? TurnTitle { get; set; }
}
