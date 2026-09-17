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
    IConfiguration configuration, AiTaskNotifier notifier, ILogger<AiRunService> logger)
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
                   a.attempt_count   AS AttemptCount
              FROM projmng.ai_task a
              JOIN projmng.ai_target b ON b.target_key = a.target_key
             WHERE a.is_deleted   = false
               AND a.request_flag = 'requested'
               AND a.task_status IN ('idle', 'queued')
               AND a.runner_kind  = ANY(@kinds)
               AND b.is_enabled   = true
               AND b.is_deleted   = false
               AND b.running_run_key IS NULL
             ORDER BY a.priority DESC, a.requested_at
             FOR UPDATE OF a SKIP LOCKED
             LIMIT @capacity
            """, new { kinds = kinds.ToArray(), capacity }, tx)).ToList();

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

            var runKey = await db.ExecuteScalarAsync<long>("""
                INSERT INTO projmng.ai_task_run
                     ( task_key, seq, run_status, lease_expires_at, token_hash,
                       started_at, instruction )
                SELECT @TaskKey,
                       COALESCE(MAX(seq), 0) + 1,
                       'preparing',
                       now() + make_interval(secs => @lease),
                       @hash,
                       now(),
                       @Contents
                  FROM projmng.ai_task_run WHERE task_key = @TaskKey
                RETURNING run_key
                """, new
            {
                task.TaskKey,
                lease = _leaseSeconds,
                hash = Hash(token),
                task.Contents,
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
                Instruction = task.Contents,
                RunnerKind = task.RunnerKind,
                TimeoutMinutes = task.TimeoutMinutes,
                AutoPush = task.AutoPush,
                TargetRef = task.TargetRef,
                Target = target,
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

        // 작업 쪽 요약. **요청여부를 되돌린다** — 한 번 시킨 것이 끝났으므로
        // 그대로 두면 감시자가 같은 건을 또 집는다.
        await db.ExecuteAsync("""
            UPDATE projmng.ai_task t
               SET task_status    = @status,
                   request_flag   = 'none',
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
            runKey, status, done.ExitCode, done.Error,
            done.PushedCommit, done.PreviousTag, done.WorkspacePath, done.Branch,
        }, tx);

        // 대상 잠금을 푼다. **이것을 빠뜨리면 그 대상이 영영 막힌다.**
        await db.ExecuteAsync("""
            UPDATE projmng.ai_target SET running_run_key = NULL WHERE running_run_key = @runKey
            """, new { runKey }, tx);

        tx.Commit();

        logger.LogInformation("실행 {RunKey} 가 {Status} 로 끝났습니다 (exit {Exit}).",
            runKey, status, done.ExitCode);

        // 메일은 **기다리지 않는다.** 실행기의 완료 보고가 메일 전송 시간만큼
        // 늦어질 이유가 없다 — 그 사이 실행 슬롯이 묶인다.
        _ = notifier.SendAsync(runKey);

        return true;
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
