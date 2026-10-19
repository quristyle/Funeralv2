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

/// <summary>문의 접수를 담당자에게 앱 푸시로 알린다(NotificationServer /notifications/push). 메일(IInquiryMailNotifier)과 짝이며, 푸시는 지금 보게 하는 길이다.
/// 받을 역할은 여기서 정하지 않고 eventCode = SITE_INQUIRY 만 보낸다(포털관리 알림관리가 정본). 메일은 따로 나가므로 email_from_push 는 거짓이어야 두 통이 안 간다. 실패해도 접수는 이미 저장됐으므로 예외를 던지지 않는다.</summary>
public class InquiryPushNotifier(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<InquiryPushNotifier> logger) : IInquiryPushNotifier
{
    /// <summary>눌렀을 때 열 곳 — 포털의 [사이트 문의내역]. DB 메뉴의 옛 경로 대신 @page 주소를 써야 「준비 중」이 안 뜬다.</summary>
    private const string InquiryListUrl = "/site/inquiries";

    /// <summary>알림의 수명(초) — 하루. 서버 기본(여섯 시간)은 지나면 뜻이 없는 알림에 맞춘 값이고, 문의는 주말을 끼고 들어와도 월요일에 보는 편이 낫다.</summary>
    private const int TtlSeconds = 24 * 60 * 60;

    /// <summary>푸시 본문에 싣는 제목의 최대 길이. 넘으면 말줄임표로 접는다.</summary>
    private const int SubjectLimit = 60;

    public async Task NotifyAsync(Guid inquiryId, InquiryRequestDto request, CancellationToken ct = default)
    {
        // 비어 있는 것이 정상이다 — 역할을 정하는 자리는 알림관리이고, 여기 적힌 것은 표가 빈 동안만 쓰는 비상 통로다.
        var roles = ResolveRoles();

        var baseUrl = Setting("InquiryPush:NotificationBaseUrl", "InquiryMail:NotificationBaseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("NotificationServer 주소 설정이 없습니다 (InquiryPush:NotificationBaseUrl / InquiryMail:NotificationBaseUrl).");
            return;
        }

        // 로그에 적는 한 줄. 비어 있는 것이 정상이라 빈 글자로 두면
        // 「설정을 빠뜨렸나」로 읽힌다.
        var roleText = roles.Count == 0 ? "(알림관리)" : string.Join(",", roles);

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
                    inquiryId, roleText);
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
                    inquiryId, roleText);
            }
        }
        catch (Exception ex)
        {
            // NotificationServer 가 꺼져 있을 수 있다. 접수는 이미 끝났다.
            logger.LogWarning(ex, "문의 접수 푸시 중 오류.");
        }
    }

    /// <summary>비상 통로로 적어 둔 역할들. 보통은 비어 있다 — 알림관리에 역할이 한 줄이라도 걸리면 그것이 이긴다. 메일과 푸시의 담당자가 다를 수 있어 둘을 가른다.</summary>
    private List<string> ResolveRoles()
    {
        var raw = Setting("InquiryPush:ToRole", "InquiryMail:ToRole");

        return string.IsNullOrWhiteSpace(raw)
            ? []
            : [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>앞의 열쇠가 비어 있으면 뒤의 것을 쓴다. ?? 는 쓰지 않는다 — appsettings 의 "" 는 null 이 아니라 빈 글자로 와서 물려받기가 끊기기 때문이다.</summary>
    private string? Setting(string key, string fallbackKey)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? configuration[fallbackKey] : value;
    }

    /// <summary>알림창에 뜨는 한 줄 — 「누가 · 무엇을」. 본문은 싣지 않는다(저장 전에 거른 HTML 이라 알림창에서는 태그가 글자로 보인다).</summary>
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
