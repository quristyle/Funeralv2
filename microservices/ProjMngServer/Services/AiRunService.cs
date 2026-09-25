using System.Data;
using System.Security.Cryptography;
using System.Text;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 실행기가 쓰는 쪽 — 집어가기 · 하트비트 · 로그 · 완료.
/// </summary>
/// <remarks>
/// <para>
/// 사람이 쓰는 <see cref="AiTaskService"/> 와 갈라 두었다. 인증이 다르기
/// 때문이다 — 저쪽은 로그인한 사람이고 이쪽은 <b>run 별 1회용 토큰</b>이다.
/// 배포 도구가 같은 방식을 쓴다(설계 6.9).
/// </para>
/// </remarks>
public sealed class AiRunService(
    IConfiguration configuration, AiTaskNotifier notifier, AiRunSummaryWriter summaries,
    AiTaskTitler titler, AiTaskQueue queue, ILogger<AiRunService> logger)
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

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    // ── 집어가기 ────────────────────────────────────────────

    /// <summary>
    /// 요청된 작업을 <b>한 문장으로</b> 집는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>FOR UPDATE SKIP LOCKED</c> 가 요점이다. 실행기가 둘이거나 종과
    /// 폴링이 겹쳐 동시에 들어와도 서로 다른 건을 집고, 기다리지도 않는다.
    /// 읽고 → 판단하고 → 쓰는 세 걸음으로 나누면 같은 건을 둘이 집는다.
    /// </para>
    /// <para>
    /// <b>대상 잠금을 함께 본다.</b> 같은 대상에 두 건이 동시에 돌면 폴더
    /// 대상은 서로의 파일을 덮는다(설계 9.5).
    /// </para>
    /// </remarks>
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

            // 다시 시도하는 것이면 **지난번에 왜 실패했는지**를 지시문 앞에
            // 얹는다. 안 얹으면 같은 글을 같은 자리에서 다시 읽는 것뿐이라
            // 대개 같은 이유로 또 실패한다(`RetryPrompt` 머리말).
            //
            // `attempt_count` 는 위 ②에서 이미 1 올렸으므로, 여기서 읽은
            // 값에 1을 더한 것이 이번 시도 번호다.
            var attempt = task.AttemptCount + 1;

            var previous = attempt <= 1 ? null : await db.QuerySingleOrDefaultAsync<RetryPrompt.Previous>(
                PreviousRunSql, new { task.TaskKey }, tx);

            var instruction = RetryPrompt.Compose(
                task.Contents, attempt, Math.Max(task.AttemptMax, 1), previous);

            var runKey = await db.ExecuteScalarAsync<long>("""
                INSERT INTO projmng.ai_task_run
                     ( task_key, seq, run_status, lease_expires_at, token_hash,
                       started_at, instruction, runner_nm )
                SELECT @TaskKey,
                       COALESCE(MAX(seq), 0) + 1,
                       'preparing',
                       now() + make_interval(secs => @lease),
                       @hash,
                       now(),
                       @instruction,
                       @runnerName
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

            await db.ExecuteAsync("""
                UPDATE projmng.ai_task SET last_run_key = @runKey WHERE task_key = @TaskKey
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
    /// 이 작업의 <b>가장 최근에 끝난 실행</b> 한 줄. 다시 시도할 때 지시문에
    /// 얹을 실패 이야기를 여기서 꺼낸다.
    /// </summary>
    /// <remarks>
    /// <c>finished_at IS NOT NULL</c> 로 거른다 — 지금 막 만든 이번 실행 줄은
    /// 아직 안 끝났으므로 걸리지 않는다. 그 조건이 없으면 <b>자기 자신</b>을
    /// 지난 실행으로 읽어 빈 실패 이야기를 얹는다.
    /// </remarks>
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
    /// 살아 있다고 알린다. 임대를 연장하고 <b>취소 요청이 왔는지 돌려준다</b>.
    /// </summary>
    /// <remarks>
    /// 취소를 이 응답에 실어 보내는 이유: 실행기에게 들어오는 길을 새로 열지
    /// 않으려는 것이다. 실행기는 받는 포트가 없다(설계 7.1).
    /// </remarks>
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
    /// 로그 줄을 쌓는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>같은 구간을 두 번 보내도 한 번만 들어간다</b> — <c>(run_key, seq)</c> 에
    /// 유일 제약이 걸려 있고 충돌은 무시한다. 네트워크가 끊겼다 붙으면
    /// 실행기는 반드시 다시 보낸다.
    /// </para>
    /// <para>
    /// 상한을 넘으면 <b>조용히 버리지 않고</b> 한 줄을 남긴다. 아무 말 없이
    /// 끊기면 「AI 가 거기서 멈췄나」로 읽힌다.
    /// </para>
    /// </remarks>
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
            // **적재 직전에 가린다.** DB 에 들어간 뒤에 가리면 이미 늦다 —
            // 화면에도 메일에도 나가고, 지워도 백업에 남는다(설계 9.6).
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

        // 결과문은 화면에도 뜨고 **메일로 나간다.** 메일은 한 번 나가면
        // 회수할 수 없는 유일한 출구라 여기서 반드시 가린다.
        done.ResultText = SecretMask.Apply(done.ResultText);
        done.Error = SecretMask.Apply(done.Error);
        done.DiffStat = SecretMask.Apply(done.DiffStat);

        await db.ExecuteAsync("""
            UPDATE projmng.ai_task_run
               SET run_status    = @status,
                   finished_at   = now(),
                   exit_code     = @ExitCode,
                   error_summary = @Error,
                   git_branch    = @Branch,
                   base_sha      = @BaseSha,
                   diff_stat     = @DiffStat,
                   result_text   = @ResultText,
                   session_id    = @SessionId,
                   session_kind  = @SessionKind,
                   workspace_path = @WorkspacePath,
                   lease_expires_at = NULL
             WHERE run_key = @runKey
            """, new
        {
            runKey, status, done.ExitCode, done.Error, done.Branch, done.BaseSha,
            done.DiffStat, done.ResultText, done.SessionId, done.SessionKind, done.WorkspacePath,
        }, tx);

        // ── 다시 시도할 것인가 ──────────────────────────────
        //
        // 조건이 셋이고, **셋 다 설계 6.11 에서 나온다.**
        //
        // ① 실패로 끝났고 상한이 남았다(`attempt_max`, 1~5).
        //
        // ② **연락 끊김(interrupted)은 제외한다.** 서버가 보기엔 죽었지만
        //    CLI 는 아직 돌고 있을 수 있다. 거기서 같은 일을 또 주면 둘이
        //    같은 저장소를 고친다. 취소도 제외다 — 사람이 그만두라고 한 것이다.
        //
        // ③ **작업공간이 실행마다 갈리는 대상만.** 6.11 이 자동 재시도의
        //    전제로 못 박아 둔 조건이다. 원본 직접(inplace)은 지난 시도가
        //    고쳐 놓은 파일 위에서 다시 도는 셈이라, 두 번째 시도가 무엇을
        //    보고 있는지 아무도 모른다.
        //
        // 다시 넣을 때 **지시문에 지난 실패를 얹는 일은 집어 갈 때** 한다
        // (`ClaimAsync`). 여기서 얹으면 본문(`contents`)을 고치는 셈이 되어
        // 사람이 화면에서 읽는 글이 바뀐다.
        var counters = await db.QuerySingleAsync<Counters>("""
            SELECT t.task_key AS TaskKey, t.attempt_count AS Count, t.attempt_max AS Max,
                   COALESCE(g.isolation_mode,
                            CASE WHEN g.target_kind = 'repo' THEN 'worktree' ELSE 'copy' END)
                     AS Isolation
              FROM projmng.ai_task t
              JOIN projmng.ai_target g ON g.target_key = t.target_key
             WHERE t.task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
            """, new { runKey }, tx);

        var retry = status is AiTaskStatus.Failed or AiTaskStatus.Timeout
                    && counters.Count < Math.Max(counters.Max, 1)
                    && !string.Equals(counters.Isolation, "inplace", StringComparison.OrdinalIgnoreCase);

        // 작업 쪽 요약. **요청여부를 되돌린다** — 한 번 시킨 것이 끝났으므로
        // 그대로 두면 감시자가 같은 건을 또 집는다.
        //
        // 다시 시도할 때만 예외다. 그때는 요청을 **다시 세워** 두어야 실행기가
        // 집어 간다 — 상태도 대기로 되돌린다.
        await db.ExecuteAsync("""
            UPDATE projmng.ai_task t
               SET task_status    = CASE WHEN @retry THEN 'queued' ELSE @status END,
                   request_flag   = CASE WHEN @retry THEN 'requested' ELSE 'none' END,

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

            // **중간 실패는 알리지 않는다.** 세 번 시도하는 작업이 두 번
            // 실패하면 「실패」 메일이 두 통 먼저 가고 마지막에 「성공」이
            // 온다 — 받는 사람은 그 순서를 못 읽는다. 마지막 판정만 알린다.
            return true;
        }

        // 알림·요약·제목은 **기다리지 않는다.** 실행기의 완료 보고가 그 시간만큼
        // 늦어질 이유가 없다 — 그 사이 실행 슬롯이 묶인다.
        _ = SummarizeThenNotifyAsync(runKey);

        return true;
    }

    /// <summary>
    /// 끝난 실행의 뒤처리 — <b>앱푸시를 먼저 쏘고, 요약을 적고, 제목을 짓고,
    /// 그다음에 메일을 보낸다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>앱푸시가 맨 앞이다.</b> 아래 둘은 모델을 부르는 일이라 십수 초에서 몇
    /// 분까지 걸린다 — 요약은 45초에서 끊고 두 번까지 다시 부르며
    /// (<see cref="AiRunSummaryWriter"/>), 제목 짓기가 한 번 더 부른다.
    /// 그 뒤에 알리던 때의 운영 실측이 <b>끝난 시각에서 푸시까지 중앙 13초 ·
    /// 상위 10% 45초 · 최대 177초</b>였다(2026-09-25, <c>scom.push_send_logs</c>
    /// 와 <c>ai_task_run.finished_at</c> 대조). <b>푸시에 실리는 것은 상태와
    /// 제목뿐</b>이라 그 둘을 기다릴 까닭이 없다.
    /// </para>
    /// <para>
    /// <b>요약은 알림 설정과 무관하다.</b> 메일도 앱푸시도 끄고 시킨 건은 화면의
    /// 「처리 요약」 칸이 결과를 읽는 유일한 자리다 — 그런데 요약을 만드는 일이
    /// 알림 보내기 안에 들어 있으면 <b>알림을 끈 사람에게만 요약이 없다.</b>
    /// 그래서 여기서 걸음을 나눠 부른다.
    /// </para>
    /// <para>
    /// <b>뒤의 순서는 그대로 전부다.</b> 제목 짓기(<see cref="AiTaskTitler"/>)는
    /// 요약의 첫 줄을 읽어 모델 호출을 아끼고, 결과 메일은 그 요약과 <b>새로
    /// 지어진 제목</b>을 싣는다. 나란히 돌리면 메일이 요약 없이 나가거나,
    /// 메일과 화면에 같은 건이 서로 다른 제목으로 남는다.
    /// </para>
    /// <para>
    /// <b>중간 실패는 여기까지 오지 않는다.</b> 다시 시도할 건은 위에서 돌아간다 —
    /// 세 번 시도하는 작업이 시도마다 요약을 만들면 모델을 세 번 부르고, 그 요약이
    /// 작업 제목을 두 번 갈아 치운다. 마지막 판정만 남긴다(알림과 같은 규칙).
    /// </para>
    /// </remarks>
    private async Task SummarizeThenNotifyAsync(long runKey)
    {
        try
        {
            // **띄워 놓고 기다리지 않는다.** 앞세우는 것이 목적이지 앞을 막는
            // 것이 목적이 아니다 — 알림 서버가 굼뜬 날 여기서 기다리면 이번에는
            // **처리 요약이 그만큼 늦게 적힌다**(아래 두 번째 문단의 불변식).
            var push = notifier.SendPushAsync(runKey);

            await summaries.EnsureAsync(runKey);
            await titler.TitleAsync(runKey);

            // **메일보다 먼저 거둔다.** 둘이 같은 칸(`ai_task.notify_error`)에
            // 적는데 푸시가 그 칸을 비우는 쪽이라, 순서가 뒤집히면 메일이 남긴
            // 사유를 푸시가 지운다.
            await push;

            await notifier.SendMailAsync(runKey);
        }
        catch (Exception ex)
        {
            // 셋 다 스스로 삼키도록 돼 있지만, **여기는 아무도 지켜보지 않는
            // 갈래**라 새어 나온 것이 있으면 로그로 끝내야 한다.
            logger.LogWarning(ex, "끝난 실행의 뒤처리에 실패했습니다 (run {RunKey}).", runKey);
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
        /// 대상의 격리 방식. <b>원본 직접이면 자동 재시도를 하지 않는다</b>(6.11).
        ///
        /// <para>
        /// 우리가 묻는 것은 <b>「원본 직접인가」 하나뿐</b>이고, 그 값은 언제나
        /// <c>isolation_mode</c> 에 적혀 있다 — 비어 있을 때의 기본값은 worktree
        /// 아니면 copy 이고 둘 다 격리된 자리다. 그래서 실행기의
        /// <c>Workspace.IsolationOf</c> 가 <c>.git</c> 유무로 더 따지는 것과
        /// 갈려도 이 판정의 답은 같다.
        /// </para>
        /// </summary>
        public string? Isolation { get; set; }
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
                   log_line_count AS LogLineCount, result_text AS ResultText,
                   instruction AS Instruction, summary_text AS SummaryText
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
    /// 이 토큰이 그 실행의 것인가. <b>끝난 실행은 거절한다</b> —
    /// 그러면 늦게 온 보고가 끝난 기록을 덮지 않는다.
    /// </summary>
    /// <summary>
    /// 실행기가 <b>이 실행에 붙은 파일 하나</b>를 받아 간다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 실행기는 게이트웨이를 지나지 않으므로(<c>AiRunnerController</c> 머리말)
    /// 포털 계정이 없다. 그래서 이미 들고 있는 <b>run 토큰</b>으로 연다 —
    /// 로그를 올리고 심장 소리를 보내는 그 토큰이다.
    /// </para>
    /// <para>
    /// <b>그 실행의 작업에 붙은 것만 나간다.</b> 조건을 <c>file_key</c> 하나로
    /// 두면 토큰 하나로 <b>남의 지시에 붙은 사진을 전부</b> 받아 갈 수 있다.
    /// </para>
    /// </remarks>
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
    /// 지시에 함께 올라온 파일들. <b>바이트는 여기 없다</b> — 실행기가 번호로
    /// 하나씩 받아 간다(<c>/api/ai-runner/runs/{runKey}/files/{fileKey}</c>).
    /// </summary>
    /// <remarks>
    /// 실어 보내면 집어가기 한 번이 사진 몇 장을 통째로 끌고 온다 —
    /// 집어가기는 실행기가 <b>비어 있어도 1초마다 부르는</b> 자리다.
    /// </remarks>
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
    public int LogLineCount { get; set; }

    /// <summary>AI 의 마지막 답. 화면과 메일이 둘 다 이것을 읽는다.</summary>
    public string? ResultText { get; set; }

    /// <summary>그때 실제로 준 지시문.</summary>
    public string? Instruction { get; set; }

    public string? SummaryText { get; set; }
}
