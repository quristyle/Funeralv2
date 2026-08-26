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

        /// <summary>
        /// FileServer 가 발급한 파일 아이디. 채워져 있으면 이 첨부는 FileServer 로 옮겨진 것이다.
        /// </summary>
        /// <remarks>
        /// FileServer 라는 전용 서비스가 있는데도 헬프데스크가 첨부를 따로 관리하고 있었다
        /// (결정 D5-B). 새 업로드는 FileServer 로 바로 가고, 기존 37건은
        /// <c>deploy/attachment-migration/migrate.py</c> 가 옮긴다.
        ///
        /// <para>
        /// <b><see cref="FilePath"/>·<see cref="StoredFileName"/> 은 지우지 않는다.</b>
        /// 되돌릴 근거이고, 아직 옮기지 않은 행은 그 값으로 내려받아야 한다.
        /// </para>
        /// </remarks>
        public string? FileId { get; set; }

        /// <summary>FileServer 로 옮긴 시각. 옮기지 않았으면 비어 있다.</summary>
        public DateTime? MigratedAt { get; set; }
    }

    }