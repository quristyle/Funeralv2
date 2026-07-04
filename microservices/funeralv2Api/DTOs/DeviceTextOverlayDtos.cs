using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 장비 텍스트 오버레이 응답 DTO
/// </summary>
public class DeviceTextOverlayDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public decimal FontSize { get; set; }
    public string FontColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "transparent";
    public string TextAlign { get; set; } = "center";
    public string FontWeight { get; set; } = "normal";
    public decimal PositionLeft { get; set; }
    public decimal PositionTop { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 장비 텍스트 오버레이 생성/수정 DTO
/// </summary>
public class DeviceTextOverlayUpsertDto
{
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string TextContent { get; set; } = string.Empty;

    public decimal FontSize { get; set; } = 3;
    public string FontColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "transparent";
    public string TextAlign { get; set; } = "center";
    public string FontWeight { get; set; } = "normal";
    public decimal PositionLeft { get; set; }
    public decimal PositionTop { get; set; }
    public decimal Width { get; set; } = 30;
    public decimal Height { get; set; } = 10;
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 장비 텍스트 오버레이 일괄 저장 DTO
/// </summary>
public class DeviceTextOverlayBulkSaveDto
{
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    public List<DeviceTextOverlayUpsertDto> Overlays { get; set; } = new();
}
