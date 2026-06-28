using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인 장례 계약자 정보 관리 엔티티
/// </summary>
[Table("deceased_contractors")]
public class DeceasedContractor
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
    [Column("contact")]
    [MaxLength(50)]
    public string Contact { get; set; } = null!;

    [Column("relation")]
    [MaxLength(50)]
    public string? Relation { get; set; }

    [Column("address")]
    [MaxLength(200)]
    public string? Address { get; set; }

    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }

    [Column("signature_file_id")]
    [MaxLength(50)]
    public string? SignatureFileId { get; set; } // 전자 서명 파일 ID
}
