using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>
/// 레이아웃 상단 도구 아이콘 목록 및 기본 사용 여부 엔티티 클래스
/// </summary>
[Table("layout_header_icons", Schema = "scom")]
public class LayoutHeaderIcon
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Required]
    [Column("icon_key")]
    public string IconKey { get; set; } = string.Empty;

    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("icon")]
    public string Icon { get; set; } = string.Empty;

    [Column("route_key")]
    public string? RouteKey { get; set; }

    [Column("path")]
    public string? Path { get; set; }

    [Column("default_enabled")]
    public bool DefaultEnabled { get; set; } = true;

    [Column("order_no")]
    public int OrderNo { get; set; } = 0;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
