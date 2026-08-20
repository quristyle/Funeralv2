namespace HelpDeskServer.Models;

/// <summary>
/// 발송된 푸시 메시지의 내용을 저장하는 엔티티
/// </summary>
public class PushMessage : BaseEntity
{
    /// <summary>
    /// 메시지 제목
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 메시지 본문
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 클릭 시 이동할 URL
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 이 메시지를 수신한 사용자 목록
    /// </summary>
    public virtual ICollection<PushMessageRecipient> Recipients { get; set; } = new List<PushMessageRecipient>();
}
