namespace HelpDeskServer.Models
{
    /// <summary>시스템 운영전환 체크리스트</summary>
    public class Checklist : BaseEntity
    {
        /// <summary>분류 (예: Network, Server, Application)</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>점검 항목 명</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>점검 여부</summary>
        public bool IsChecked { get; set; }

        /// <summary>완료 일시</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>비고</summary>
        public string? Note { get; set; }

        /// <summary>정렬 순서</summary>
        public int SortOrder { get; set; }
    }
}
