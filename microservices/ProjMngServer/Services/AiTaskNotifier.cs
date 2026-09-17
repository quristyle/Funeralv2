using System.Net.Http.Json;
using System.Text;

using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 끝난 작업을 메일로 알린다 (설계 8-2).
/// </summary>
/// <remarks>
/// <para>
/// <b>실행기가 아니라 서버가 보낸다.</b> 실행기가 보내면 작업은 끝났는데
/// 메일 때문에 실행 슬롯이 묶이고, 메일 실패가 실행 실패처럼 보인다 —
/// 그 둘은 다른 일이다.
/// </para>
/// <para>
/// <b>큐가 아니라 직발송(<c>/emails/send</c>)을 쓴다.</b> 큐 쪽
/// (<c>/notification/email</c>)은 결과를 「넣었다」까지만 알 수 있고, 무엇보다
/// 우리가 피하기로 한 <c>run_script</c> 큐를 다시 탄다(설계 6.2).
/// 「결과를 메일로 받겠다」는 기능에서 보내졌는지 모르면 절반만 있는 것이다.
/// </para>
/// <para>
/// <b>못 보내도 작업 상태를 바꾸지 않는다.</b> 사유만 남기고 화면이 그것을
/// 말한다 — 배포 도구가 정한 규칙과 같다(보고에 실패해도 배포는 계속한다).
/// </para>
/// </remarks>
public sealed class AiTaskNotifier(
    IConfiguration configuration, IHttpClientFactory http, ILogger<AiTaskNotifier> logger)
{
    private readonly string? _connectionString = configuration.GetConnectionString("jsini");

    /// <summary>
    /// 알림 서비스 주소. 같은 장비 안이라 루프백이다.
    /// </summary>
    private readonly string _notifyUrl =
        configuration["AiTasks:NotifyUrl"] is { Length: > 0 } u ? u : "http://127.0.0.1:5460";

    /// <summary>화면에서 그 건을 열 수 있는 주소. 메일 끝에 붙인다.</summary>
    private readonly string? _portalUrl = configuration["AiTasks:PortalUrl"];

    /// <summary>
    /// 이 실행의 결과를 메일로 보낸다. <b>보낼 이유가 없으면 조용히 끝낸다.</b>
    /// </summary>
    public async Task SendAsync(long runKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            var row = await db.QuerySingleOrDefaultAsync<MailRow>("""
                SELECT t.task_key      AS TaskKey,
                       t.title         AS Title,
                       t.notify_email  AS NotifyEmail,
                       t.notify_to     AS NotifyTo,
                       t.notify_when   AS NotifyWhen,
                       t.task_status   AS TaskStatus,
                       t.duration_ms   AS DurationMs,
                       t.pushed_commit AS PushedCommit,
                       t.cre_id        AS CreId,
                       b.target_nm     AS TargetNm,
                       r.seq           AS Seq,
                       r.exit_code     AS ExitCode,
                       r.result_text   AS ResultText,
                       r.diff_stat     AS DiffStat,
                       r.error_summary AS ErrorSummary,
                       r.git_branch    AS GitBranch
                  FROM projmng.ai_task_run r
                  JOIN projmng.ai_task t   ON t.task_key = r.task_key
                  LEFT JOIN projmng.ai_target b ON b.target_key = t.target_key
                 WHERE r.run_key = @runKey
                """, new { runKey });

            if (row is null || !row.NotifyEmail)
            {
                return;
            }

            // 「언제 보내나」를 본다. 실패만 받겠다고 한 사람에게 성공 메일을
            // 보내면 그 옵션이 있으나 마나다.
            var ok = row.TaskStatus == "succeeded";

            if ((row.NotifyWhen == "on_success" && !ok)
                || (row.NotifyWhen == "on_failure" && ok))
            {
                return;
            }

            var to = string.IsNullOrWhiteSpace(row.NotifyTo) ? row.CreId : row.NotifyTo;

            if (string.IsNullOrWhiteSpace(to))
            {
                await MarkAsync(db, runKey, "받는 사람을 알 수 없습니다.");
                return;
            }

            var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"{_notifyUrl.TrimEnd('/')}/emails/send")
            {
                Content = JsonContent.Create(new
                {
                    to,
                    subject = $"[AI 작업] {row.Title} — {StatusText(row.TaskStatus)}",
                    body = Body(row),
                    isHtml = false,
                }),
            };

            // 서비스 간 직접 호출이라 자기 이름을 적어 보낸다
            // (SiteServer 가 SITE_INQUIRY 로 하는 것과 같다).
            req.Headers.Add("X-User-Id", "AI_TASK");

            using var res = await client.SendAsync(req, ct);

            if (res.IsSuccessStatusCode)
            {
                logger.LogInformation("작업 {TaskKey} 결과를 {To} 에게 보냈습니다.", row.TaskKey, to);
                await MarkAsync(db, runKey, null);
            }
            else
            {
                await MarkAsync(db, runKey, $"메일 서비스가 HTTP {(int)res.StatusCode} 로 답했습니다.");
            }
        }
        catch (Exception ex)
        {
            // **작업 상태는 건드리지 않는다.** 메일이 안 갔다고 성공한 일이
            // 실패가 되면 안 된다.
            logger.LogWarning(ex, "결과 메일을 보내지 못했습니다 (run {RunKey}).", runKey);

            try
            {
                await using var db = new NpgsqlConnection(_connectionString);
                await MarkAsync(db, runKey, ex.Message);
            }
            catch
            {
                // 여기서 또 실패하면 남길 곳이 없다. 로그로 끝낸다.
            }
        }
    }

    private static async Task MarkAsync(NpgsqlConnection db, long runKey, string? error)
        => await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET notify_error = @error
             WHERE task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
            """, new { runKey, error = error?[..Math.Min(error.Length, 480)] });

    /// <summary>
    /// 메일 한 통.
    /// </summary>
    /// <remarks>
    /// <b>로그 전문을 붙이지 않는다.</b> 수천 줄이 메일함에 쌓이고, 마스킹을
    /// 거친 것이라도 메일은 한 번 나가면 회수할 수 없다. 링크로 보낸다.
    /// </remarks>
    private string Body(MailRow r)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"작업 : {r.Title}");
        sb.AppendLine($"대상 : {r.TargetNm}");
        sb.AppendLine($"결과 : {StatusText(r.TaskStatus)} (exit {r.ExitCode}) · {r.Seq}차");

        if (r.DurationMs is { } ms and > 0)
        {
            sb.AppendLine($"시간 : {TimeSpan.FromMilliseconds(ms):h\\:mm\\:ss}");
        }

        // push 한 건은 **따로 적는다.** 같은 「완료」로 뭉개면 코드만 고친 건과
        // 운영이 바뀐 건을 구분할 수 없다(설계 9.3).
        if (!string.IsNullOrWhiteSpace(r.PushedCommit))
        {
            sb.AppendLine($"배포 : 운영에 올라갔습니다 — {r.PushedCommit}");
        }
        else if (!string.IsNullOrWhiteSpace(r.GitBranch))
        {
            sb.AppendLine($"브랜치 : {r.GitBranch} (아직 올리지 않았습니다)");
        }

        sb.AppendLine();
        sb.AppendLine(new string('-', 50));
        sb.AppendLine();

        // 결과문이 비어 있으면 **그 사실을 적는다.** 빈 메일을 보내면
        // 받는 사람이 메일 사고로 읽는다.
        sb.AppendLine(string.IsNullOrWhiteSpace(r.ResultText)
            ? "(AI 가 아무 말 없이 끝났습니다. 화면에서 로그를 보십시오.)"
            : r.ResultText);

        sb.AppendLine();
        sb.AppendLine(new string('-', 50));

        if (!string.IsNullOrWhiteSpace(r.DiffStat))
        {
            sb.AppendLine();
            sb.AppendLine("바뀐 파일");
            sb.AppendLine(r.DiffStat);
        }

        if (!string.IsNullOrWhiteSpace(r.ErrorSummary))
        {
            sb.AppendLine();
            sb.AppendLine($"오류 : {r.ErrorSummary}");
        }

        if (!string.IsNullOrWhiteSpace(_portalUrl))
        {
            sb.AppendLine();
            sb.AppendLine($"화면에서 보기: {_portalUrl.TrimEnd('/')}/projmng/ai/tasks");
        }

        return sb.ToString();
    }

    private static string StatusText(string? status) => status switch
    {
        "succeeded" => "완료",
        "failed" => "실패",
        "timeout" => "시간초과",
        "canceled" => "취소",
        "interrupted" => "중단",
        _ => status ?? string.Empty,
    };

    private sealed class MailRow
    {
        public long TaskKey { get; set; }
        public string? Title { get; set; }
        public bool NotifyEmail { get; set; }
        public string? NotifyTo { get; set; }
        public string? NotifyWhen { get; set; }
        public string? TaskStatus { get; set; }
        public long? DurationMs { get; set; }
        public string? PushedCommit { get; set; }
        public string? CreId { get; set; }
        public string? TargetNm { get; set; }
        public int Seq { get; set; }
        public int? ExitCode { get; set; }
        public string? ResultText { get; set; }
        public string? DiffStat { get; set; }
        public string? ErrorSummary { get; set; }
        public string? GitBranch { get; set; }
    }
}
