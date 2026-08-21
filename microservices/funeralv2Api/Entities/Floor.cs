using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 건물 내 층(Floor) 정보 엔티티 클래스
/// </summary>
[Table("floors", Schema = "smfr")]
public class Floor : BaseEntity<string>
{
    /// <summary>
    /// Floor 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public Floor()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 소속 건물 식별자 (ID)
    /// </summary>
    [Required]
    [Column("building_id")]
    public string BuildingId { get; set; } = string.Empty;

    /// <summary>
    /// 층 명칭 (예: 1F, B1 등)
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
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>
    /// 소속 건물 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }
}
