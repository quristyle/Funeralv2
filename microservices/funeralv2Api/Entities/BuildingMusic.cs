using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 건물에 배정한 음원.
/// </summary>
/// <remarks>
/// 옛 <c>smfr.t_music_build</c>(<c>ms_seq</c> · <c>b_key</c> 두 칸뿐)에 해당한다.
/// 음원 목록은 모든 건물이 공유하지만 실제로 트는 것은 건물마다 다르다 —
/// 그 연결을 담는다.
/// </remarks>
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
