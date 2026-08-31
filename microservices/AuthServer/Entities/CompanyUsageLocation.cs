using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 회사 사용처. 그 회사가 <b>어느 시스템에서 쓰이는지</b>를 공통코드로 담는다.
/// </summary>
/// <remarks>
/// 코드 그룹은 <c>COMPANY_USAGE_LOCATION</c>(회사 사용처)이다.
/// 회사 하나가 <b>여러 개</b>를 가질 수 있고 <b>하나도 없을 수도 있다</b> —
/// 그래서 회사 표에 칸을 더하지 않고 잇는 표를 뒀다
/// (<see cref="RoleCompany"/> 와 같은 모양).
///
/// <para>
/// 담는 값은 코드의 id 가 아니라 <b><c>code_value</c></b>(예: <c>HELP_DESK</c>)다.
/// 화면의 공통코드 셀렉트와 다른 시스템이 주고받는 값이 모두 그것이라,
/// id 를 담으면 쓸 때마다 코드 표를 한 번 더 들러야 한다.
/// 대신 공통코드로의 외래키는 없다 — 코드가 사라지면 값이 그대로 남는다.
/// </para>
/// </remarks>
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
