using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 건물(시설물) 정보 엔티티 클래스
/// </summary>
[Table("buildings", Schema = "smfr")]
public class Building : BaseEntity<string>
{
    /// <summary>
    /// Building 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public Building()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 소속 회사 식별자 (ID)
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
    /// 건물 약칭
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
    /// 건물 주소
    /// </summary>
    [Column("address")]
    public string? Address { get; set; }

    /// <summary>
    /// 우편번호
    /// </summary>
    [Column("zip_code")]
    public string? ZipCode { get; set; }

    /// <summary>
    /// 상세 주소
    /// </summary>
    [Column("address_detail")]
    public string? AddressDetail { get; set; }

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }

    /// <summary>
    /// 건물 전경 사진 파일 그룹 식별자 (ID)
    /// </summary>
    [Column("building_photo_group_id")]
    public string? BuildingPhotoGroupId { get; set; }

    /// <summary>
    /// 주차장 안내 이미지 파일 그룹 식별자 (ID)
    /// </summary>
    [Column("parking_photo_group_id")]
    public string? ParkingPhotoGroupId { get; set; }
}
