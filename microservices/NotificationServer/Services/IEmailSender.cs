using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>SMTP 직발송. 큐 방식(<see cref="IEmailQueueSender"/>)과 달리 성공·실패를 바로 안다.</summary>
public interface IEmailSender
{
    /// <summary>받는 사람은 쉼표·세미콜론으로 잇는다. 첨부는 base64, textBody 는 HTML 과 같은 내용의 평문 갈래다. cc 는 숨은참조가 아니다.</summary>
    Task SendAsync(
        string to, string subject, string body, bool html = false,
        IReadOnlyList<EmailAttachmentDto>? attachments = null,
        string? textBody = null,
        string? cc = null);
}
