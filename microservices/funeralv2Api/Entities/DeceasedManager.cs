using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인별 장례 담당 임직원 및 지도사 매핑 엔티티
/// </summary>
[Table("deceased_managers")]
public class DeceasedManager
{
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = null!;

    [Required]
    [Column("deceased_id")]
    [MaxLength(50)]
    public string DeceasedId { get; set; } = null!;

    [Column("director_name")]
    [MaxLength(100)]
    public string? DirectorName { get; set; } // 장례지도사

    [Column("director_contact")]
    [MaxLength(50)]
    public string? DirectorContact { get; set; }

    [Column("mutual_aid_company")]
    [MaxLength(100)]
    public string? MutualAidCompany { get; set; } // 상조회사

    [Column("staff_name")]
    [MaxLength(100)]
    public string? StaffName { get; set; } // 당사 담당 직원

    [Column("staff_contact")]
    [MaxLength(50)]
    public string? StaffContact { get; set; }
}
