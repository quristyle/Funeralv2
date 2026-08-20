using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Models
{
    /// <summary>메뉴 관리 엔티티</summary>
    public class Menu : BaseEntity
    {
        /// <summary>메뉴명</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>아이콘 (PrimeIcons)</summary>
        public string? Icon { get; set; }

        /// <summary>이동 경로 (내부 라우터)</summary>
        public string? To { get; set; }

        /// <summary>외부 URL</summary>
        public string? Url { get; set; }

        /// <summary>부모 메뉴 ID</summary>
        public int? ParentId { get; set; }

        /// <summary>정렬 순서</summary>
        public int SortOrder { get; set; }

        /// <summary>활성화 여부</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>메뉴 노출 여부</summary>
        public bool Visible { get; set; } = true;

        // 권한 사용 여부 설정
        public bool UseCreate { get; set; } = true;
        public bool UseRead { get; set; } = true;
        public bool UseUpdate { get; set; } = true;
        public bool UseDelete { get; set; } = true;

        public bool UseExt1 { get; set; }
        public string? Ext1Name { get; set; }
        public bool UseExt2 { get; set; }
        public string? Ext2Name { get; set; }
        public bool UseExt3 { get; set; }
        public string? Ext3Name { get; set; }
        public bool UseExt4 { get; set; }
        public string? Ext4Name { get; set; }
        public bool UseExt5 { get; set; }
        public string? Ext5Name { get; set; }
        public bool UseExt6 { get; set; }
        public string? Ext6Name { get; set; }
        public bool UseExt7 { get; set; }
        public string? Ext7Name { get; set; }
        public bool UseExt8 { get; set; }
        public string? Ext8Name { get; set; }

        /// <summary>허용된 권한 목록</summary>
        public virtual ICollection<MenuRole> MenuRoles { get; set; } = new List<MenuRole>();

        /// <summary>부모 메뉴 객체</summary>
        [ForeignKey("ParentId")]
        public virtual Menu? Parent { get; set; }

        /// <summary>자식 메뉴 목록</summary>
        public virtual ICollection<Menu> Children { get; set; } = new List<Menu>();
    }
}
