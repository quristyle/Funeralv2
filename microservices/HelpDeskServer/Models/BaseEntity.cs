using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models
{
    /// <summary>공통 베이스 엔티티 (생성/수정 이력 관리)</summary>
    public abstract class BaseEntity
    {
        /// <summary>PK</summary>
        public int Id { get; set; }

        /// <summary>생성자 (로그인 ID)</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>생성일시</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>수정자 (로그인 ID)</summary>
        public string? ModifiedBy { get; set; }

        /// <summary>수정일시</summary>
        public DateTime? ModifiedAt { get; set; }

        /// <summary>작업 서비스</summary>
        public string? ActionService { get; set; }
        /// <summary>작업 메뉴/화면 정보</summary>
        public string? MenuContext { get; set; }
        /// <summary>접근아이피</summary>
        public string? RemoteAddr { get; set; }
        /// <summary>접근 클라이언트 정보 (User-Agent 등)</summary>
        public string? RemoteMchineInfo { get; set; }
    }




}
