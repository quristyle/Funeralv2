using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>SMTP 직발송. 큐 방식(<see cref="IEmailQueueSender"/>)과 달리 성공·실패를 바로 안다.</summary>
public interface IEmailSender
{
    /// <param name="to">받는 사람. 여럿이면 쉼표나 세미콜론으로 잇는다.</param>
    /// <param name="subject">제목.</param>
    /// <param name="body">본문. <paramref name="html"/> 값에 따라 HTML 이거나 평문이다.</param>
    /// <param name="html">참이면 본문을 HTML 로 보낸다. 거짓이면 평문.</param>
    /// <param name="attachments">
    /// 붙일 파일들. <b>선택이다</b> — 안 주면 예전처럼 본문만 보낸다.
    /// 내용은 base64 이고, 형식이 비면 <c>application/octet-stream</c> 이 된다.
    /// </param>
    Task SendAsync(
        string to, string subject, string body, bool html = false,
        IReadOnlyList<EmailAttachmentDto>? attachments = null);
}
