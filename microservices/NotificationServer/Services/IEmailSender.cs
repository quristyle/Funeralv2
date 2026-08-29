namespace NotificationServer.Services;

/// <summary>SMTP 직발송. 큐 방식(<see cref="IEmailQueueSender"/>)과 달리 성공·실패를 바로 안다.</summary>
public interface IEmailSender
{
    /// <param name="html">참이면 본문을 HTML 로 보낸다. 거짓이면 평문.</param>
    Task SendAsync(string to, string subject, string body, bool html = false);
}
