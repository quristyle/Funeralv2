using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인 정보 관리 엔티티
/// </summary>
[Table("deceaseds")]
public class Deceased
{
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = null!;

    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [Required]
    [Column("gender")]
    [MaxLength(10)]
    public string Gender { get; set; } = null!; // MALE, FEMALE

    [Column("age")]
    public int Age { get; set; }

    [Column("religion")]
    [MaxLength(50)]
    public string? Religion { get; set; }

    [Required]
    [Column("death_date")]
    public DateTime DeathDate { get; set; }

    [Column("funeral_date")]
    public DateTime? FuneralDate { get; set; }

    [Column("burial_date")]
    public DateTime? BurialDate { get; set; }

    [Column("room_id")]
    [MaxLength(50)]
    public string? RoomId { get; set; }

    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "IN_HOSPITAL"; // IN_HOSPITAL, DISCHARGED, COMPLETED

    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }

    [Column("created_by")]
    [MaxLength(50)]
    public string CreatedBy { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_by")]
    [MaxLength(50)]
    public string? UpdatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
