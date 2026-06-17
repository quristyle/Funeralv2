using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>
/// 조직/부서 정보 엔티티 클래스
/// </summary>
[Table("departments", Schema = "scom")]
public class Department : BaseEntity
{
    /// <summary>부서 고유 ID (Primary Key)</summary>
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>부서 명칭</summary>
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>상위 부서 ID (트리 구조 지원)</summary>
    [Column("parent_id")]
    public string? ParentId { get; set; }

    /// <summary>소속 회사 ID</summary>
    [Column("company_id")]
    public string? CompanyId { get; set; }

    [ForeignKey("CompanyId")]
    public Company? Company { get; set; }

    /// <summary>부서 상태 (0: 비활성, 1: 활성)</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>부서 설명 및 비고</summary>
    [Column("remark")]
    public string? Remark { get; set; }

    // 관계 설정: 1(Department) : N(Account)
    public ICollection<Account>? Accounts { get; set; }
}
