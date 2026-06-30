using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 장비 정보 응답 DTO
/// </summary>
public class DeviceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "DID";
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string Status { get; set; } = "UNKNOWN";

    public int SortOrder { get; set; }

    public string? CompanyId { get; set; }
    public string? BuildingId { get; set; }
    public string? FloorId { get; set; }
    public string? RoomId { get; set; }
    
    // For display
    public string? CompanyName { get; set; }
    public string? BuildingName { get; set; }
    public string? FloorName { get; set; }
    public string? RoomName { get; set; }
    public string? BuildingShortName { get; set; }
    public string? FloorShortName { get; set; }
    public string? RoomShortName { get; set; }
    public string? VideoId { get; set; }
    public string? MusicId { get; set; }
    public bool IsVideoEnabled { get; set; }
    public bool IsMusicEnabled { get; set; }
    public string? VideoName { get; set; }
    public string? MusicName { get; set; }
    public bool IsMemorialPhotoEnabled { get; set; }
    public bool IsDeceasedNameVisible { get; set; }
    public bool IsFamilyContactVisible { get; set; }
    public double MusicVolume { get; set; }
}

/// <summary>
/// 장비 생성 DTO
/// </summary>
public class DeviceCreateDto
{
    [Required(ErrorMessage = "장비명은 필수입니다.")]
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }

    public string DeviceType { get; set; } = "DID";
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string Status { get; set; } = "UNKNOWN";
    public int SortOrder { get; set; }
    
    [Required]
    public string? CompanyId { get; set; }

    // A device can be associated with a building, a floor, or a room.
    public string? BuildingId { get; set; }
    public string? FloorId { get; set; }
    public string? RoomId { get; set; }
}

/// <summary>
/// 장비 수정 DTO
/// </summary>
public class DeviceUpdateDto
{
    [Required(ErrorMessage = "장비명은 필수입니다.")]
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }

    public string DeviceType { get; set; } = "DID";
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string Status { get; set; } = "UNKNOWN";
    public int SortOrder { get; set; }

    [Required]
    public string? CompanyId { get; set; }

    public string? BuildingId { get; set; }
    public string? FloorId { get; set; }
    public string? RoomId { get; set; }
}
