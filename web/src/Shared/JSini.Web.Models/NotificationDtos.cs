namespace JSini.Web.Models;

public sealed class NotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool IsRead { get; set; }
    public System.DateTime? CreatedAt { get; set; }
    public string? Category { get; set; }
    public string? Url { get; set; }
    public bool Delivered { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>앱 알림이 띄웠던 아이콘 주소(보낼 때 기록한 값). 사람을 지목하면 그 사람의 사진, 아니면 null 이다. 열쇠(?t=)는 떼어 둔다. 붙은 채로 두면 한 시간 뒤부터 그림자로 떨어진다. 칸이 생기기 전에 보낸 줄은 비어 있다.</summary>
    public string? Icon { get; set; }
}

public sealed class NotificationUnreadDto
{
    public int Unread { get; set; }
}
