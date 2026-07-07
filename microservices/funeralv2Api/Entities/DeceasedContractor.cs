using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인 장례 계약자 정보 관리 엔티티 클래스
/// </summary>
[Table("deceased_contractors")]
public class DeceasedContractor
{
    /// <summary>
    /// 계약자 식별자 (ID)
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
    /// 계약자 성명
    /// </summary>
    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// 계약자 연락처 (전화번호)
    /// </summary>
    [Required]
    [Column("contact")]
    [MaxLength(50)]
    public string Contact { get; set; } = null!;

    /// <summary>
    /// 고인과의 관계
    /// </summary>
    [Column("relation")]
    [MaxLength(50)]
    public string? Relation { get; set; }

    /// <summary>
    /// 계약자 주소
    /// </summary>
    [Column("address")]
    [MaxLength(200)]
    public string? Address { get; set; }

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 계약 서명 이미지 파일 식별자 (ID)
    /// </summary>
    [Column("signature_file_id")]
    [MaxLength(50)]
    public string? SignatureFileId { get; set; }
}
