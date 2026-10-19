using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>회사 사용처. 그 회사가 어느 시스템에서 쓰이는지를 공통코드로 담는다. 한 회사가 여러 개를 가질 수 있고 하나도 없을 수도 있어 회사 표에 칸을 두지 않는다.</summary>
[Table("company_usage_locations", Schema = "scom")]
public class CompanyUsageLocation : BaseEntity<int>
{
    /// <summary>연관된 회사 식별자 (ID)</summary>
    [Required]
    [Column("company_id")]
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>연관된 회사 엔티티 탐색 속성</summary>
    [ForeignKey(nameof(CompanyId))]
    public virtual Company? Company { get; set; }

    /// <summary>공통코드 값 (<c>COMPANY_USAGE_LOCATION</c> 그룹의 <c>code_value</c>)</summary>
    [Required]
    [Column("code_value")]
    [MaxLength(50)]
    public string CodeValue { get; set; } = string.Empty;
}
