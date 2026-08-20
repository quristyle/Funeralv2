using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Models
{
    /// <summary>메뉴별 권한 매핑 엔티티</summary>
    public class MenuRole : BaseEntity
    {
        /// <summary>메뉴 ID</summary>
        public int MenuId { get; set; }

        /// <summary>권한명 (admin, customer 등)</summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>연결된 메뉴 객체</summary>
        [ForeignKey("MenuId")]
        public virtual Menu? Menu { get; set; }
    }
}
