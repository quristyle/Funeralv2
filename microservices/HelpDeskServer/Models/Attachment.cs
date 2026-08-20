using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models
{
    /// <summary>공통 첨부파일</summary>
    public class Attachment : BaseEntity
    {
        /// <summary>연결된 엔티티 타입</summary>
        public string EntityType { get; set; } = string.Empty;  // "ImprovementRequest", "Team", "Company", "Comment"

        /// <summary>연결된 엔티티 PK</summary>
        public int EntityId { get; set; }

        /// <summary>원본 파일명</summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>저장 파일명</summary>
        public string StoredFileName { get; set; } = string.Empty;

        /// <summary>파일 경로</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>파일 타입</summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>파일 크기</summary>
        public long FileSize { get; set; }

        /// <summary>업로드 일시</summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    }