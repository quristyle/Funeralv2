namespace HelpDeskServer.Models
{
    /// <summary>공지사항</summary>
    public class Notice : BaseEntity
    {
        /// <summary>제목</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>내용</summary>
        public string Content { get; set; } = string.Empty;
    }
}
