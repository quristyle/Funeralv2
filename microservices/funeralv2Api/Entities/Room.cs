using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 호실(빈소, 안치실, 참관실 등) 정보 엔티티 클래스
/// </summary>
[Table("rooms", Schema = "smfr")]
public class Room : BaseEntity<string>
{
    /// <summary>
    /// Room 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public Room()
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
    /// 소속 층 식별자 (ID)
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
    /// 호실 약칭
    /// </summary>
    [Column("short_name")]
    public string? ShortName { get; set; }

    /// <summary>
    /// 호실 유형 (예: 빈소, 안치실, 참관실, 영결식장 등)
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
    /// 사용 상태 (예: ACTIVE, INACTIVE 등, 기본값: ACTIVE)
    /// </summary>
    [Required]
    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>
    /// 소속 층 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(FloorId))]
    public Floor? Floor { get; set; }
}
