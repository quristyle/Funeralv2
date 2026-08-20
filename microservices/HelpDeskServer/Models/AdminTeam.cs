using System;

namespace HelpDeskServer.Models
{
    /// <summary>관리자-팀 매핑 (N:N 관계)</summary>
    public class AdminTeam
    {
        /// <summary>관리자 ID</summary>
        public int AdminId { get; set; }
        /// <summary>관리자 (Navigation property)</summary>
        public Admin? Admin { get; set; }

        /// <summary>팀 ID</summary>
        public int TeamId { get; set; }
        /// <summary>팀 (Navigation property)</summary>
        public Team? Team { get; set; }
    }
}
