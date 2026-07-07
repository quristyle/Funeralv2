using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 장비 리본 설정 엔티티 클래스
/// 장비(모니터) 화면의 특정 위치에 장식(리본) 이미지를 배치하는 설정을 관리합니다.
/// 위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.
/// </summary>
[Table("device_ribbons", Schema = "smfr")]
public class DeviceRibbon : BaseEntity<string>
{
    /// <summary>
    /// DeviceRibbon 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public DeviceRibbon()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 장비 식별자 (ID)
    /// </summary>
    [Required]
    [Column("device_id")]
    [MaxLength(50)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 장비 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(DeviceId))]
    public Device? Device { get; set; }

    /// <summary>
    /// 장식(미디어소스) 식별자 (ID, MediaSource.SourceType = IMAGE)
    /// </summary>
    [Required]
    [Column("media_source_id")]
    [MaxLength(50)]
    public string MediaSourceId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 미디어 리소스 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(MediaSourceId))]
    public MediaSource? MediaSource { get; set; }

    /// <summary>
    /// 화면 내 좌측 위치 (%, 소수점 3자리)
    /// </summary>
    [Column("position_left", TypeName = "decimal(6,3)")]
    public decimal PositionLeft { get; set; } = 0;

    /// <summary>
    /// 화면 내 상단 위치 (%, 소수점 3자리)
    /// </summary>
    [Column("position_top", TypeName = "decimal(6,3)")]
    public decimal PositionTop { get; set; } = 0;

    /// <summary>
    /// 화면 내 표시 너비 (%, 소수점 3자리)
    /// </summary>
    [Column("width", TypeName = "decimal(6,3)")]
    public decimal Width { get; set; } = 10;

    /// <summary>
    /// 화면 내 표시 높이 (%, 소수점 3자리)
    /// </summary>
    [Column("height", TypeName = "decimal(6,3)")]
    public decimal Height { get; set; } = 10;

    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }
}
