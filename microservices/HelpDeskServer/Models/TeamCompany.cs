using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models;
   
    /// <summary>팀-회사 관계 (N:N)</summary>
    public class TeamCompany
    {
        /// <summary>팀 ID</summary>
        public int TeamId { get; set; }
        /// <summary>
        /// 팀 (Navigation property)
        /// </summary>
        public Team? Team { get; set; }

        /// <summary>
        /// 고객사 식별자 — 포털(<c>scom.companies.id</c>)의 값이다.
        /// 헬프데스크에는 회사 표가 없으므로 탐색 속성도 없다.
        /// </summary>
        public string CompanyId { get; set; } = string.Empty;
    }
