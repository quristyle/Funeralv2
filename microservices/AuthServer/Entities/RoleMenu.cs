using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 역할 - 메뉴 세부 권한 매핑 엔티티
/// </summary>
[Table("role_menus", Schema = "scom")]
public class RoleMenu : BaseEntity<int>
{
    [Required]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [ForeignKey(nameof(RoleId))]
    public virtual Role? Role { get; set; }

    [Required]
    [Column("menu_id")]
    public string MenuId { get; set; } = string.Empty;

    [ForeignKey(nameof(MenuId))]
    public virtual SystemMenu? SystemMenu { get; set; }

    [Column("can_view")]
    public bool CanView { get; set; } = false;

    [Column("can_search")]
    public bool CanSearch { get; set; } = false;

    [Column("can_create")]
    public bool CanCreate { get; set; } = false;

    [Column("can_delete")]
    public bool CanDelete { get; set; } = false;

    [Column("can_update")]
    public bool CanUpdate { get; set; } = false;

    [Column("can_print")]
    public bool CanPrint { get; set; } = false;

    [Column("can_excel")]
    public bool CanExcel { get; set; } = false;

    [Column("can_cust1")]
    public bool CanCust1 { get; set; } = false;

    [Column("can_cust2")]
    public bool CanCust2 { get; set; } = false;

    [Column("can_cust3")]
    public bool CanCust3 { get; set; } = false;

    [Column("can_cust4")]
    public bool CanCust4 { get; set; } = false;

    [Column("can_cust5")]
    public bool CanCust5 { get; set; } = false;

    [Column("can_cust6")]
    public bool CanCust6 { get; set; } = false;

    [Column("can_cust7")]
    public bool CanCust7 { get; set; } = false;

    [Column("can_cust8")]
    public bool CanCust8 { get; set; } = false;
}
