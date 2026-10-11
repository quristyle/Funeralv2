using System.Text;
using System.Text.Json;
using JSini.Shared.DTOs;
using SiteServer.DTOs;

namespace SiteServer.Services;

/// <summary>문의 접수 앱 푸시 — NotificationServer(<c>/notifications/push</c>)를 부른다.</summary>
public interface IInquiryPushNotifier
{
    /// <summary>접수 알림을 담당 역할에게 보낸다. <b>실패해도 예외를 던지지 않는다.</b></summary>
    Task NotifyAsync(Guid inquiryId, InquiryRequestDto request, CancellationToken ct = default);
}

/// <summary>
/// 문의가 접수됐다는 것을 담당자에게 <b>앱 푸시</b>로 알린다 —
/// NotificationServer 의 <c>/notifications/push</c> 를 부른다.
/// </summary>
/// <remarks>
/// <para>
/// 메일(<see cref="IInquiryMailNotifier"/>)과 짝이다. 메일은 자리를 비운
/// 담당자에게 남는 기록이고, 푸시는 <b>지금 보게 하는 길</b>이다 — 문의한
/// 사람은 답을 기다리고 있고, 늦게 여는 메일함에서는 그 급함이 사라진다.
/// </para>
///
/// <para>
/// [받는 사람을 여기서 정하지 않는다]
/// </para>
///
/// <para>
/// 역할 이름(<c>InquiryPush:ToRole</c> — 없으면 메일과 같은
/// <c>InquiryMail:ToRole</c>)만 넘기고, 사람으로 푸는 것도 푸시를 꺼 둔 사람을
/// 거르는 것도 NotificationServer 가 한다. 역할표(<c>scom.role_accounts</c>)는
/// 포털 DB 에 있고 이 서비스는 <c>jsinisite</c> 만 본다.
/// </para>
///
/// <para>
/// <b>그 역할마저 기본값일 뿐이다.</b> 포털관리의 「알림관리」
/// (<c>/admin/system/notify-policy</c>)에 <c>SITE_INQUIRY</c> 의 <b>앱 푸시</b>를
/// 켠 역할이 하나라도 있으면 알림 서버가 그 역할들로 바꿔 보낸다 — 설정 파일을
/// 고치고 서비스를 다시 띄우지 않고 받는 사람을 바꾸는 길이 그것이다
/// (<c>docs/notify-policy.md</c>).
/// </para>
///
/// <para>
/// [메일이 두 통 가지 않게]
/// </para>
///
/// <para>
/// 메일은 <b>지금까지처럼</b> <see cref="IInquiryMailNotifier"/> 가 제 틀로
/// 따로 낸다. 그래서 <c>notification_events.email_from_push</c> 는
/// <c>SITE_INQUIRY</c> 에서 <b>거짓으로 둔다</b> — 켜면 푸시 경로가 같은 내용을
/// 한 통 더 내서 담당자가 문의 하나에 메일 두 통을 받는다.
/// </para>
///
/// <para>
/// 실패해도 예외를 던지지 않는다. 접수는 이미 저장됐고 메일도 따로 간다 —
/// 알림이 못 간 것을 접수 실패로 만들면 문의한 사람이 같은 글을 다시 쓴다.
/// </para>
/// </remarks>
public class InquiryPushNotifier(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<InquiryPushNotifier> logger) : IInquiryPushNotifier
{
    /// <summary>
    /// 눌렀을 때 열 곳 — 포털의 [사이트 문의내역].
    /// </summary>
    /// <remarks>
    /// <b>Blazor 의 <c>@page</c> 주소다.</b> DB 메뉴에는 옛 경로
    /// (<c>/company/site-inquiries</c>)가 남아 있지만 라우팅의 임자는 이제
    /// <c>@page</c> 라(<c>web/CLAUDE.md</c>), 알림이 띄우는 창은 실제 라우트로
    /// 보내야 「눌렀더니 준비 중이 뜬다」가 안 생긴다.
    /// </remarks>
    private const string InquiryListUrl = "/site/inquiries";

    /// <summary>
    /// 알림의 수명(초) — 하루.
    /// </summary>
    /// <remarks>
    /// 서버 기본값은 여섯 시간이다(<c>Push:DefaultTtlSeconds</c>). 배포 결과처럼
    /// <b>지나고 나면 뜻이 없는</b> 알림에 맞춘 값인데, 문의는 그 반대다 —
    /// 주말을 끼고 들어온 문의도 월요일에 보는 편이 낫다.
    /// </remarks>
    private const int TtlSeconds = 24 * 60 * 60;

    /// <summary>푸시 본문에 싣는 제목의 최대 길이. 넘으면 말줄임표로 접는다.</summary>
    private const int SubjectLimit = 60;

    public async Task NotifyAsync(Guid inquiryId, InquiryRequestDto request, CancellationToken ct = default)
    {
        var roles = ResolveRoles();
        if (roles.Count == 0)
        {
            // 설정이 없으면 보내지 않는다. 접수 자체는 이미 끝났으므로 경고만 남긴다.
            logger.LogWarning("문의 접수 푸시 대상 역할 설정이 없어 보내지 않습니다 (InquiryPush:ToRole / InquiryMail:ToRole).");
            return;
        }

        var baseUrl = Setting("InquiryPush:NotificationBaseUrl", "InquiryMail:NotificationBaseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("NotificationServer 주소 설정이 없습니다 (InquiryPush:NotificationBaseUrl / InquiryMail:NotificationBaseUrl).");
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            roles,
            message = new
            {
                title = "새 문의가 접수됐습니다",
                body = BuildBody(request),
                url = InquiryListUrl,

                // 알림구분(공통코드 NOTI_CATEGORY). 기록에만 남고 알림창에는 안 뜬다 —
                // 「내 알림함」과 「발송 이력」이 이것으로 거른다.
                category = PushCategories.SiteInquiry,

                // 설정이 걸리는 열쇠. 구분과 글자가 같지만 하는 일이 다르다.
                eventCode = NotificationEvents.SiteInquiry,

                // 같은 문의로 두 번 보내더라도 알림창은 한 장으로 겹친다.
                // 문의마다 달라야 **서로 다른 문의가 서로를 덮지 않는다**.
                tag = $"site-inquiry-{inquiryId}",
                ttlSeconds = TtlSeconds,
            },
        });

        try
        {
            var client = httpClientFactory.CreateClient("notification");
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/notifications/push")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };

            // 게이트웨이를 안 거치는 루프백 호출이라 JWT 가 없다. 부른 이 표시만 남긴다
            // (메일 쪽과 같은 글자 — 발송 이력에서 둘이 한 자리에서 나온 것으로 보인다).
            msg.Headers.Add("X-User-Id", "SITE_INQUIRY");

            var res = await client.SendAsync(msg, ct);

            // 202 = 받을 기기가 하나도 없었다(구독이 없거나 모두 껐다). 고장이 아니다.
            if ((int)res.StatusCode == 202)
            {
                logger.LogInformation(
                    "문의 접수 푸시: 받을 기기가 없었습니다. inquiry={Id} roles={Roles}",
                    inquiryId, string.Join(",", roles));
            }
            else if (!res.IsSuccessStatusCode)
            {
                var detail = await res.Content.ReadAsStringAsync(ct);
                logger.LogWarning("문의 접수 푸시 실패. status={Status} body={Body}",
                    (int)res.StatusCode, detail);
            }
            else
            {
                logger.LogInformation("문의 접수 푸시를 보냈습니다. inquiry={Id} roles={Roles}",
                    inquiryId, string.Join(",", roles));
            }
        }
        catch (Exception ex)
        {
            // NotificationServer 가 꺼져 있을 수 있다. 접수는 이미 끝났다.
            logger.LogWarning(ex, "문의 접수 푸시 중 오류.");
        }
    }

    /// <summary>
    /// 받을 역할들. <c>InquiryPush:ToRole</c> 이 없으면 메일과 같은 역할을 쓴다.
    /// </summary>
    /// <remarks>
    /// 둘을 가른 까닭은 <b>메일과 푸시의 담당자가 다를 수 있기 때문</b>이고,
    /// 안 적으면 같은 사람에게 간다 — 설정 파일 하나를 더 채워야 푸시가 돌기
    /// 시작하는 모양으로 두면 「올렸는데 아무 일도 없다」가 된다.
    /// </remarks>
    private List<string> ResolveRoles()
    {
        var raw = Setting("InquiryPush:ToRole", "InquiryMail:ToRole");

        return string.IsNullOrWhiteSpace(raw)
            ? []
            : [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// 앞의 열쇠를 보고, <b>비어 있으면</b> 뒤의 것을 쓴다.
    /// </summary>
    /// <remarks>
    /// <c>??</c> 로는 안 된다 — appsettings 에 <c>""</c> 로 적어 둔 칸은
    /// <c>null</c> 이 아니라 <b>빈 글자</b>로 돌아와서, 물려받기가 조용히 끊기고
    /// 「설정이 없다」 경고만 남는다.
    /// </remarks>
    private string? Setting(string key, string fallbackKey)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? configuration[fallbackKey] : value;
    }

    /// <summary>
    /// 알림창에 뜨는 한 줄 — 「누가 · 무엇을」.
    /// </summary>
    /// <remarks>
    /// <b>본문(<c>Message</c>)은 싣지 않는다.</b> 저장 전에 거른 HTML 이라
    /// 알림창에서는 태그가 글자로 보이고, 길이도 가늠할 수 없다. 자세한 것은
    /// 눌러서 보는 자리(<see cref="InquiryListUrl"/>)에 있다.
    /// </remarks>
    private static string BuildBody(InquiryRequestDto request)
    {
        var who = string.IsNullOrWhiteSpace(request.Company)
            ? request.Name.Trim()
            : $"{request.Name.Trim()} ({request.Company!.Trim()})";

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "(제목 없음)"
            : request.Subject.Trim();

        if (subject.Length > SubjectLimit) subject = subject[..SubjectLimit] + "…";

        return string.IsNullOrWhiteSpace(request.Category)
            ? $"{who} — {subject}"
            : $"[{request.Category!.Trim()}] {who} — {subject}";
    }
}
