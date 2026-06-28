using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인별 장례 시설(안치실, 염습실, 영결식장 등) 이용 내역 엔티티
/// </summary>
[Table("deceased_facilities")]
public class DeceasedFacility
{
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = null!;

    [Required]
    [Column("deceased_id")]
    [MaxLength(50)]
    public string DeceasedId { get; set; } = null!;

    [Required]
    [Column("facility_type")]
    [MaxLength(50)]
    public string FacilityType { get; set; } = null!; // MORGUE, WASH_ROOM, HALL, ETC

    [Column("start_time")]
    public DateTime? StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    [Column("use_hours")]
    public double UseHours { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("total_price")]
    public decimal TotalPrice { get; set; }

    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }
}
