using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인 상주 정보 관리 엔티티
/// </summary>
[Table("deceased_mourners")]
public class DeceasedMourner
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
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [Required]
    [Column("relation")]
    [MaxLength(50)]
    public string Relation { get; set; } = null!; // 배우자, 자녀, 자부, 사위, 손자 등

    [Required]
    [Column("contact")]
    [MaxLength(50)]
    public string Contact { get; set; } = null!;

    [Column("email")]
    [MaxLength(100)]
    public string? Email { get; set; }

    [Column("address")]
    [MaxLength(200)]
    public string? Address { get; set; }

    [Column("is_chief")]
    public bool IsChief { get; set; } // 대표상주 여부

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
