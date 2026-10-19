using System.Net.Http.Json;
using System.Text;
using JSini.Shared.DTOs;

using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 끝난 작업을 메일로 알린다. 실행기가 아니라 서버가 보낸다 — 실행 슬롯이 메일에 묶이지 않게.
/// 못 보내도 작업 상태는 바꾸지 않고 사유만 남긴다.
/// </summary>
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
    /// 결과 앱푸시를 시킨 사람 말고도 받는 역할. 비우면 시킨 사람에게만 간다.
    /// 역할을 사람으로 푸는 것은 알림 서비스다 — 이 DB 에는 역할표가 없다.
    /// </summary>
    private readonly string[] _resultRoles =
        configuration.GetSection("AiTasks:ResultNotifyRoles").Get<string[]>()
        ?? ["SYSTEM_ADMINISTRATOR"];

    /// <summary>
    /// 그 건 하나를 펴 놓는 화면의 상대 주소. 앱알림과 메일이 같은 한 줄을 쓴다.
    /// 질의 문자열이 아니라 경로에 번호를 싣는다 — 포털 탭이 질의를 떼고 탭을 가르기 때문.
    /// </summary>
    private static string TaskUrl(long taskKey) => $"/projmng/ai/task/{taskKey}";

    /// <summary>
    /// 기록에 적을 받는 쪽 한 줄. 역할로 간 몫도 보여야 「왜 안 왔나」를 되짚을 수 있다.
    /// </summary>
    private string Who(Target target)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(target.ToUser))
        {
            parts.Add(target.ToUser!);
        }

        if (_resultRoles.Length > 0)
        {
            parts.Add($"역할 {string.Join("·", _resultRoles)}");
        }

        return parts.Count == 0 ? "(없음)" : string.Join(" + ", parts);
    }

    /// <summary>
    /// 이 실행의 앱푸시를 보낸다. 알림 세 걸음 중 첫째.
    /// 요약·제목은 읽기만 한다 — 기다리는 것은 부르는 쪽이다. 여기서 모델을 부르면 요약 없는 알림이 되살아난다.
    /// </summary>
    public async Task SendPushAsync(long runKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            // 이 실행의 사유를 여기서 비운다 — 푸시가 첫 걸음이고, 뒤따르는 메일은 덧붙인다.
            await MarkAsync(db, runKey, null, reset: true);

            var target = await LoadAsync(db, runKey, wantEmail: false, ct);

            if (target is null)
            {
                return;
            }

            var row = target.Row;

            // 주소만 적힌 「받는 사람」은 푸시로 보낼 곳이 없다. 지켜보는 역할이 있으면 그쪽으로만 간다.
            var owners = string.IsNullOrWhiteSpace(target.ToUser)
                ? Array.Empty<object>()
                : new object[] { new { ownerType = "jsini", ownerKey = target.ToUser } };

            if (owners.Length == 0 && _resultRoles.Length == 0)
            {
                return;
            }

            var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"{_notifyUrl.TrimEnd('/')}/notifications/push")
            {
                Content = JsonContent.Create(new
                {
                    owners,

                    // 시킨 사람 말고 지켜보는 역할도 받는다. 겹치면 저쪽이 접으므로 여기서 덜지 않는다.
                    roles = _resultRoles,

                    message = new
                    {
                        // 제목 줄은 「어느 건이 어떻게 됐나」다. 상태와 작업 제목을 싣고, 무엇을 했는지는 본문에 쓴다.
                        title = $"[{StatusText(row.TaskStatus)}] {Trim(row.Title, 60)}",

                        // 본문에 처리 요약이 실린다 (PushBody).
                        body = PushBody(row),

                        // 그 건 하나를 펴 놓는 주소. 메일 단추도 같은 주소를 쓴다.
                        url = TaskUrl(row.TaskKey),

                        // 아이콘은 받는 사람이 아니라 시킨 사람(CreId)의 얼굴이다. 사진은 알림 서비스가 아이디로 푼다 (이 DB 에는 사진도 메일 주소도 없다).
                        iconOwnerKey = row.CreId?.Trim(),

                        // 두 시간 지나면 배달하지 않는다 — 끝난 작업은 화면과 내 알림함에 남아 잃는 것이 없다. 수명 설정은 docs/push-delivery.md 참고.
                        ttlSeconds = 7200,

                        // 알림구분(공통코드 NOTI_CATEGORY). 요청 알림과 같은 갈래다.
                        category = PushCategories.AiTask
                    }
                }),
            };

            req.Headers.Add("X-User-Id", "AI_TASK");

            using var res = await client.SendAsync(req, ct);

            if (res.IsSuccessStatusCode)
            {
                // 「보냈다」와 「보낼 곳이 없었다」는 다르다. 2xx 만 보고 넘기면 후자가 사라진다 (PushOutcome).
                var body = await res.Content.ReadAsStringAsync(ct);
                var notSent = PushOutcome.NotSent(res.StatusCode, body);

                if (notSent is null)
                {
                    logger.LogInformation(
                        "작업 {TaskKey} 결과를 {To} 에게 PWA로 보냈습니다.", row.TaskKey, Who(target));
                }
                else
                {
                    await MarkAsync(db, runKey, $"PWA 미발송: {notSent}".Trim(), reset: false);
                    logger.LogWarning(
                        "작업 {TaskKey} 결과 PWA 알림이 {To} 에게 한 건도 가지 않았습니다: {Why}",
                        row.TaskKey, Who(target), notSent);
                }
            }
            else
            {
                var why = await res.Content.ReadAsStringAsync(ct);
                await MarkAsync(db, runKey, $"PWA 실패 (HTTP {(int)res.StatusCode}): {Reason(why)}".Trim(), reset: false);
            }
        }
        catch (Exception ex)
        {
            // 작업 상태는 건드리지 않는다. 알림 실패로 성공한 일이 실패가 되지 않게.
            logger.LogWarning(ex, "결과 앱푸시를 보내지 못했습니다 (run {RunKey}).", runKey);

            // 덮어쓴다. 앞 실행의 사유가 남으면 이번 사유가 그 뒤에 붙어 섞인다.
            await TryMarkAsync(runKey, ex.Message, reset: true);
        }
    }

    /// <summary>
    /// 이 실행의 결과를 메일로 보낸다. 보낼 이유가 없으면 조용히 끝낸다.
    /// 요약이 없으면 deferUntilSummary 일 때 미루고, 보내기 전에 mail_sent_at 을 먼저 찍어 한 통만 나가게 한다.
    /// </summary>
    /// <param name="deferUntilSummary">요약이 없을 때 미룰지. 완료 처리는 켜고, 따라잡기의 마지막 호출은 끈다.</param>
    public async Task SendMailAsync(
        long runKey, bool deferUntilSummary = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            await using var db = new NpgsqlConnection(_connectionString);

            var target = await LoadAsync(db, runKey, wantEmail: true, ct);

            if (target is null)
            {
                // 받기가 꺼졌거나 받을 사람이 없어도 도장은 찍는다. 안 찍으면 따라잡기가 이 건을 끝없이 다시 집는다.
                await StampMailAsync(db, runKey, ct);
                return;
            }

            var row = target.Row;
            var to = target.To;

            // 주소가 아닌 값은 여기서 말한다. 저쪽까지 갔다 오면 어느 칸이 틀렸는지 흐려진다.
            if (!string.IsNullOrWhiteSpace(to)
                && to.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Any(one => !System.Net.Mail.MailAddress.TryCreate(one, out _)))
            {
                await MarkAsync(db, runKey,
                    $"「받는 사람」이 메일 주소가 아닙니다: {to} — 비워 두면 요청한 사람에게 갑니다.",
                    reset: false);

                // 칸을 고치기 전에는 몇 번을 보내도 같은 답이다. 도장을 찍는다.
                await StampMailAsync(db, runKey, ct);
                return;
            }

            // 처리 요약은 여기서 만들지 않는다. 완료 처리가 먼저 적어 두고, 여기서는 적힌 것을 되읽기만 한다.
            var summary = AiResultSummary.Parse(row.SummaryText);

            // 요약이 아직 없으면 미룬다. 애초에 정리할 글이 없는 건(AI 가 말없이 끝난 건)은 그냥 보낸다.
            if (summary is null && deferUntilSummary && !string.IsNullOrWhiteSpace(row.ResultText))
            {
                logger.LogInformation(
                    "작업 {TaskKey} 결과 메일을 미룹니다 — 처리 요약이 아직 없습니다 (run {RunKey}).",
                    row.TaskKey, runKey);
                return;
            }

            // **자리를 먼저 잡는다.** 못 잡으면 다른 길이 이미 보낸 것이다.
            if (!await StampMailAsync(db, runKey, ct))
            {
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
                    toUser = target.ToUser,
                    subject = $"[AI 작업] {row.Title} — {StatusText(row.TaskStatus)}",
                    body = Body(row, summary),
                    // 저쪽 DTO 속성은 Html 이다. isHtml 로 적으면 조용히 기본값이 쓰여 태그가 그대로 보인다.
                    html = true,

                    // 알림관리가 이 글자로 가른다. 까닭은 NotificationServer 의 PushSender.ExpandOwnersAsync 머리말에 있다.
                    eventCode = "AI_TASK",
                }),
            };

            // 서비스 간 직접 호출이라 자기 이름을 적어 보낸다
            req.Headers.Add("X-User-Id", "AI_TASK");

            using var res = await client.SendAsync(req, ct);

            if (res.IsSuccessStatusCode)
            {
                logger.LogInformation("작업 {TaskKey} 결과를 {To} 에게 메일로 보냈습니다.", row.TaskKey, to ?? target.ToUser);
            }
            else
            {
                var why = await res.Content.ReadAsStringAsync(ct);
                await MarkAsync(db, runKey,
                    $"메일 실패 (HTTP {(int)res.StatusCode}): {Reason(why)}".Trim(), reset: false);
            }
        }
        catch (Exception ex)
        {
            // 작업 상태는 건드리지 않는다. 메일 실패로 성공한 일이 실패가 되지 않게.
            logger.LogWarning(ex, "결과 메일을 보내지 못했습니다 (run {RunKey}).", runKey);

            // 메일은 뒤 걸음이라 앞이 남긴 사유를 지우지 않는다.
            await TryMarkAsync(runKey, ex.Message, reset: false);
        }
    }

    /// <summary>알림 한 통을 만들 때 필요한 것 — 읽어 온 줄과 받는 곳.</summary>
    /// <param name="Row">실행·작업을 한 줄로 편 것.</param>
    /// <param name="To">사람이 적은 메일 주소. 비어 있으면 <paramref name="ToUser"/> 가 찬다.</param>
    /// <param name="ToUser">요청한 사람의 포털 아이디. 주소를 적었으면 <c>null</c>.</param>
    private sealed record Target(MailRow Row, string? To, string? ToUser);

    /// <summary>
    /// 보낼 줄을 읽고 보낼 이유가 있는지 가린다. 없으면 null.
    /// 푸시·메일 두 길이 이 관문을 함께 쓴다 — 따로 적으면 한쪽만 고쳐져 어긋난다.
    /// </summary>
    /// <param name="wantEmail">메일 길인가. 아니면 앱푸시 길이다.</param>
    private async Task<Target?> LoadAsync(
        NpgsqlConnection db, long runKey, bool wantEmail, CancellationToken ct)
    {
        var row = await db.QuerySingleOrDefaultAsync<MailRow>(new CommandDefinition("""
            SELECT t.task_key      AS TaskKey,
                   t.title         AS Title,
                   t.notify_email  AS NotifyEmail,
                   t.notify_pwa    AS NotifyPwa,
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
                   r.changed_files AS ChangedFiles,
                   r.error_summary AS ErrorSummary,
                   r.git_branch    AS GitBranch,
                   r.instruction   AS Instruction,
                   r.summary_text  AS SummaryText
              FROM projmng.ai_task_run r
              JOIN projmng.ai_task t   ON t.task_key = r.task_key
              LEFT JOIN projmng.ai_target b ON b.target_key = t.target_key
             WHERE r.run_key = @runKey
            """, new { runKey }, cancellationToken: ct));

        if (row is null)
        {
            return null;
        }

        if (!(wantEmail ? row.NotifyEmail : row.NotifyPwa))
        {
            return null;
        }

        // 실패만 받겠다고 한 사람에게 성공 메일을 보내지 않는다.
        var ok = row.TaskStatus == "succeeded";

        if ((row.NotifyWhen == "on_success" && !ok)
            || (row.NotifyWhen == "on_failure" && ok))
        {
            return null;
        }

        // 주소 칸에 아이디를 실으면 알림 서비스가 걸러 한 통도 안 나간다. 아이디는 toUser 로 넘겨 저쪽이 푼다.
        var to = row.NotifyTo?.Trim();
        var toUser = string.IsNullOrWhiteSpace(to) ? row.CreId?.Trim() : null;

        if (string.IsNullOrWhiteSpace(to) && string.IsNullOrWhiteSpace(toUser))
        {
            await MarkAsync(db, runKey, "받는 사람을 알 수 없습니다.", reset: false);
            return null;
        }

        return new Target(row, to, toUser);
    }

    /// <summary>
    /// 이 실행의 메일 자리를 잡는다 — mail_sent_at 을 지금으로 찍고, 찍은 것이 나인지 돌려준다.
    /// WHERE mail_sent_at IS NULL 이 요점이다. 보낸 뒤에 찍으면 두 길이 겹쳐 같은 메일이 두 통 나간다.
    /// </summary>
    private static async Task<bool> StampMailAsync(
        NpgsqlConnection db, long runKey, CancellationToken ct)
    {
        var claimed = await db.ExecuteAsync(new CommandDefinition("""
            UPDATE projmng.ai_task_run
               SET mail_sent_at = now()
             WHERE run_key = @runKey
               AND mail_sent_at IS NULL
            """, new { runKey }, cancellationToken: ct));

        return claimed > 0;
    }

    /// <summary>
    /// 사유를 남겨 보되 못 남겨도 그만인 자리. 이미 예외 처리 중이라 여기서 또 던지면 남길 곳이 없다.
    /// </summary>
    private async Task TryMarkAsync(long runKey, string? error, bool reset)
    {
        try
        {
            await using var db = new NpgsqlConnection(_connectionString);
            await MarkAsync(db, runKey, error, reset);
        }
        catch
        {
            // 로그로 끝낸다.
        }
    }

    /// <summary>
    /// 실패 응답에서 사람이 읽을 한 줄만 꺼낸다. 못 꺼내면 빈 문자열 — JSON 덩어리를 화면에 올리지 않는다.
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

    /// <summary>
    /// 이 실행의 알림 사유(ai_task.notify_error)를 적는다. 푸시가 먼저, 메일이 나중에 나가므로 뒤엣것이 덮으면 앞의 실패가 사라진다.
    /// 첫 걸음만 reset 으로 비우고 나머지는 ' / ' 로 잇는다.
    /// </summary>
    /// <param name="reset">
    /// 참이면 앞의 것을 지우고 이 값만 남긴다(새 실행의 첫 걸음).
    /// 거짓이면 뒤에 잇는다 — <paramref name="error"/> 가 비면 아무것도 안 한다.
    /// </param>
    private static async Task MarkAsync(
        NpgsqlConnection db, long runKey, string? error, bool reset)
    {
        var clipped = string.IsNullOrWhiteSpace(error)
            ? null
            : error[..Math.Min(error.Length, 480)];

        if (reset)
        {
            await db.ExecuteAsync("""
                UPDATE projmng.ai_task
                   SET notify_error = @error
                 WHERE task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
                """, new { runKey, error = clipped });
            return;
        }

        if (clipped is null)
        {
            // 잘 갔다는 것은 적지 않는다 — 앞 걸음이 남긴 사유를 지우게 된다.
            return;
        }

        // 이미 적힌 말은 다시 적지 않는다. 같은 문장이 두 걸음에서 나오면 화면에 두 번 뜬다.
        // LIKE 가 아니라 POSITION 이다 — 사유의 % 나 _ 가 패턴으로 읽히면 안 된다.
        await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET notify_error = CASE
                     WHEN POSITION(@error IN COALESCE(notify_error, '')) > 0 THEN notify_error
                     ELSE LEFT(CONCAT_WS(' / ', NULLIF(notify_error, ''), @error), 480)
                   END
             WHERE task_key = ( SELECT task_key FROM projmng.ai_task_run WHERE run_key = @runKey )
            """, new { runKey, error = clipped });
    }

    /// <summary>
    /// 메일 한 통. 맨 위 요약만 읽어도 다 알 수 있게 짠다 — 차례는 머리띠, 처리 요약, 할 일, 시킨 것·바뀐 것, 전문(화면과 같다).
    /// 스타일은 인라인과 표 바깥틀로 짠다(메일 앱이 style 블록과 flex 를 무시한다). 결과문·지시문은 반드시 HTML 이스케이프한다.
    /// </summary>
    internal string Body(MailRow r, AiResultSummary? summary)
    {
        var accent = Accent(r.TaskStatus);
        var tint = Tint(r.TaskStatus);
        var sb = new StringBuilder();

        // ── 바깥틀 · 머리 ───────────────────────────────────
        sb.Append($"""
            <!DOCTYPE html>
            <html lang="ko">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Esc(Headline(r))}</title>
            </head>
            <body style="margin:0;padding:0;background:#f1f3f5;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f1f3f5;">
            <tr><td align="center" style="padding:16px 10px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:640px;background:#ffffff;border:1px solid #e3e6ea;border-radius:10px;font-family:-apple-system,BlinkMacSystemFont,'Apple SD Gothic Neo','Malgun Gothic',sans-serif;color:#212529;">
            <tr><td style="background:{accent};padding:18px 20px;border-radius:9px 9px 0 0;">
              <div style="font-size:11px;letter-spacing:.06em;color:#ffffff;opacity:.85;">AI 작업 결과 보고</div>
              <div style="font-size:19px;line-height:1.35;font-weight:700;color:#ffffff;padding-top:5px;">{Esc(Headline(r))}</div>
              <div style="font-size:12.5px;color:#ffffff;opacity:.9;padding-top:7px;">{Esc(SubHead(r))}</div>
            </td></tr>
            """);

        // ── 처리 요약 — 머리띠 바로 아래 제 칸. 라벨 칸 안에 접히면 「요약이 빠졌다」로 읽힌다 ──
        sb.Append(SummaryCard(r, summary, accent, tint));

        // 「할 일」은 요약 바로 밑 — 사람이 가장 알고 싶은 물음이다. 바탕은 회색이다(바로 위 요약과 같은 색이면 한 덩이로 읽힌다).
        sb.Append($"""
            <tr><td style="padding:12px 20px 0 20px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f8f9fa;border:1px solid #e9ecef;border-radius:6px;">
                <tr><td style="padding:11px 13px;font-size:13.5px;line-height:1.55;">
                  <div style="font-size:11.5px;font-weight:700;color:#868e96;letter-spacing:.06em;">할 일</div>
                  <div style="padding-top:3px;font-weight:600;">{Esc(NextStep(r))}</div>
                </td></tr>
              </table>
            </td></tr>
            """);

        // 결과·대상·차수·시간은 머리띠가 이미 말하므로 여기서 다시 세우지 않는다.
        sb.Append("""
            <tr><td style="padding:18px 20px 0 20px;">
              <div style="font-size:11.5px;font-weight:700;color:#868e96;letter-spacing:.06em;padding-bottom:10px;">한눈에</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="font-size:13.5px;line-height:1.6;">
            """);

        // 무엇을 시켰나. 이어가기 건은 이번에 시킨 것만 뜬다(ThisTurn) — 앞에서 세 줄을 뜨면 지난 회차 머리말이 잡힌다.
        sb.Append(SummaryRow("시킨 것", Lines(Gist(ThisTurn(r.Instruction), lines: 3)) ?? Muted("적힌 것이 없습니다")));
        sb.Append(SummaryRow("바뀐 것", Esc(ChangeGist(r))));

        sb.Append("""
              </table>
            </td></tr>
            """);

        // ── 전문 ────────────────────────────────────────────
        sb.Append(Divider("전문"));

        if (!string.IsNullOrWhiteSpace(r.Instruction))
        {
            sb.Append(Block("시킨 것", SecretMask.Apply(r.Instruction)));
        }

        // 결과문이 비었으면 그 사실을 적는다. 빈 메일은 메일 사고로 읽힌다.
        //
        // 정리본을 위에 올렸으면 여기가 원문임을 밝힌다 — 같은 라벨이 두 번 나오면 어느 쪽이 고친 것인지 알 수 없다.
        var said = summary is null ? "AI 의 답" : "AI 의 답 (원문)";

        sb.Append(string.IsNullOrWhiteSpace(r.ResultText)
            ? Block(said, "(AI 가 아무 말 없이 끝났습니다. 화면에서 로그를 보십시오.)")
            : Block(said, SecretMask.Apply(r.ResultText)));

        // push 한 건은 따로 적는다 — 같은 완료로 뭉개면 코드만 고친 건과 운영이 바뀐 건을 구분할 수 없다.
        if (!string.IsNullOrWhiteSpace(r.PushedCommit))
        {
            sb.Append(Note("배포", $"""운영에 올라갔습니다 — <span style="font-family:ui-monospace,SFMono-Regular,Consolas,monospace;">{Esc(r.PushedCommit)}</span>"""));
        }
        else if (!string.IsNullOrWhiteSpace(r.GitBranch))
        {
            sb.Append(Note("브랜치", $"""<span style="font-family:ui-monospace,SFMono-Regular,Consolas,monospace;">{Esc(r.GitBranch)}</span> (아직 올리지 않았습니다)"""));
        }

        // 처리 파일이 있으면 그것을 적는다 — diff_stat 은 잘려서 목록 구실을 못 한다. 칸이 없던 옛 건만 옛 값을 쓴다.
        if (ChangedFileText(r.ChangedFiles) is { Length: > 0 } files)
        {
            sb.Append(Block("처리 파일", files));
        }
        else if (!string.IsNullOrWhiteSpace(r.DiffStat))
        {
            sb.Append(Block("바뀐 파일", r.DiffStat));
        }

        if (!string.IsNullOrWhiteSpace(r.ErrorSummary))
        {
            // 오류는 붉은 칸으로 낸다 — 같은 회색이면 실패 메일에서 사유를 못 찾는다.
            sb.Append($"""
                <tr><td style="padding:16px 20px 0 20px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#fff5f5;border:1px solid #ffc9c9;border-radius:6px;">
                    <tr><td style="padding:11px 13px;font-size:13px;line-height:1.6;color:#c92a2a;">
                      <div style="font-size:11.5px;font-weight:700;letter-spacing:.06em;">오류</div>
                      <div style="padding-top:3px;word-break:break-word;">{Esc(r.ErrorSummary)}</div>
                    </td></tr>
                  </table>
                </td></tr>
                """);
        }

        // ── 화면으로 가는 단추 ──────────────────────────────
        if (!string.IsNullOrWhiteSpace(_portalUrl))
        {
            var link = $"{_portalUrl.TrimEnd('/')}{TaskUrl(r.TaskKey)}";

            sb.Append($"""
                <tr><td align="center" style="padding:20px 20px 4px 20px;">
                  <a href="{Esc(link)}" style="display:inline-block;padding:11px 22px;background:{accent};color:#ffffff;font-size:13.5px;font-weight:600;text-decoration:none;border-radius:6px;">화면에서 보기</a>
                </td></tr>
                """);
        }

        // ── 꼬리 ────────────────────────────────────────────
        sb.Append("""
            <tr><td style="padding:16px 20px 20px 20px;">
              <div style="border-top:1px solid #e9ecef;padding-top:12px;font-size:11.5px;line-height:1.6;color:#adb5bd;">
                작업에서 「끝나면 메일로 받기」를 켜 두어 보내진 메일입니다.
                로그 전문은 길어서 붙이지 않습니다 — 화면에서 보십시오.
              </div>
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    /// <summary>머리의 둘째 줄 — 대상과 몇 차, 얼마나 걸렸나.</summary>
    private static string SubHead(MailRow r)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(r.TargetNm))
        {
            parts.Add(r.TargetNm!);
        }

        parts.Add($"{r.Seq}차");

        if (Took(r) is { Length: > 0 } took)
        {
            parts.Add(took.TrimStart(' ', '·').Trim());
        }

        return string.Join(" · ", parts);
    }

    /// <summary>요약 한 줄. 라벨 칸은 고정 폭이라 <b>값이 접혀도 칸이 남는다.</b></summary>
    private static string SummaryRow(string label, string valueHtml) => $"""
        <tr>
          <td valign="top" style="width:72px;padding:4px 12px 4px 0;color:#868e96;white-space:nowrap;">{label}</td>
          <td valign="top" style="padding:4px 0;word-break:break-word;">{valueHtml}</td>
        </tr>
        """;

    /// <summary>「전문」처럼 아래가 다른 것임을 알리는 가로선.</summary>
    private static string Divider(string label) => $"""
        <tr><td style="padding:20px 20px 0 20px;">
          <div style="border-top:1px solid #e9ecef;padding-top:14px;font-size:11.5px;font-weight:700;color:#868e96;letter-spacing:.06em;">{label}</div>
        </td></tr>
        """;

    /// <summary>
    /// 긴 글 한 덩이. 줄바꿈과 사이 띄움을 그대로 둔다 — AI 답의 표와 목록이 무너지면 읽을 수 없다.
    /// </summary>
    private static string Block(string title, string? text) => $"""
        <tr><td style="padding:12px 20px 0 20px;">
          <div style="font-size:12px;font-weight:600;color:#495057;padding-bottom:6px;">{title}</div>
          <div style="white-space:pre-wrap;word-break:break-word;font-family:ui-monospace,SFMono-Regular,Consolas,'D2Coding',monospace;font-size:12.5px;line-height:1.65;color:#343a40;background:#f8f9fa;border:1px solid #e9ecef;border-radius:6px;padding:12px 13px;">{Esc(text?.Trim())}</div>
        </td></tr>
        """;

    /// <summary>
    /// 처리 파일 목록을 메일에서 읽을 글자로 편다. 상태 글자는 한국어 한 마디로 풀어 앞에 붙인다.
    /// </summary>
    private static string ChangedFileText(string? changed)
    {
        if (string.IsNullOrWhiteSpace(changed))
        {
            return string.Empty;
        }

        var lines = changed
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var cut = line.IndexOf('\t');

                if (cut < 0)
                {
                    return line;
                }

                var mark = line[..cut];
                var path = line[(cut + 1)..].Trim();

                return mark == "…" ? $"… {path}" : $"[{ChangeWord(mark)}] {path}";
            })
            .ToList();

        return lines.Count == 0 ? string.Empty : string.Join('\n', lines);
    }

    /// <summary>
    /// git 의 상태 글자 한 자를 한국어로. 모르는 글자는 그대로 둔다.
    /// 뒤 글자는 결과문에서 뽑은 줄을 실제 파일에 대 본 확인 결과다. 없는 경로도 빼지 않는다.
    /// </summary>
    private static string ChangeWord(string mark)
    {
        var check = AiChangedFiles.CheckOf(mark);
        var head = check == AiChangedFileCheck.None ? mark : mark[..^1];

        var word = head switch
        {
            "A" => "추가",
            "M" => "수정",
            "D" => "삭제",
            "R" => "이름변경",
            "C" => "복사",
            "T" => "형식변경",
            "-" => "확인필요",
            var other => other,
        };

        return check switch
        {
            AiChangedFileCheck.Pending => $"{word}·추정",
            AiChangedFileCheck.Found => $"{word}·추정(파일 있음)",
            AiChangedFileCheck.Missing => $"{word}·추정(없는 경로)",
            AiChangedFileCheck.Ambiguous => $"{word}·추정(여러 곳)",
            _ => word,
        };
    }

    /// <summary>한 줄짜리 알림 칸 — 배포·브랜치처럼 짧은 사실.</summary>
    private static string Note(string label, string valueHtml) => $"""
        <tr><td style="padding:14px 20px 0 20px;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f8f9fa;border:1px solid #e9ecef;border-radius:6px;">
            <tr>
              <td valign="top" style="width:60px;padding:10px 0 10px 13px;font-size:12px;font-weight:600;color:#868e96;white-space:nowrap;">{label}</td>
              <td valign="top" style="padding:10px 13px 10px 8px;font-size:13px;line-height:1.55;word-break:break-word;">{valueHtml}</td>
            </tr>
          </table>
        </td></tr>
        """;

    /// <summary>
    /// 메일의 맨 앞 칸 「처리 요약」. 화면의 같은 칸과 차례·모양을 맞춘다.
    /// 라벨 칸 뒤에 두면 답이 곁줄로 접혀 「요약이 빠졌다」로 읽힌다. 글 크기로 차례를 매기고, 요약이 없으면 왜 없는지 적고 결과문 앞줄을 싣는다.
    /// </summary>
    private static string SummaryCard(MailRow r, AiResultSummary? summary, string accent, string tint)
    {
        var sb = new StringBuilder();

        sb.Append($"""
            <tr><td style="padding:18px 20px 0 20px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{tint};border:1px solid #e9ecef;border-left:4px solid {accent};border-radius:0 8px 8px 0;">
                <tr><td style="padding:14px 16px;">
                  <div style="font-size:11.5px;font-weight:700;color:{accent};letter-spacing:.06em;padding-bottom:9px;">처리 요약</div>
            """);

        if (summary is not null)
        {
            if (summary.Headline.Length > 0)
            {
                sb.Append($"""
                      <div style="font-size:15px;font-weight:700;line-height:1.55;color:#212529;word-break:break-word;">{Esc(summary.Headline)}</div>
                    """);
            }

            if (summary.Points.Count > 0)
            {
                sb.Append($"""
                      <div style="padding-top:{(summary.Headline.Length > 0 ? "9" : "0")}px;">{Bullets(summary.Points)}</div>
                    """);
            }

            // 「확인할 것」은 AI 가 짚은 것이고 「할 일」은 기계가 아는 사실이라, 근거가 달라 한 칸에 뭉치지 않는다.
            if (summary.Checks.Count > 0)
            {
                sb.Append($"""
                      <div style="margin-top:11px;border-top:1px solid #e3e6ea;padding-top:9px;">
                        <div style="font-size:11.5px;font-weight:700;color:#e8590c;letter-spacing:.06em;padding-bottom:2px;">확인할 것</div>
                        {Bullets(summary.Checks, size: "13px", color: "#495057")}
                      </div>
                    """);
            }
        }
        else
        {
            // 왜 없는지부터 적는다 — 결과문 앞줄만 세우면 그것이 요약인 줄 알고 서론으로 읽힌다.
            sb.Append("""
                  <div style="font-size:12.5px;line-height:1.6;color:#868e96;padding-bottom:7px;">AI 가 정리한 요약을 받지 못했습니다 — 결과문 앞부분을 대신 싣습니다.</div>
                """);

            sb.Append(Lines(Gist(r.ResultText, lines: 5)) is { Length: > 0 } gist
                ? $"""<div style="font-size:13.5px;line-height:1.6;color:#343a40;word-break:break-word;">{gist}</div>"""
                : """<div style="font-size:13.5px;line-height:1.6;color:#adb5bd;">(AI 가 아무 말 없이 끝났습니다.)</div>""");
        }

        sb.Append("""
                </td></tr>
              </table>
            </td></tr>
            """);

        return sb.ToString();
    }

    /// <summary>
    /// 점 찍힌 여러 줄. ul 을 쓰지 않는다 — 메일 앱마다 들여쓰기와 점 모양이 달라 다른 칸과 맞지 않는다.
    /// </summary>
    private static string Bullets(
        IReadOnlyList<string> items, string size = "13.5px", string color = "#343a40")
    {
        var sb = new StringBuilder();

        foreach (var item in items)
        {
            sb.Append($"""
                <div style="padding-top:3px;padding-left:13px;text-indent:-13px;font-size:{size};line-height:1.6;color:{color};word-break:break-word;">· {Esc(item)}</div>
                """);
        }

        return sb.ToString();
    }

    /// <summary>요약 칸에 들어갈 여러 줄. 없으면 <c>null</c> 이다.</summary>
    private static string? Lines(IReadOnlyList<string>? lines) =>
        lines is { Count: > 0 }
            ? string.Join("<br>", lines.Select(Esc))
            : null;

    /// <summary>「없다」를 적는 말. <b>빈 칸으로 두지 않는다</b> — 빠진 것처럼 보인다.</summary>
    private static string Muted(string text) =>
        $"""<span style="color:#adb5bd;">({Esc(text)})</span>""";

    /// <summary>
    /// HTML 로 나갈 글자를 가린다. 빠뜨리면 AI 답의 코드 조각 때문에 보고서가 무너진다.
    /// </summary>
    private static string Esc(string? text) =>
        System.Net.WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>상태 색. 머리띠·단추·「할 일」 선이 같은 색을 쓴다.</summary>
    private static string Accent(string? status) => status switch
    {
        "succeeded" => "#2b8a3e",
        "failed" or "timeout" => "#c92a2a",
        _ => "#495057",
    };

    /// <summary>상태 색의 옅은 것 — 「할 일」 칸 바탕이다.</summary>
    private static string Tint(string? status) => status switch
    {
        "succeeded" => "#f4fbf6",
        "failed" or "timeout" => "#fff5f5",
        _ => "#f8f9fa",
    };

    /// <summary>
    /// 앱푸시 본문. 무엇을 했나 몇 줄 — 제목이 상태와 작업 제목을 말하므로 되풀이하지 않는다.
    /// 메일 요약과 같은 값을 쓰고 줄머리를 · 로 찍는다(내 알림함 카드는 줄바꿈을 접는다). 길이는 PushBodyMax 에서 끊는다.
    /// </summary>
    private string PushBody(MailRow r)
    {
        var lines = new List<string>();

        if (AiResultSummary.Parse(r.SummaryText) is { } summary)
        {
            // 제목 줄과 같은 말을 두 번 하지 않는다 — 요약이 적히면 작업 제목도 그 첫 문장이 되므로, 그때는 한 일(Points)부터 적는다.
            if (summary.Headline.Length > 0 && !SameLine(summary.Headline, r.Title))
            {
                lines.Add(Trim(summary.Headline, 110));
            }

            // 한 일은 둘까지만 — 알림창은 두어 줄에서 끊는다.
            lines.AddRange(summary.Points.Take(2).Select(p => $"· {Trim(p, 80)}"));

            // 확인할 것은 한 줄이라도 싣는다 — 사람이 가서 해야 할 일인지가 알림에서 가장 크게 갈린다.
            if (summary.Checks.Count > 0)
            {
                lines.Add($"확인할 것 · {Trim(summary.Checks[0], 80)}");
            }
        }

        if (lines.Count == 0)
        {
            lines.AddRange(PushFallback(r));
        }

        return Trim(string.Join("\n", lines), PushBodyMax);
    }

    /// <summary>
    /// 요약이 없을 때의 푸시 본문. 고친 것이 있으면 변경 요약과 할 일, 실패면 오류 한 줄, 물어보기만 했으면 결과문 앞줄을 싣는다.
    /// 결과문 앞줄을 무턱대고 뜨면 서론이나 머리글이 두 줄을 다 차지한다 — 그래서 셋으로 가른다.
    /// </summary>
    private IReadOnlyList<string> PushFallback(MailRow r)
    {
        if (r.TaskStatus != "succeeded" && !string.IsNullOrWhiteSpace(r.ErrorSummary))
        {
            return [Trim(SecretMask.Apply(r.ErrorSummary), 140)];
        }

        if (!string.IsNullOrWhiteSpace(r.DiffStat))
        {
            return [ChangeGist(r), NextStep(r)];
        }

        return Gist(r.ResultText, lines: 2) is { Count: > 0 } gist
            ? [.. gist.Select(l => Trim(l, 110))]
            : (IReadOnlyList<string>)[$"{StatusText(r.TaskStatus)} · {r.Seq}차{Took(r)}"];
    }

    /// <summary>앱푸시 본문의 글자 상한. 까닭은 PushBody 머리말에 있다.</summary>
    private const int PushBodyMax = 240;

    /// <summary>
    /// 두 줄이 사실상 같은 말인가. 한쪽이 다른 쪽의 앞부분이기만 해도 같다고 본다 — 제목 칸은 길이 상한에 잘려 저장되기도 한다.
    /// </summary>
    private static bool SameLine(string? a, string? b)
    {
        var x = (a ?? string.Empty).Trim();
        var y = (b ?? string.Empty).Trim();

        if (x.Length == 0 || y.Length == 0)
        {
            return false;
        }

        var (shorter, longer) = x.Length <= y.Length ? (x, y) : (y, x);

        // 열 글자 미만의 우연한 겹침은 같다고 보지 않는다 — 짧은 제목의 헤드라인이 통째로 빠지기 때문.
        return shorter.Length >= 10
            && longer.StartsWith(shorter, StringComparison.Ordinal);
    }

    /// <summary>
    /// 요약의 첫 줄. 이 한 줄만 읽어도 무엇이 어떻게 됐는지 나와야 한다.
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
    /// 이어가기 지시문에서 이번에 시킨 것만 떼어 낸다. 지난 회차 머리말이 앞에 있으므로 맨 끝의 「## 이번에 할 일」 뒤를 집는다.
    /// 머리표가 없으면 손대지 않고, 여러 번 이어간 건은 마지막 것을 집는다. 전문 쪽은 그대로 둔다(AI 가 읽은 글자를 가리지 않도록).
    /// </summary>
    private static string? ThisTurn(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return instruction;
        }

        const string marker = "## 이번에 할 일";

        var at = instruction.LastIndexOf(marker, StringComparison.Ordinal);

        if (at < 0)
        {
            return instruction;
        }

        var tail = instruction[(at + marker.Length)..];

        // 머리표 뒤가 비었으면 자르지 않은 쪽이 낫다 — 지난 머리말이라도 있는 편이 무엇에 대한 메일인지 답한다.
        return string.IsNullOrWhiteSpace(tail) ? instruction : tail;
    }

    /// <summary>
    /// 긴 글에서 앞쪽 몇 줄만 뽑아 한 덩이로 만든다. AI 요약을 못 받았을 때만 쓰인다 — 요약이 실패해도 메일은 나간다.
    /// 표 구분선이나 밑줄 같은 내용 없는 줄은 건너뛴다.
    /// </summary>
    private static IReadOnlyList<string>? Gist(string? text, int lines)
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

        // 줄을 그대로 돌려준다 — 공백으로 들여쓰기를 맞추면 글꼴 폭에 기대게 되므로, 칸은 표가 잡는다.
        return picked;
    }

    /// <summary>마크다운 머리표를 뗀다. 요약에서는 ## 나 - 가 뜻을 더하지 않는다.</summary>
    private static string Plain(string line) =>
        line.TrimStart('#', '>', '*', '-', ' ').Trim();

    /// <summary>내용 없는 꾸밈 줄인가 — <c>---</c> · <c>===</c> · <c>|---|</c> 따위.</summary>
    private static bool Decoration(string line) =>
        line.All(c => c is '-' or '=' or '|' or ':' or '_' or '*' or '#' or ' ' or '─' or '═');

    /// <summary>
    /// 무엇이 바뀌었나 한 줄. git diff --stat 의 마지막 요약 줄이 이미 그 답이라 그것을 쓴다.
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

        // 마지막 줄이 "3 files changed, 120 insertions(+), 4 deletions(-)" 꼴이다. 480자에서 잘려 없을 수도 있으니 그때는 줄 수로 센다.
        var tail = rows.LastOrDefault(l => l.Contains("changed", StringComparison.OrdinalIgnoreCase));

        return tail is not null
            ? Trim(tail, 110)
            : $"파일 {rows.Count}개";
    }

    /// <summary>내가 할 일이 있나. 「읽고 끝」인지 「가서 봐야 하는지」가 여기서 갈린다.</summary>
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

    /// <summary>
    /// 실행·작업을 한 줄로 편 것. 시험이 보게 internal 이다 — ProjMngServer.Tests 가 이 줄을 채워 요약이 제대로 서는지 확인한다.
    /// </summary>
    internal sealed class MailRow
    {
        public long TaskKey { get; set; }
        public string? Title { get; set; }
        public bool NotifyEmail { get; set; }
        public bool NotifyPwa { get; set; }
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

        /// <summary>「처리 파일」 — 이 실행이 건드린 파일 목록. 한 줄에 상태\t경로. 화면과 같은 값을 쓴다.</summary>
        public string? ChangedFiles { get; set; }

        public string? ErrorSummary { get; set; }
        public string? GitBranch { get; set; }

        /// <summary>그때 실제로 준 지시문. 요약에 「무엇을 시켰나」를 적으려면 필요하다.</summary>
        public string? Instruction { get; set; }

        /// <summary>AI 가 다시 써 준 요약. 이미 있으면 모델을 또 부르지 않는다.</summary>
        public string? SummaryText { get; set; }
    }
}
