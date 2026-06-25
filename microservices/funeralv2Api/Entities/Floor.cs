using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 층 엔티티
/// </summary>
[Table("floors", Schema = "smfr")]
public class Floor : BaseEntity<string>
{
    public Floor()
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
    /// 층 명칭
    /// </summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;



    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Required]
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>
    /// 비고/설명
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }

    // 네비게이션 프로퍼티
    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }
}
