using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 장비 기본 설정 응답 DTO
/// </summary>
public class DeviceConfigDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public int Volume { get; set; } = 50;
    public int Brightness { get; set; } = 80;
    public string? RebootTime { get; set; }
    public bool IsAutoPower { get; set; } = false;
    public string? PowerOnTime { get; set; }
    public string? PowerOffTime { get; set; }
}

/// <summary>
/// 장비 기본 설정 생성/수정 DTO (Upsert)
/// </summary>
public class DeviceConfigUpsertDto
{
    [Required(ErrorMessage = "장비 ID는 필수입니다.")]
    public string DeviceId { get; set; } = string.Empty;

    [Range(0, 100)]
    public int Volume { get; set; } = 50;

    [Range(0, 100)]
    public int Brightness { get; set; } = 80;

    [MaxLength(5)]
    public string? RebootTime { get; set; }

    public bool IsAutoPower { get; set; } = false;

    [MaxLength(5)]
    public string? PowerOnTime { get; set; }

    [MaxLength(5)]
    public string? PowerOffTime { get; set; }
}
