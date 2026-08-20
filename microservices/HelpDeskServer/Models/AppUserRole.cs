using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Models
{
    /// <summary>사용자별 권한 그룹 매핑 엔티티</summary>
    public class AppUserRole : BaseEntity
    {
        /// <summary>권한 그룹 ID</summary>
        public int RoleId { get; set; }

        /// <summary>사용자 타입 (admin, customer)</summary>
        public string UserType { get; set; } = string.Empty;

        /// <summary>사용자 ID (AdminId 또는 CustomerId)</summary>
        public int UserId { get; set; }

        /// <summary>연결된 권한 그룹 객체</summary>
        [ForeignKey("RoleId")]
        public virtual AppRole? Role { get; set; }
    }
}
