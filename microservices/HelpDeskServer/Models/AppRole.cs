using System.Collections.Generic;

namespace HelpDeskServer.Models
{
    /// <summary>권한 그룹(역할) 엔티티</summary>
    public class AppRole : BaseEntity
    {
        /// <summary>그룹명 (영문 식별자 권장)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>표시용 명칭</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>설명</summary>
        public string? Description { get; set; }

        /// <summary>정렬 순서</summary>
        public int SortOrder { get; set; }

        /// <summary>소속 사용자 매핑 목록</summary>
        public virtual ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
    }
}
