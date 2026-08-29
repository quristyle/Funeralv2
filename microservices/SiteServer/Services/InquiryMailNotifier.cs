using System.Text;
using System.Text.Json;
using SiteServer.DTOs;

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

/// <summary>
/// NotificationServer 호출 구현.
/// </summary>
/// <remarks>
/// <para>
/// 게이트웨이를 거치지 않고 루프백으로 직접 부른다 — 서비스 간 호출이라
/// JWT 가 없고, NotificationServer 는 어차피 루프백에만 열려 있다.
/// 부른 이 표시는 <c>X-User-Id: SITE_INQUIRY</c> 로 남긴다.
/// </para>
///
/// <para>
/// 메일 본문은 <see cref="InquiryEmailTemplates"/> 가 만든다 — HTML 을 여기서 조립하지 않는다.
/// </para>
///
/// <para>
/// 받는 사람은 설정 <c>InquiryMail:To</c> 다. 지금은 확인용 개인 주소이고,
/// 검증이 끝나면 포털의 시스템관리자 역할 사용자들로 바꾼다 — 그때는 주소를
/// AuthServer 에서 받아 와야 하므로 이 클래스가 그 조회를 품게 된다 (D-S12).
/// </para>
/// </remarks>
public class InquiryMailNotifier(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<InquiryMailNotifier> logger) : IInquiryMailNotifier
{
    public async Task NotifyAsync(Guid inquiryId, InquiryRequestDto request, CancellationToken ct = default)
    {
        var to = configuration["InquiryMail:To"];
        if (string.IsNullOrWhiteSpace(to))
        {
            // 설정이 없으면 보내지 않는다. 접수 자체는 이미 끝났으므로 경고만 남긴다.
            logger.LogWarning("문의 알림 메일 설정이 없어 보내지 않습니다 (InquiryMail:To).");
            return;
        }

        var (subject, body) = InquiryEmailTemplates.Received(inquiryId, request);

        // 메일 실패가 접수 실패가 되면 안 된다 — 결과는 로그로만 남긴다.
        var ok = await SendHtmlAsync(to, subject, body, ct);
        if (ok)
        {
            logger.LogInformation("문의 알림 메일을 보냈습니다. inquiry={Id} to={To}", inquiryId, to);
        }
    }

    public async Task<bool> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
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
                    JsonSerializer.Serialize(new { to, subject, body = htmlBody, html = true }),
                    Encoding.UTF8, "application/json"),
            };
            msg.Headers.Add("X-User-Id", "SITE_INQUIRY");

            var res = await client.SendAsync(msg, ct);
            if (!res.IsSuccessStatusCode)
            {
                var detail = await res.Content.ReadAsStringAsync(ct);
                logger.LogWarning("메일 발송 실패. to={To} status={Status} body={Body}",
                    to, (int)res.StatusCode, detail);
            }

            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "메일 발송 중 오류. to={To}", to);
            return false;
        }
    }
}
