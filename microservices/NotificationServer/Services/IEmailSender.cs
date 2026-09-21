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
    /// <param name="textBody">
    /// HTML 과 <b>같은 내용의 평문 갈래</b>. <paramref name="html"/> 가 참일 때만 쓴다.
    ///
    /// <para>
    /// HTML 만 실은 메일은 평문으로만 읽는 클라이언트에서 <b>빈 칸</b>이 되고
    /// 스팸 점수도 올라간다. 평문 원본을 이미 들고 있는 자리
    /// (<see cref="NoticeEmailTemplate"/> 를 거친 알림)에서는 안 실을 이유가 없다.
    /// 만들 것이 없으면 안 줘도 된다 — 그때는 예전처럼 HTML 한 갈래만 나간다.
    /// </para>
    /// </param>
    Task SendAsync(
        string to, string subject, string body, bool html = false,
        IReadOnlyList<EmailAttachmentDto>? attachments = null,
        string? textBody = null);
}
