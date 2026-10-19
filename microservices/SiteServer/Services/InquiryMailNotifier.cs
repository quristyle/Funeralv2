using System.Text;
using System.Text.Json;
using SiteServer.DTOs;
using JSini.Shared.DTOs;

namespace SiteServer.Services;

/// <summary>문의 관련 메일 발송 — NotificationServer(<c>/emails/send</c>, SMTP 직발송)를 부른다.</summary>
public interface IInquiryMailNotifier
{
    /// <summary>접수 알림을 담당자에게 보낸다. <b>실패해도 예외를 던지지 않는다.</b></summary>
    Task NotifyAsync(Guid inquiryId, InquiryRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// HTML 메일을 보낸다 (답장 등). 성공 여부를 돌려준다 —
    /// 답장은 관리자가 화면에서 결과를 봐야 하므로 삼키지 않는다.
    /// </summary>
    Task<bool> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>NotificationServer 호출 구현. 게이트웨이를 거치지 않고 루프백으로 부른다(JWT 없이 X-User-Id: SITE_INQUIRY). 받는 역할의 정본은 포털관리 알림관리의 SITE_INQUIRY 줄이다.
/// 설정 파일에 역할을 적어 두면 화면에서 체크를 다 풀어도 그 기본값으로 되돌아가므로 역할은 비워 둔다. 역할 밖의 고정 주소가 필요하면 InquiryMail:To 에 쉼표로 잇는다.</summary>
public class InquiryMailNotifier(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<InquiryMailNotifier> logger) : IInquiryMailNotifier
{
    public async Task NotifyAsync(Guid inquiryId, InquiryRequestDto request, CancellationToken ct = default)
    {
        // 설정이 비어도 보낸다 — 받을 역할은 포털관리 알림관리가 정하고, 여기 설정은 표가 비었을 때만 쓰는 비상 통로다. 비었다고 되돌아서면 화면이 거짓말을 한다.
        var toRole = configuration["InquiryMail:ToRole"];
        var to = configuration["InquiryMail:To"];

        var (subject, body) = InquiryEmailTemplates.Received(inquiryId, request);

        // 메일 실패가 접수 실패가 되면 안 된다 — 결과는 로그로만 남긴다. 받는 역할은 eventCode 로 알림 서버가 정책에서 풀며, 아무것도 없으면 알림 서버가 400 으로 말한다.
        var ok = await PostAsync(
            new { to, toRole, subject, body, html = true, eventCode = NotificationEvents.SiteInquiry },
            ct);
        if (ok)
        {
            // 받는 역할이 비어 있는 것이 정상이다 — 알림관리가 정한다(머리말).
            logger.LogInformation("문의 알림 메일을 보냈습니다. inquiry={Id} toRole={Role} to={To}",
                inquiryId,
                string.IsNullOrWhiteSpace(toRole) ? "(알림관리)" : toRole,
                string.IsNullOrWhiteSpace(to) ? "(없음)" : to);
        }
    }

    public Task<bool> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        => PostAsync(new { to, subject, body = htmlBody, html = true }, ct);

    /// <summary>NotificationServer /emails/send 호출. 실패는 로그 후 false.</summary>
    private async Task<bool> PostAsync(object payload, CancellationToken ct)
    {
        var baseUrl = configuration["InquiryMail:NotificationBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("NotificationServer 주소 설정이 없습니다 (InquiryMail:NotificationBaseUrl).");
            return false;
        }

        try
        {
            var client = httpClientFactory.CreateClient("notification");
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/emails/send")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8, "application/json"),
            };
            msg.Headers.Add("X-User-Id", "SITE_INQUIRY");

            var res = await client.SendAsync(msg, ct);
            if (!res.IsSuccessStatusCode)
            {
                var detail = await res.Content.ReadAsStringAsync(ct);
                logger.LogWarning("메일 발송 실패. status={Status} body={Body}",
                    (int)res.StatusCode, detail);
            }

            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "메일 발송 중 오류.");
            return false;
        }
    }
}
