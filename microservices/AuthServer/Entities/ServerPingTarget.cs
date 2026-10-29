using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>
/// 내부 서버 Ping 감시 대상 장비 엔티티
/// </summary>
[Table("server_ping_targets", Schema = "scom")]
public class ServerPingTarget
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("ip")]
    public string Ip { get; set; } = string.Empty;

    [Column("timeout_ms")]
    public int TimeoutMs { get; set; } = 2000;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = "admin";

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    [Column("updated_by")]
    public string UpdatedBy { get; set; } = "admin";

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
