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

            // **주소와 아이디를 구분해서 보낸다.**
            //
            // 이 DB(projmng)에는 사람의 메일 주소가 없다 — 계정은 scom 에 있고
            // 그쪽은 알림 서비스의 DB 다. 그래서 「요청한 사람에게」는 아이디를
            // toUser 로 넘겨 저쪽에서 풀게 한다.
            //
            // 예전에는 아이디(cre_id)를 그대로 to 에 실었다. 주소 꼴이 아니라서
            // 알림 서비스가 걸러 버렸고 **「받는 사람」을 비워 둔 건은 한 통도
            // 나가지 않았다** — notify_error 에 HTTP 400 만 쌓였다.
            var to = row.NotifyTo?.Trim();
            var toUser = string.IsNullOrWhiteSpace(to) ? row.CreId?.Trim() : null;

            if (string.IsNullOrWhiteSpace(to) && string.IsNullOrWhiteSpace(toUser))
            {
                await MarkAsync(db, runKey, "받는 사람을 알 수 없습니다.");
                return;
            }

            // 사람이 적은 값이 주소가 아니면 **여기서 말한다.** 저쪽까지 갔다
            // 오면 「받는 사람이 없습니다」가 되어 어느 칸이 틀렸는지가 흐려진다.
            if (!string.IsNullOrWhiteSpace(to)
                && to.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Any(one => !System.Net.Mail.MailAddress.TryCreate(one, out _)))
            {
                await MarkAsync(db, runKey,
                    $"「받는 사람」이 메일 주소가 아닙니다: {to} — 비워 두면 요청한 사람에게 갑니다.");
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
                    toUser,
                    subject = $"[AI 작업] {row.Title} — {StatusText(row.TaskStatus)}",
                    body = Body(row),
                    // 저쪽 DTO 의 속성 이름은 `Html` 이다. `isHtml` 로 적으면
                    // 붙지 않고 조용히 기본값이 쓰인다.
                    html = false,
                }),
            };

            // 서비스 간 직접 호출이라 자기 이름을 적어 보낸다
            // (SiteServer 가 SITE_INQUIRY 로 하는 것과 같다).
            req.Headers.Add("X-User-Id", "AI_TASK");

            using var res = await client.SendAsync(req, ct);

            if (res.IsSuccessStatusCode)
            {
                logger.LogInformation("작업 {TaskKey} 결과를 {To} 에게 보냈습니다.", row.TaskKey, to ?? toUser);
                await MarkAsync(db, runKey, null);
            }
            else
            {
                // **본문까지 읽어 남긴다.** 상태 번호만 적어 두면 화면이
                // 「HTTP 400」만 말하게 되고, 정작 무엇이 틀렸는지는 알림
                // 서비스 로그를 봐야 알 수 있다 — 실제로 그렇게 헤맸다.
                var why = await res.Content.ReadAsStringAsync(ct);

                await MarkAsync(db, runKey,
                    $"메일 서비스가 HTTP {(int)res.StatusCode} 로 답했습니다. {Reason(why)}".Trim());
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

    /// <summary>
    /// 실패 응답에서 사람이 읽을 한 줄만 꺼낸다. 못 꺼내면 빈 문자열이다 —
    /// <b>JSON 덩어리를 그대로 화면에 올리지 않는다.</b>
    /// </summary>
    private static string Reason(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("message", out var m)
                && m.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // JSON 이 아니면 아래에서 앞부분만 자른다.
        }

        return body.Trim()[..Math.Min(body.Trim().Length, 200)];
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
