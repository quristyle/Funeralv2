using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 고인 상주 정보 관리 엔티티 클래스
/// </summary>
[Table("deceased_mourners")]
public class DeceasedMourner
{
    /// <summary>
    /// 상주 식별자 (ID)
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
    /// 상주 성명
    /// </summary>
    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// 고인과의 관계 (예: 배우자, 자녀, 자부, 사위, 손자 등)
    /// </summary>
    [Required]
    [Column("relation")]
    [MaxLength(50)]
    public string Relation { get; set; } = null!;

    /// <summary>
    /// 상주 연락처 (전화번호)
    /// </summary>
    [Required]
    [Column("contact")]
    [MaxLength(50)]
    public string Contact { get; set; } = null!;

    /// <summary>
    /// 이메일 주소
    /// </summary>
    [Column("email")]
    [MaxLength(100)]
    public string? Email { get; set; }

    /// <summary>
    /// 주소
    /// </summary>
    [Column("address")]
    [MaxLength(200)]
    public string? Address { get; set; }

    /// <summary>
    /// 대표 상주 여부
    /// </summary>
    [Column("is_chief")]
    public bool IsChief { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>
    /// 삭제 여부 플래그
    /// </summary>
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
