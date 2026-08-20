namespace HelpDeskServer.Dtos
{
    public class ChecklistCreateDto
    {
        public string Category { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Note { get; set; }
    }

    public class ChecklistUpdateDto
    {
        public string Category { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Note { get; set; }
        public int SortOrder { get; set; }
    }
}
