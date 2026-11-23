using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>회사 사용처의 처리 담당자 — 그 회사의 그 사용처(예: 헬프데스크) 일을 누가 맡는가.</summary>
/// 사용처 줄(company_usage_locations)의 id 가 아니라 (회사, 코드값)에 매단다 — 사용처는 회사 저장 때 지웠다 다시 넣어 id 가 바뀐다.
/// 사용처를 해제해도 담당자 줄은 남는다(다시 걸면 그대로 살아난다). 읽는 쪽이 지금 걸린 사용처와 맞춰 본다.
[Table("company_usage_managers", Schema = "scom")]
public class CompanyUsageManager : BaseEntity<int>
{
    /// <summary>회사 식별자 (scom.companies.id)</summary>
    [Required]
    [Column("company_id")]
    public string CompanyId { get; set; } = string.Empty;

    [ForeignKey(nameof(CompanyId))]
    public virtual Company? Company { get; set; }

    /// <summary>사용처 코드값 (<c>COMPANY_USAGE_LOCATION</c> 그룹의 <c>code_value</c>)</summary>
    [Required]
    [Column("code_value")]
    [MaxLength(50)]
    public string CodeValue { get; set; } = string.Empty;

    /// <summary>담당자 계정 식별자 (scom.accounts.id)</summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }
}
