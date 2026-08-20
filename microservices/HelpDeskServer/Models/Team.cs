

using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models;


    /// <summary>팀</summary>
    public class Team : BaseEntity
    {
        /// <summary>팀 명</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 이 팀에 속한 관리자 목록 (N:N 관계)
        /// </summary>
        public ICollection<AdminTeam> AdminTeams { get; set; } = new List<AdminTeam>();
        /// <summary>
        /// 이 팀과 연결된 고객사 목록 (N:N 관계)
        /// </summary>
        public ICollection<TeamCompany> TeamCompanies { get; set; } = new List<TeamCompany>();
        // public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
