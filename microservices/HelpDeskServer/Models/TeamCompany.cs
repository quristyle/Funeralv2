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

        /// <summary>고객사 ID</summary>
        public int CompanyId { get; set; }
        /// <summary>
        /// 고객사 (Navigation property)
        /// </summary>
        public CustomerCompany? Company { get; set; }
    }
