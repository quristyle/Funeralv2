using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인별 장례 담당 임직원 및 지도사 매핑 엔티티 클래스
/// </summary>
[Table("deceased_managers")]
public class DeceasedManager
{
    /// <summary>
    /// 매핑 정보 식별자 (ID)
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// 연관된 고인 식별자 (ID)
    /// </summary>
    [Required]
    [Column("deceased_id")]
    [MaxLength(50)]
    public string DeceasedId { get; set; } = null!;

    /// <summary>
    /// 장례지도사 성명
    /// </summary>
    [Column("director_name")]
    [MaxLength(100)]
    public string? DirectorName { get; set; }

    /// <summary>
    /// 장례지도사 연락처 (전화번호)
    /// </summary>
    [Column("director_contact")]
    [MaxLength(50)]
    public string? DirectorContact { get; set; }

    /// <summary>
    /// 상조회사 명칭
    /// </summary>
    [Column("mutual_aid_company")]
    [MaxLength(100)]
    public string? MutualAidCompany { get; set; }

    /// <summary>
    /// 당사 담당 직원 성명
    /// </summary>
    [Column("staff_name")]
    [MaxLength(100)]
    public string? StaffName { get; set; }

    /// <summary>
    /// 당사 담당 직원 연락처 (전화번호)
    /// </summary>
    [Column("staff_contact")]
    [MaxLength(50)]
    public string? StaffContact { get; set; }
}
