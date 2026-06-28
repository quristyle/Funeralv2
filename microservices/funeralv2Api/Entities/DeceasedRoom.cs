using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인별 호실(빈소) 배정 및 사용 기간 이력 관리 엔티티
/// </summary>
[Table("deceased_rooms")]
public class DeceasedRoom
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
    [Column("room_id")]
    [MaxLength(50)]
    public string RoomId { get; set; } = null!;

    [Required]
    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
