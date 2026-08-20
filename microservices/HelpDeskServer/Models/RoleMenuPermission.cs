using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Models
{
    /// <summary>역할별 메뉴 상세 권한 엔티티</summary>
    public class RoleMenuPermission : BaseEntity
    {
        public int RoleId { get; set; }
        public int MenuId { get; set; }

        public bool CanCreate { get; set; }
        public bool CanRead { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDelete { get; set; }

        // 확장 권한 1~8
        public bool Ext1 { get; set; }
        public bool Ext2 { get; set; }
        public bool Ext3 { get; set; }
        public bool Ext4 { get; set; }
        public bool Ext5 { get; set; }
        public bool Ext6 { get; set; }
        public bool Ext7 { get; set; }
        public bool Ext8 { get; set; }

        [ForeignKey("RoleId")]
        public virtual AppRole? Role { get; set; }

        [ForeignKey("MenuId")]
        public virtual Menu? Menu { get; set; }
    }
}
