using System.Net;
using System.Text;
using AuthServer.DTOs;
using AuthServer.Entities;
using JSini.Shared.Infrastructure.Time;
using JSini.Shared.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 배치 하나를 <b>메일 한 통으로 만들어 내보낸다.</b>
/// </summary>
/// <remarks>
/// <para>
/// [메일에 숫자를 담지 않고 <b>보고서로 가는 길</b>을 담는다]
/// </para>
///
/// <para>
/// 운영 리포트 화면들이 읽는 자료는 <b>보는 사람마다 다르다</b> —
/// 「운영 모니터링」이 긁어 오는 <c>dashboard/admin-stats</c> 는 이름부터
/// 「내 배정 현황」이고, 주간·월간도 「내 것」과 「전체」가 갈린다. 그 한 벌을
/// 서버가 대표로 떠서 열 사람에게 똑같이 보내면 <b>아홉 명은 남의 숫자</b>를
/// 받는다. 그래서 메일은 「이 보고서들을 볼 때가 되었다」고 알리고 각자의
/// 화면으로 데려간다 — 거기서는 본인의 권한과 본인의 자료로 그려진다.
/// </para>
///
/// <para>
/// 숫자를 메일에 담아야 할 날이 오면 손댈 곳은 <see cref="BuildBody"/> 하나다.
/// 그때 먼저 풀어야 하는 것은 <b>누구의 자료인가</b>이지 HTML 이 아니다.
/// </para>
///
/// <para>
/// [보내는 길은 하나다]
/// </para>
///
/// <para>
/// 주소를 여기서 풀지 않는다. 역할 이름을 그대로 NotificationServer 에 넘기면
/// 그쪽이 <c>scom</c> 으로 풀고, <b>본인이 꺼 둔 메일 갈래까지 본다.</b>
/// 여기서 주소를 풀어 보내면 그 설정이 조용히 새는 길이 하나 생긴다
/// (<c>AccountMailClient.SendToRolesAsync</c> 머리말).
/// </para>
/// </remarks>
public class ReportMailSender(
    AccountMailClient mail,
    IConfiguration configuration,
    ILogger<ReportMailSender> logger)
{
    /// <summary><c>X-User-Id</c> 에 실을 이름. 사람이 아니라 기능을 적는다.</summary>
    private const string SenderKey = "AUTH_REPORT_MAIL";

    /// <summary>
    /// 미리받아보기가 쓰는 이름. <b>정기 발송과 가른다</b> — 알림 서버의
    /// 기록에서 「주기로 나간 것」과 「누가 눌러 본 것」이 같은 줄로 보이면
    /// 발송량을 셀 때 눌러 본 수가 섞인다.
    /// </summary>
    private const string PreviewSenderKey = "AUTH_REPORT_MAIL_PREVIEW";

    /// <summary>포털 주소. 메일의 링크가 이 값으로 만들어진다.</summary>
    private string PortalBaseUrl =>
        (configuration["Portal:BaseUrl"] ?? "http://localhost:5557").TrimEnd('/');

    /// <summary>
    /// 한 통 보낸다. 성공 여부와 <b>화면에 그대로 적을 한 줄</b>을 돌려준다.
    /// </summary>
    public async Task<(bool Ok, string Message)> SendAsync(
        ReportMailSchedule schedule,
        IReadOnlyList<ReportCatalogItemDto> catalog,
        string actor,
        CancellationToken ct = default)
    {
        var roles = (schedule.Roles ?? []).Select(r => r.RoleId).Distinct(StringComparer.Ordinal).ToList();

        var stamp = AppTime.ToKorea(AppTime.UtcNow).ToString("yyyy-MM-dd HH:mm");

        // **보낼 것이 없으면 보내지 않는다.** 빈 메일이 주기마다 나가는 것이
        // 가장 나쁘다 — 받는 사람이 곧 거르기 규칙을 만들고, 그때부터 진짜
        // 보고서도 안 읽힌다.
        if (roles.Count == 0) return (false, $"[{stamp}] 받을 역할이 없어 보내지 않았습니다.");

        var (ready, why, live, missing) = Compose(schedule, catalog, stamp);
        if (!ready) return (false, why);

        var subject = Subject(schedule, preview: false);
        var body = BuildBody(schedule, live, missing, preview: false);

        // **받는 역할을 알림관리가 대신할 수 있다.** 배치가 고른 역할
        // (`roles`)을 그대로 넘기지만, 알림관리에 `REPORT_MAIL` 정책이 걸려
        // 있으면 알림 서버가 그 역할로 바꿔 보낸다 — 그 사정은
        // docs/notify-policy.md 「보고서 메일과 겹치는 자리」에 적어 두었다.
        var (ok, reason) = await mail.SendToRolesAsync(
            roles, subject, body, SenderKey, NotificationEvents.ReportMail, ct);

        if (ok)
        {
            logger.LogInformation(
                "보고서 메일 발송: {Name} ({Id}) — 보고서 {Reports}건 · 역할 {Roles} (요청: {Actor})",
                schedule.Name, schedule.Id, live.Count, string.Join(",", roles), actor);

            var note = missing.Count > 0 ? $" (없어진 보고서 {missing.Count}건은 뺐습니다)" : string.Empty;
            return (true, $"[{stamp}] 보고서 {live.Count}건을 역할 {roles.Count}개로 보냈습니다.{note}");
        }

        logger.LogError(
            "보고서 메일 발송 실패: {Name} ({Id}) — {Why}", schedule.Name, schedule.Id, reason);

        return (false, $"[{stamp}] 보내지 못했습니다 — {reason}");
    }

    /// <summary>
    /// <b>누른 사람 본인에게만</b> 한 통 보낸다 — 「미리받아보기」.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [역할을 거치지 않는 유일한 길이다]
    /// </para>
    /// <para>
    /// 정기 발송은 주소를 안 풀고 역할 이름만 넘긴다(클래스 머리말). 이쪽은
    /// 반대로 <b>주소 하나</b>로 보낸다 — 받는 사람이 누른 사람 본인으로
    /// 이미 정해져 있어 풀 것이 없고, 본인이 지금 달라고 누른 메일을
    /// 「메일 알림을 꺼 뒀다」로 막으면 아무 일도 안 일어난 것처럼 보인다.
    /// 그 설정은 <b>남이 보내는 것</b>을 끄려고 있는 것이다.
    /// </para>
    /// <para>
    /// 넘어오는 <paramref name="schedule"/> 은 저장된 줄이 아니어도 된다 —
    /// 화면에서 고르고 있는 그대로를 담은 임시 객체다. 그래야 저장 전에
    /// 메일 꼴을 볼 수 있다.
    /// </para>
    /// </remarks>
    public async Task<(bool Ok, string Message, int Count, List<string> Missing)> SendPreviewAsync(
        ReportMailSchedule schedule,
        IReadOnlyList<ReportCatalogItemDto> catalog,
        string to,
        string actor,
        CancellationToken ct = default)
    {
        var stamp = AppTime.ToKorea(AppTime.UtcNow).ToString("yyyy-MM-dd HH:mm");

        var (ready, why, live, missing) = Compose(schedule, catalog, stamp);
        if (!ready) return (false, why, 0, missing);

        var subject = Subject(schedule, preview: true);
        var body = BuildBody(schedule, live, missing, preview: true);

        // **이벤트 코드를 싣지 않는다.** 미리받아보기는 누른 사람 본인에게만
        // 가는 한 통이지 그 이벤트의 발송이 아니다. 코드를 실으면 관리자가
        // `REPORT_MAIL` 을 꺼 둔 날 **메일 꼴을 확인할 길까지 막힌다**.
        var (ok, reason) = await mail.SendToAddressAsync(
            to, subject, body, PreviewSenderKey, eventCode: null, ct);

        if (ok)
        {
            logger.LogInformation(
                "보고서 메일 미리받아보기: 보고서 {Reports}건 → {To} (요청: {Actor})",
                live.Count, to, actor);

            var note = missing.Count > 0 ? $" (없어진 보고서 {missing.Count}건은 뺐습니다)" : string.Empty;
            return (true, $"[{stamp}] 보고서 {live.Count}건을 {to} 로 보냈습니다.{note}", live.Count, missing);
        }

        logger.LogError(
            "보고서 메일 미리받아보기 실패: {To} (요청: {Actor}) — {Why}", to, actor, reason);

        return (false, $"[{stamp}] 보내지 못했습니다 — {reason}", 0, missing);
    }

    /// <summary>
    /// 고른 열쇠를 <b>지금 메뉴에 있는 보고서</b>와 사라진 것으로 가른다.
    /// 보낼 수 없으면 그 까닭을 함께 돌려준다.
    /// </summary>
    /// <remarks>
    /// 정기 발송과 미리받아보기가 <b>같은 규칙으로</b> 갈라야 한다 — 미리 본
    /// 것과 실제로 가는 것이 다르면 미리보기가 아니다.
    /// </remarks>
    private static (bool Ok, string Message, List<ReportCatalogItemDto> Live, List<string> Missing) Compose(
        ReportMailSchedule schedule,
        IReadOnlyList<ReportCatalogItemDto> catalog,
        string stamp)
    {
        var keys = (schedule.Reports ?? []).Select(r => r.ReportKey).Distinct(StringComparer.Ordinal).ToList();

        if (keys.Count == 0)
        {
            return (false, $"[{stamp}] 고른 보고서가 없어 보내지 않았습니다.", [], []);
        }

        var items = keys
            .Select(k => (Key: k, Item: catalog.FirstOrDefault(c => c.RouteKey == k)))
            .ToList();

        var live = items.Where(i => i.Item is not null).Select(i => i.Item!).ToList();
        var missing = items.Where(i => i.Item is null).Select(i => i.Key).ToList();

        if (live.Count == 0)
        {
            return (false,
                $"[{stamp}] 고른 보고서가 모두 메뉴에서 사라져 보내지 않았습니다 "
                + $"({string.Join(", ", missing)}).",
                live, missing);
        }

        return (true, string.Empty, live, missing);
    }

    /// <summary>
    /// 메일 제목. 미리받아보기는 <b>제목에서부터 갈라 적는다</b> — 받은 사람이
    /// 열기 전에 「주기로 온 것」이 아님을 알아야 한다.
    /// </summary>
    private static string Subject(ReportMailSchedule schedule, bool preview) =>
        preview
            ? $"[JSini] (미리받아보기) 시스템 모니터링 보고서 — {schedule.Name}"
            : $"[JSini] 시스템 모니터링 보고서 — {schedule.Name}";

    /// <summary>
    /// 메일 본문(HTML). <b>완성된 문서로 보낸다</b>(<c>html = true</c>) —
    /// 알림 서버의 평문 틀을 두 겹으로 씌우지 않는다.
    /// </summary>
    /// <remarks>
    /// 표나 바깥 스타일시트를 쓰지 않고 <c>style</c> 속성만 쓴다. 메일
    /// 클라이언트마다 <c>&lt;style&gt;</c> 를 떼어 내는 규칙이 달라서, 그쪽에서
    /// 무너지면 확인할 길이 없다.
    /// </remarks>
    private string BuildBody(
        ReportMailSchedule schedule,
        IReadOnlyList<ReportCatalogItemDto> reports,
        IReadOnlyList<string> missing,
        bool preview)
    {
        var nowKst = AppTime.ToKorea(AppTime.UtcNow);
        var html = new StringBuilder();

        html.Append("<div style=\"font-family:'Malgun Gothic',AppleSDGothicNeo,sans-serif;")
            .Append("font-size:14px;color:#1f2937;line-height:1.7;max-width:640px\">");

        html.Append("<h2 style=\"font-size:18px;margin:0 0 4px\">시스템 모니터링 보고서</h2>");

        // **미리받아보기임을 본문에서도 말한다.** 제목을 안 보고 넘긴 사람이
        // 「정기 발송이 시작됐다」고 읽으면, 아직 저장도 안 한 배치가 도는
        // 줄 안다.
        if (preview)
        {
            html.Append("<p style=\"margin:0 0 12px;padding:8px 12px;background:#eff6ff;")
                .Append("border-left:3px solid #2563eb;border-radius:4px;font-size:13px;color:#1e40af\">")
                .Append("<b>미리받아보기</b> — 눌러 보신 분에게만 보낸 한 통입니다. ")
                .Append("정기 발송과는 무관하며 다음 발송 시각도 바뀌지 않습니다.")
                .Append("</p>");
        }

        html.Append("<p style=\"margin:0 0 16px;color:#6b7280\">")
            .Append(Esc(schedule.Name)).Append(" · ")
            .Append(Esc(ReportMailSchedulePlan.Describe(schedule))).Append(" · ")
            .Append(nowKst.ToString("yyyy-MM-dd HH:mm")).Append(" 기준")
            .Append("</p>");

        if (!string.IsNullOrWhiteSpace(schedule.Remark))
        {
            html.Append("<p style=\"margin:0 0 16px;padding:10px 12px;background:#f3f4f6;")
                .Append("border-radius:6px\">")
                .Append(Esc(schedule.Remark))
                .Append("</p>");
        }

        html.Append("<ul style=\"margin:0;padding:0;list-style:none\">");

        foreach (var report in reports)
        {
            var url = $"{PortalBaseUrl}{report.Path}";

            html.Append("<li style=\"margin:0 0 10px;padding:10px 12px;border:1px solid #e5e7eb;")
                .Append("border-radius:6px\">")
                .Append("<a href=\"").Append(Esc(url))
                .Append("\" style=\"font-weight:600;color:#0b5699;text-decoration:none\">")
                .Append(Esc(report.Title))
                .Append("</a>")
                .Append("<div style=\"color:#6b7280;font-size:12px\">")
                .Append(Esc(report.Path))
                .Append("</div>")
                .Append("</li>");
        }

        html.Append("</ul>");

        // **없어진 보고서를 조용히 빼지 않는다.** 세 건인 줄 알고 두 건을
        // 받으면 무엇이 빠졌는지 알 길이 없다.
        if (missing.Count > 0)
        {
            html.Append("<p style=\"margin:16px 0 0;color:#b45309;font-size:12px\">")
                .Append("메뉴에서 사라진 보고서는 뺐습니다: ")
                .Append(Esc(string.Join(", ", missing)))
                .Append("</p>");
        }

        html.Append("<p style=\"margin:20px 0 0;color:#9ca3af;font-size:12px\">")
            .Append("숫자는 링크를 눌러 <b>본인 권한으로</b> 보시는 화면에 있습니다. ")
            .Append("받는 설정은 포털관리 › 시스템관리 › 보고서 메일에서 바꿉니다.")
            .Append("</p>");

        html.Append("</div>");

        return html.ToString();
    }

    /// <summary>
    /// 사람이 적은 글자를 HTML 에 넣기 전에 막는다. 배치 이름과 한마디가
    /// 그대로 본문에 실리므로 여기를 빼면 메일 본문이 통째로 무너질 수 있다.
    /// </summary>
    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
