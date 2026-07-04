using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 장비 텍스트 오버레이 설정 엔티티
/// 장비(모니터) 화면의 특정 위치에 텍스트를 배치하는 설정을 관리합니다.
/// 위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.
/// </summary>
[Table("device_text_overlays", Schema = "smfr")]
public class DeviceTextOverlay : BaseEntity<string>
{
    public DeviceTextOverlay()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>장비 FK</summary>
    [Required]
    [Column("device_id")]
    [MaxLength(50)]
    public string DeviceId { get; set; } = string.Empty;

    [ForeignKey(nameof(DeviceId))]
    public Device? Device { get; set; }

    /// <summary>표시할 텍스트 내용</summary>
    [Required]
    [Column("text_content")]
    [MaxLength(500)]
    public string TextContent { get; set; } = string.Empty;

    /// <summary>폰트 크기 (px 단위 기준, 화면 높이 대비 %로 저장)</summary>
    [Column("font_size", TypeName = "decimal(6,3)")]
    public decimal FontSize { get; set; } = 3;

    /// <summary>폰트 색상 (CSS hex 색상값, 예: #FFFFFF)</summary>
    [Column("font_color")]
    [MaxLength(20)]
    public string FontColor { get; set; } = "#FFFFFF";

    /// <summary>배경 색상 (CSS hex 색상값 또는 'transparent')</summary>
    [Column("background_color")]
    [MaxLength(30)]
    public string BackgroundColor { get; set; } = "transparent";

    /// <summary>텍스트 정렬 (left | center | right)</summary>
    [Column("text_align")]
    [MaxLength(10)]
    public string TextAlign { get; set; } = "center";

    /// <summary>폰트 굵기 (normal | bold)</summary>
    [Column("font_weight")]
    [MaxLength(10)]
    public string FontWeight { get; set; } = "normal";

    /// <summary>좌측 위치 (%, 소수점 3자리)</summary>
    [Column("position_left", TypeName = "decimal(6,3)")]
    public decimal PositionLeft { get; set; } = 0;

    /// <summary>상단 위치 (%, 소수점 3자리)</summary>
    [Column("position_top", TypeName = "decimal(6,3)")]
    public decimal PositionTop { get; set; } = 0;

    /// <summary>너비 (%, 소수점 3자리)</summary>
    [Column("width", TypeName = "decimal(6,3)")]
    public decimal Width { get; set; } = 30;

    /// <summary>높이 (%, 소수점 3자리)</summary>
    [Column("height", TypeName = "decimal(6,3)")]
    public decimal Height { get; set; } = 10;

    /// <summary>정렬 순서</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    /// <summary>비고</summary>
    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }
}
