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
}

public sealed class NotificationUnreadDto
{
    public int Unread { get; set; }
}
