using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인별 장례 시설(안치실, 염습실, 영결식장 등) 이용 내역 엔티티 클래스
/// </summary>
[Table("deceased_facilities")]
public class DeceasedFacility
{
    /// <summary>
    /// 이용 내역 식별자 (ID)
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
    /// 시설 유형 (예: MORGUE, WASH_ROOM, HALL, ETC 등)
    /// </summary>
    [Required]
    [Column("facility_type")]
    [MaxLength(50)]
    public string FacilityType { get; set; } = null!;

    /// <summary>
    /// 시설 이용 시작 일시
    /// </summary>
    [Column("start_time")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 시설 이용 종료 일시
    /// </summary>
    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 시설 총 이용 시간
    /// </summary>
    [Column("use_hours")]
    public double UseHours { get; set; }

    /// <summary>
    /// 시설 시간당 이용 단가
    /// </summary>
    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 시설 이용 총 금액
    /// </summary>
    [Column("total_price")]
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }
}
