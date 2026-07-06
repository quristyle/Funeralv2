using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 건물 정보 응답 DTO
/// </summary>
public class BuildingDto
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Abbreviation { get; set; }
    public int SortOrder { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }
    public string? AddressDetail { get; set; }
    public string? Remark { get; set; }
    public string? BuildingPhotoGroupId { get; set; } // 건물전경사진 파일그룹 ID
    public string? ParkingPhotoGroupId { get; set; }  // 주차장안내이미지 파일그룹 ID
    public List<string> BuildingPhotos { get; set; } = new(); // 건물전경사진 썸네일 URL 목록
    public List<string> ParkingPhotos { get; set; } = new();  // 주차장안내이미지 썸네일 URL 목록
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 건물 생성 DTO
/// </summary>
public class BuildingCreateDto
{
    [Required(ErrorMessage = "회사 ID는 필수입니다.")]
    public string CompanyId { get; set; } = string.Empty;

    [Required(ErrorMessage = "건물명은 필수입니다.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "약어는 필수입니다.")] 
    public string? Abbreviation { get; set; }

    public string? ShortName { get; set; }
    public int SortOrder { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }
    public string? AddressDetail { get; set; }
    public string? Remark { get; set; }
    public string? BuildingPhotoGroupId { get; set; } // 건물전경사진 파일그룹 ID
    public string? ParkingPhotoGroupId { get; set; }  // 주차장안내이미지 파일그룹 ID
}

/// <summary>
/// 건물 수정 DTO
/// </summary>
public class BuildingUpdateDto
{
    [Required(ErrorMessage = "건물명은 필수입니다.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "약어는 필수입니다.")]
    public string? Abbreviation { get; set; }

    public string? ShortName { get; set; }
    public int SortOrder { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }
    public string? AddressDetail { get; set; }
    public string? Remark { get; set; }
    public string? BuildingPhotoGroupId { get; set; } // 건물전경사진 파일그룹 ID
    public string? ParkingPhotoGroupId { get; set; }  // 주차장안내이미지 파일그룹 ID
}
