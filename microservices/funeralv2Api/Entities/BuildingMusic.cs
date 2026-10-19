using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>건물에 배정한 음원. 음원 목록은 건물끼리 공유하고, 실제로 트는 건물별 연결을 담는다.</summary>
[Table("building_music")]
public class BuildingMusic
{
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>대상 건물</summary>
    [Required]
    [Column("building_id")]
    [MaxLength(50)]
    public string BuildingId { get; set; } = string.Empty;

    /// <summary>배정한 음원 (<c>smfr.media_sources</c> 의 AUDIO 행)</summary>
    [Required]
    [Column("media_source_id")]
    [MaxLength(50)]
    public string MediaSourceId { get; set; } = string.Empty;

    /// <summary>건물 안에서의 재생 순서</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
