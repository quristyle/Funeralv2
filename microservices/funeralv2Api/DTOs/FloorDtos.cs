using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 층 정보 응답 DTO
/// </summary>
public class FloorDto
{
    public string Id { get; set; } = string.Empty;
    public string BuildingId { get; set; } = string.Empty;
    public string? BuildingName { get; set; }
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 층 생성 DTO
/// </summary>
public class FloorCreateDto
{
    [Required(ErrorMessage = "건물 ID는 필수입니다.")]
    public string BuildingId { get; set; } = string.Empty;

    [Required(ErrorMessage = "층 명칭은 필수입니다.")]
    public string Name { get; set; } = string.Empty;



    [Required(ErrorMessage = "정렬 순서는 필수입니다.")]
    public int SortOrder { get; set; }

    public string? Remark { get; set; }
}

/// <summary>
/// 층 수정 DTO
/// </summary>
public class FloorUpdateDto
{
    [Required(ErrorMessage = "층 명칭은 필수입니다.")]
    public string Name { get; set; } = string.Empty;



    [Required(ErrorMessage = "정렬 순서는 필수입니다.")]
    public int SortOrder { get; set; }

    public string? Remark { get; set; }
}
