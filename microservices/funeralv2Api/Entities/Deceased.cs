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

    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "IN_HOSPITAL"; // IN_HOSPITAL, DISCHARGED, COMPLETED

    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }

    [Column("ssn")]
    [MaxLength(50)]
    public string? Ssn { get; set; } // 주민등록번호

    [Column("cause_of_death")]
    [MaxLength(200)]
    public string? CauseOfDeath { get; set; } // 사망원인

    [Column("burial_plot")]
    [MaxLength(200)]
    public string? BurialPlot { get; set; } // 장지 위치

    [Column("memorial_photo_url")]
    [MaxLength(500)]
    public string? MemorialPhotoUrl { get; set; } // 영정사진 주소

    [Column("memorial_photo_file_id")]
    [MaxLength(50)]
    public string? MemorialPhotoFileId { get; set; } // 영정사진 파일 ID

    [Column("memorial_edited_photo_url")]
    [MaxLength(500)]
    public string? MemorialEditedPhotoUrl { get; set; } // 편집된 영정사진 주소

    [Column("memorial_edited_photo_file_id")]
    [MaxLength(50)]
    public string? MemorialEditedPhotoFileId { get; set; } // 편집된 영정사진 파일 ID

    [Column("family_photo_group_id")]
    [MaxLength(50)]
    public string? FamilyPhotoGroupId { get; set; } // 유족 추모용 사진 그룹 ID

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
