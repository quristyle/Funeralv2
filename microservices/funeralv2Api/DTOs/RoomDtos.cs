using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 호실 정보 응답 DTO
/// </summary>
public class RoomDto
{
    public string Id { get; set; } = string.Empty;
    public string BuildingId { get; set; } = string.Empty;
    public string FloorId { get; set; } = string.Empty;
    public string? FloorName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? Remark { get; set; }
}

/// <summary>
/// 호실 생성 DTO
/// </summary>
public class RoomCreateDto
{
    [Required(ErrorMessage = "건물 ID는 필수입니다.")]
    public string BuildingId { get; set; } = string.Empty;

    [Required(ErrorMessage = "층 ID는 필수입니다.")]
    public string FloorId { get; set; } = string.Empty;

    [Required(ErrorMessage = "호실 명칭은 필수입니다.")]
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }

    [Required(ErrorMessage = "호실 타입은 필수입니다.")]
    public string RoomType { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? Remark { get; set; }
}

/// <summary>
/// 호실 수정 DTO
/// </summary>
public class RoomUpdateDto
{
    [Required(ErrorMessage = "건물 ID는 필수입니다.")]
    public string BuildingId { get; set; } = string.Empty;

    [Required(ErrorMessage = "층 ID는 필수입니다.")]
    public string FloorId { get; set; } = string.Empty;

    [Required(ErrorMessage = "호실 명칭은 필수입니다.")]
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }

    [Required(ErrorMessage = "호실 타입은 필수입니다.")]
    public string RoomType { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? Remark { get; set; }
}
