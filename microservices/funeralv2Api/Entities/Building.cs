using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 건물 엔티티
/// </summary>
[Table("buildings", Schema = "smfr")]
public class Building : BaseEntity<string>
{
    public Building()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 소속 회사 ID
    /// </summary>
    [Required]
    [Column("company_id")]
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>
    /// 건물명
    /// </summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 짧은건물명
    /// </summary>
    [Column("short_name")]
    public string? ShortName { get; set; }

    /// <summary>
    /// 건물 약어 (3자리 영문 대문자)
    /// </summary>
    [Column("abbreviation")]
    [MaxLength(3)]
    //[RegularExpression("^[A-Z]{3}$")]
    public string? Abbreviation { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Required]
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>
    /// 주소
    /// </summary>
    [Column("address")]
    public string? Address { get; set; }

    /// <summary>
    /// 우편번호
    /// </summary>
    [Column("zip_code")]
    public string? ZipCode { get; set; }

    /// <summary>
    /// 상세주소
    /// </summary>
    [Column("address_detail")]
    public string? AddressDetail { get; set; }

    /// <summary>
    /// 비고/설명
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>
    /// 건물 전경 사진 파일그룹 ID
    /// </summary>
    [Column("building_photo_group_id")]
    public string? BuildingPhotoGroupId { get; set; }

    /// <summary>
    /// 주차장 안내 이미지 파일그룹 ID
    /// </summary>
    [Column("parking_photo_group_id")]
    public string? ParkingPhotoGroupId { get; set; }
}
