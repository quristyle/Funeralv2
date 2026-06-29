using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 호실 엔티티
/// </summary>
[Table("rooms", Schema = "smfr")]
public class Room : BaseEntity<string>
{
    public Room()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 소속 건물 ID
    /// </summary>
    [Required]
    [Column("building_id")]
    public string BuildingId { get; set; } = string.Empty;

    /// <summary>
    /// 소속 층 ID
    /// </summary>
    [Required]
    [Column("floor_id")]
    public string FloorId { get; set; } = string.Empty;

    /// <summary>
    /// 호실 명칭
    /// </summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 짧은 명칭
    /// </summary>
    [Column("short_name")]
    public string? ShortName { get; set; }

    /// <summary>
    /// 호실 타입 (예: 빈소, 안치실, 참관실 등)
    /// </summary>
    [Required]
    [Column("room_type")]
    public string RoomType { get; set; } = string.Empty;

    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Required]
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>
    /// 상태 (ACTIVE, INACTIVE)
    /// </summary>
    [Required]
    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// 비고/설명
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }

    // 네비게이션 프로퍼티
    [ForeignKey(nameof(FloorId))]
    public Floor? Floor { get; set; }
}
