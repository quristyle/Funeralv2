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
                       r.git_branch    AS GitBranch,
                       r.instruction   AS Instruction
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
    /// 메일 한 통. <b>맨 위 요약만 읽어도 다 알 수 있게</b> 짠다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 메일은 대개 휴대폰에서 열린다.</b> 「빠른 지시」 화면의 존재
    /// 이유가 그것이고, 그 화면에서 보낸 건은 답을 오직 메일로만 본다.
    /// 그런데 아래로 스크롤해야 알 수 있는 메일은 사실상 안 읽힌다 —
    /// 제목과 첫 화면에서 끝나야 한다.
    /// </para>
    /// <para>
    /// 그래서 <b>요약이 먼저고 전문이 뒤</b>다. 요약에는 다섯만 담는다.
    /// </para>
    /// <list type="number">
    ///   <item><description><b>어떻게 됐나</b> — 완료·실패와 대상·시간</description></item>
    ///   <item><description><b>무엇을 시켰나</b> — 지시문 앞부분.
    ///   <b>이것이 한동안 빠져 있었다</b>: 답만 있고 물음이 없으면, 며칠 뒤에
    ///   열어 본 사람은 이 메일이 무엇에 대한 것인지 알 수 없다</description></item>
    ///   <item><description><b>무엇을 했다나</b> — AI 의 답 앞부분</description></item>
    ///   <item><description><b>무엇이 바뀌었나</b> — 파일 수와 증감</description></item>
    ///   <item><description><b>내가 할 일이 있나</b> — 배포됐다 · 커밋만 있다 ·
    ///   사람이 봐야 한다</description></item>
    /// </list>
    /// <para>
    /// <b>로그 전문은 붙이지 않는다.</b> 수천 줄이 메일함에 쌓이고, 마스킹을
    /// 거친 것이라도 메일은 한 번 나가면 회수할 수 없다. 링크로 보낸다.
    /// </para>
    /// </remarks>
    private string Body(MailRow r)
    {
        var sb = new StringBuilder();

        // ── 요약 ────────────────────────────────────────────
        var bar = new string('═', 46);

        sb.AppendLine(bar);
        sb.AppendLine($"  {Headline(r)}");
        sb.AppendLine(bar);
        sb.AppendLine();

        sb.AppendLine($"■ 결과    {StatusText(r.TaskStatus)} · {r.Seq}차{Took(r)}");
        sb.AppendLine($"■ 대상    {r.TargetNm}");

        // **무엇을 시켰나.** 답만 있고 물음이 없으면 며칠 뒤의 나는
        // 이 메일이 무엇에 대한 것인지 알 수 없다.
        sb.AppendLine($"■ 시킨 것 {Gist(r.Instruction, lines: 3) ?? "(적힌 것이 없습니다)"}");

        sb.AppendLine($"■ AI 의 답 {Gist(r.ResultText, lines: 5) ?? "(아무 말 없이 끝났습니다)"}");
        sb.AppendLine($"■ 바뀐 것 {ChangeGist(r)}");
        sb.AppendLine($"■ 할 일   {NextStep(r)}");

        sb.AppendLine();
        sb.AppendLine(new string('-', 50));
        sb.AppendLine("아래는 전문입니다.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(r.Instruction))
        {
            sb.AppendLine("[시킨 것]");
            sb.AppendLine();
            sb.AppendLine(SecretMask.Apply(r.Instruction));
            sb.AppendLine();
            sb.AppendLine(new string('-', 50));
            sb.AppendLine();
        }

        sb.AppendLine("[AI 의 답]");
        sb.AppendLine();

        // 결과문이 비어 있으면 **그 사실을 적는다.** 빈 메일을 보내면
        // 받는 사람이 메일 사고로 읽는다.
        sb.AppendLine(string.IsNullOrWhiteSpace(r.ResultText)
            ? "(AI 가 아무 말 없이 끝났습니다. 화면에서 로그를 보십시오.)"
            : SecretMask.Apply(r.ResultText));

        sb.AppendLine();
        sb.AppendLine(new string('-', 50));

        // push 한 건은 **따로 적는다.** 같은 「완료」로 뭉개면 코드만 고친 건과
        // 운영이 바뀐 건을 구분할 수 없다(설계 9.3).
        if (!string.IsNullOrWhiteSpace(r.PushedCommit))
        {
            sb.AppendLine();
            sb.AppendLine($"배포 : 운영에 올라갔습니다 — {r.PushedCommit}");
        }
        else if (!string.IsNullOrWhiteSpace(r.GitBranch))
        {
            sb.AppendLine();
            sb.AppendLine($"브랜치 : {r.GitBranch} (아직 올리지 않았습니다)");
        }

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

    /// <summary>
    /// 요약의 첫 줄. <b>이 한 줄만 읽어도 「무엇이 어떻게 됐는지」가 나와야 한다.</b>
    /// </summary>
    private static string Headline(MailRow r)
    {
        var what = Trim(r.Title, 52);

        return r.TaskStatus switch
        {
            "succeeded" when !string.IsNullOrWhiteSpace(r.PushedCommit)
                => $"완료 · 운영에 올라감 — {what}",
            "succeeded" when !string.IsNullOrWhiteSpace(r.DiffStat)
                => $"완료 · 고친 것 있음 — {what}",
            "succeeded" => $"완료 — {what}",
            "failed" => $"실패 — {what}",
            "timeout" => $"시간초과 — {what}",
            "canceled" => $"취소됨 — {what}",
            "interrupted" => $"중단됨 — {what}",
            _ => $"{StatusText(r.TaskStatus)} — {what}",
        };
    }

    private static string Took(MailRow r) =>
        r.DurationMs is { } ms and > 0
            ? $" · {TimeSpan.FromMilliseconds(ms):h\\:mm\\:ss}"
            : string.Empty;

    /// <summary>
    /// 긴 글에서 <b>앞쪽 몇 줄만</b> 뽑아 한 덩이로 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AI 가 요약을 다시 써 주게 하지 않는다 — 그러면 <b>메일 한 통마다 모델을
    /// 한 번 더 부르는 일</b>이 되고, 그 호출이 실패하면 메일이 안 나간다.
    /// 결과문의 앞부분은 대개 이미 결론이라 그것으로 충분하다.
    /// </para>
    /// <para>
    /// 표 구분선(<c>|---|</c>)이나 밑줄(<c>====</c>) 같은 <b>내용 없는 줄은
    /// 건너뛴다.</b> 그것부터 세면 요약 다섯 줄이 장식으로 다 찬다.
    /// </para>
    /// </remarks>
    private static string? Gist(string? text, int lines)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var picked = SecretMask.Apply(text)!
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !Decoration(l))
            .Select(Plain)
            .Where(l => l.Length > 0)
            .Take(lines)
            .Select(l => Trim(l, 110))
            .ToList();

        if (picked.Count == 0)
        {
            return null;
        }

        // 둘째 줄부터는 라벨 폭만큼 들여 쓴다. 안 그러면 다음 항목과 섞여
        // **무엇이 답이고 무엇이 다음 라벨인지** 구분이 안 된다.
        return string.Join("\n            ", picked);
    }

    /// <summary>
    /// 마크다운 머리표를 뗀다. <b>요약에서는 <c>##</c> 나 <c>-</c> 가 뜻을 더하지
    /// 않는다</b> — 글자 수만 먹고, 라벨 뒤에 붙으면 오히려 어수선하다.
    /// </summary>
    private static string Plain(string line) =>
        line.TrimStart('#', '>', '*', '-', ' ').Trim();

    /// <summary>내용 없는 꾸밈 줄인가 — <c>---</c> · <c>===</c> · <c>|---|</c> 따위.</summary>
    private static bool Decoration(string line) =>
        line.All(c => c is '-' or '=' or '|' or ':' or '_' or '*' or '#' or ' ' or '─' or '═');

    /// <summary>
    /// 무엇이 바뀌었나 한 줄. <c>git diff --stat</c> 의 <b>마지막 요약 줄</b>이
    /// 이미 그 답이라 그것을 쓴다.
    /// </summary>
    private static string ChangeGist(MailRow r)
    {
        if (string.IsNullOrWhiteSpace(r.DiffStat))
        {
            return "없음 (파일을 고치지 않았습니다)";
        }

        var rows = r.DiffStat
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // 마지막 줄이 "3 files changed, 120 insertions(+), 4 deletions(-)" 다.
        var tail = rows.LastOrDefault(l => l.Contains("changed", StringComparison.OrdinalIgnoreCase));

        // 요약 줄이 잘려 없을 수도 있다(480자 상한). 그때는 줄 수로 센다.
        return tail is not null
            ? Trim(tail, 110)
            : $"파일 {rows.Count}개";
    }

    /// <summary>
    /// <b>내가 할 일이 있나.</b> 요약에서 사람이 제일 알고 싶은 칸이다 —
    /// 「읽고 끝」인지 「가서 봐야 하는지」가 여기서 갈린다.
    /// </summary>
    private string NextStep(MailRow r)
    {
        if (r.TaskStatus != "succeeded")
        {
            return string.IsNullOrWhiteSpace(r.ErrorSummary)
                ? "화면에서 로그를 보십시오."
                : $"화면에서 로그를 보십시오 — {Trim(r.ErrorSummary, 90)}";
        }

        if (!string.IsNullOrWhiteSpace(r.PushedCommit))
        {
            return "없습니다. 이미 올라갔고 배포가 돕니다.";
        }

        if (!string.IsNullOrWhiteSpace(r.DiffStat))
        {
            return string.IsNullOrWhiteSpace(r.GitBranch)
                ? "고친 것이 작업 자리에 있습니다. 사람이 확인해 올려야 합니다."
                : $"브랜치 {r.GitBranch} 에 커밋만 있습니다. 사람이 확인해 올려야 합니다.";
        }

        return "없습니다. 읽어 보기만 하면 됩니다.";
    }

    private static string Trim(string? text, int max)
    {
        var t = (text ?? string.Empty).Trim();

        return t.Length <= max ? t : t[..max] + "…";
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

        /// <summary>그때 실제로 준 지시문. <b>요약에 「무엇을 시켰나」를 적으려면 필요하다.</b></summary>
        public string? Instruction { get; set; }
    }
}
