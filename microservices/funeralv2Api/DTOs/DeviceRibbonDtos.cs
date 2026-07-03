using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 장비 리본 설정 응답 DTO
/// </summary>
public class DeviceRibbonDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string MediaSourceId { get; set; } = string.Empty;

    // 장식 이미지 정보 (MediaSource 조인)
    public string? MediaSourceName { get; set; }
    public string? MediaSourceUrl { get; set; }
    public string? MediaSourceThumbnailUrl { get; set; }

    // 위치 및 크기 (%, 소수점 3자리)
    public decimal PositionLeft { get; set; } = 0;
    public decimal PositionTop { get; set; } = 0;
    public decimal Width { get; set; } = 10;
    public decimal Height { get; set; } = 10;

    public int SortOrder { get; set; } = 0;
    public string? Remark { get; set; }
}

/// <summary>
/// 장비 리본 설정 생성/수정 DTO
/// </summary>
public class DeviceRibbonUpsertDto
{
    [Required(ErrorMessage = "장비 ID는 필수입니다.")]
    public string DeviceId { get; set; } = string.Empty;

    [Required(ErrorMessage = "장식(미디어소스) ID는 필수입니다.")]
    public string MediaSourceId { get; set; } = string.Empty;

    /// <summary>좌측 위치 (%, 소수점 3자리, 0~100)</summary>
    [Range(0, 100)]
    public decimal PositionLeft { get; set; } = 0;

    /// <summary>상단 위치 (%, 소수점 3자리, 0~100)</summary>
    [Range(0, 100)]
    public decimal PositionTop { get; set; } = 0;

    /// <summary>너비 (%, 소수점 3자리, 1~100)</summary>
    [Range(1, 100)]
    public decimal Width { get; set; } = 10;

    /// <summary>높이 (%, 소수점 3자리, 1~100)</summary>
    [Range(1, 100)]
    public decimal Height { get; set; } = 10;

    public int SortOrder { get; set; } = 0;
    public string? Remark { get; set; }
}

/// <summary>
/// 장비 리본 일괄 저장 DTO - 장비의 전체 리본 목록을 한 번에 저장
/// </summary>
public class DeviceRibbonBulkSaveDto
{
    [Required(ErrorMessage = "장비 ID는 필수입니다.")]
    public string DeviceId { get; set; } = string.Empty;

    public List<DeviceRibbonUpsertDto> Ribbons { get; set; } = new();
}
