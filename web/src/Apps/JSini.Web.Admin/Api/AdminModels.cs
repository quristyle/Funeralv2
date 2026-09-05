namespace JSini.Web.Admin.Api;

public sealed class NoticeDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPopup { get; set; }
    public bool IsPublic { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AuthorName { get; set; }
}

public sealed class SaveNoticeDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPopup { get; set; }
    public bool IsPublic { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; } = 1;
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}
