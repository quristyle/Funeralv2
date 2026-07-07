using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인별 호실(빈소) 배정 및 사용 기간 이력 관리 엔티티 클래스
/// </summary>
[Table("deceased_rooms")]
public class DeceasedRoom
{
    /// <summary>
    /// 배정 내역 식별자 (ID)
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
    /// 배정된 호실(빈소) 식별자 (ID)
    /// </summary>
    [Required]
    [Column("room_id")]
    [MaxLength(50)]
    public string RoomId { get; set; } = null!;

    /// <summary>
    /// 호실 사용 시작 일시
    /// </summary>
    [Required]
    [Column("start_time")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 호실 사용 종료 일시
    /// </summary>
    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 삭제 여부 플래그
    /// </summary>
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
