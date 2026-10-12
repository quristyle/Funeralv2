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
/// [받는 사람의 정본은 <b>포털관리 「알림관리」</b>다 (2026-10-12)]
/// </para>
///
/// <para>
/// 역할 → 이메일 명단은 scom 을 가진 NotificationServer 가 푼다 — 이 서비스는
/// <c>eventCode = SITE_INQUIRY</c> 만 실어 보내고, 받을 역할은
/// <c>/admin/system/notify-policy</c> 의 <c>SITE_INQUIRY</c> 줄이 정한다
/// (<c>docs/notify-policy.md</c>).
/// </para>
///
/// <para>
/// <b>설정 파일에 역할을 적어 두지 않는다.</b> 한동안 여기 기본값
/// (<c>InquiryMail:ToRole</c> = SYSTEM_ADMINISTRATOR)이 있었는데, 그러면
/// 화면에서 체크를 <b>다 풀어도</b> 정책이 「제한 없음」이 되어 이 기본값으로
/// 되돌아간다 — 관리자가 끈 알림이 계속 가는 쪽이다. 그래서 그 칸을 비우고
/// 표로 옮겼다. 적어 두면 <b>표가 비었을 때만</b> 쓰이는 비상 통로로 남는다.
/// </para>
///
/// <para>
/// 역할 밖의 고정 수신자(협력사 주소 따위)가 필요하면 <c>InquiryMail:To</c> 에
/// 쉼표로 잇는다 — 그쪽은 역할이 아니라 주소라 정책이 가리지 않는다.
/// </para>
/// </remarks>
public class InquiryMailNotifier(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<InquiryMailNotifier> logger) : IInquiryMailNotifier
{
    public async Task NotifyAsync(Guid inquiryId, InquiryRequestDto request, CancellationToken ct = default)
    {
        // **설정이 비어도 보낸다.** 받을 역할을 정하는 자리는 이제 포털관리의
        // 「알림관리」(`SITE_INQUIRY`)이고, 여기 둘은 거기에 아무것도 안 걸렸을
        // 때만 쓰이는 비상 통로다(머리말). 비었다고 여기서 되돌아서면 화면에서
        // 역할을 켜 두어도 메일이 한 통도 안 나간다 — **화면이 거짓말을 한다.**
        var toRole = configuration["InquiryMail:ToRole"];
        var to = configuration["InquiryMail:To"];

        var (subject, body) = InquiryEmailTemplates.Received(inquiryId, request);

        // 메일 실패가 접수 실패가 되면 안 된다 — 결과는 로그로만 남긴다.
        //
        // **받는 역할은 알림관리가 정한다.** `eventCode` 를 실어 보내면 알림
        // 서버가 `SITE_INQUIRY` 정책의 「이메일」을 켠 역할로 보낸다. 거기에
        // 한 줄도 없고 아래 둘도 비어 있으면 받는 사람이 0 이고, 알림 서버가
        // 400 으로 그렇게 말해 준다(조용히 성공한 척하지 않는다).
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
