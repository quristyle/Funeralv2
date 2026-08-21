using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 역할 - 메뉴 세부 권한 매핑 엔티티 클래스
/// </summary>
[Table("role_menus", Schema = "scom")]
public class RoleMenu : BaseEntity<int>
{
    /// <summary>
    /// 연관된 역할 식별자 (ID)
    /// </summary>
    [Required]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 역할 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(RoleId))]
    public virtual Role? Role { get; set; }

    /// <summary>
    /// 연관된 시스템 메뉴 식별자 (ID)
    /// </summary>
    [Required]
    [Column("menu_id")]
    public string MenuId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 시스템 메뉴 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(MenuId))]
    public virtual SystemMenu? SystemMenu { get; set; }

    /// <summary>
    /// 조회(보기) 권한 여부
    /// </summary>
    [Column("can_view")]
    public bool CanView { get; set; } = false;

    /// <summary>
    /// 검색 권한 여부
    /// </summary>
    [Column("can_search")]
    public bool CanSearch { get; set; } = false;

    /// <summary>
    /// 생성(등록) 권한 여부
    /// </summary>
    [Column("can_create")]
    public bool CanCreate { get; set; } = false;

    /// <summary>
    /// 삭제 권한 여부
    /// </summary>
    [Column("can_delete")]
    public bool CanDelete { get; set; } = false;

    /// <summary>
    /// 수정(업데이트) 권한 여부
    /// </summary>
    [Column("can_update")]
    public bool CanUpdate { get; set; } = false;

    /// <summary>
    /// 인쇄 권한 여부
    /// </summary>
    [Column("can_print")]
    public bool CanPrint { get; set; } = false;

    /// <summary>
    /// 엑셀 출력 권한 여부
    /// </summary>
    [Column("can_excel")]
    public bool CanExcel { get; set; } = false;

    /// <summary>
    /// 사용자 정의 권한 1 여부
    /// </summary>
    [Column("can_cust1")]
    public bool CanCust1 { get; set; } = false;

    /// <summary>
    /// 사용자 정의 권한 2 여부
    /// </summary>
    [Column("can_cust2")]
    public bool CanCust2 { get; set; } = false;

    /// <summary>
    /// 사용자 정의 권한 3 여부
    /// </summary>
    [Column("can_cust3")]
    public bool CanCust3 { get; set; } = false;

    /// <summary>
    /// 사용자 정의 권한 4 여부
    /// </summary>
    [Column("can_cust4")]
    public bool CanCust4 { get; set; } = false;

    /// <summary>
    /// 사용자 정의 권한 5 여부
    /// </summary>
    [Column("can_cust5")]
    public bool CanCust5 { get; set; } = false;

    /// <summary>
    /// 사용자 정의 권한 6 여부
    /// </summary>
    [Column("can_cust6")]
    public bool CanCust6 { get; set; } = false;

    /// <summary>
    /// 사용자 정의 권한 7 여부
    /// </summary>
    [Column("can_cust7")]
    public bool CanCust7 { get; set; } = false;

    /// <summary>
    /// 사용자 정의 권한 8 여부
    /// </summary>
    [Column("can_cust8")]
    public bool CanCust8 { get; set; } = false;
}
