using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 역할 - 회사 매핑. 그 회사에 속한 사람 전부에게 적용되는 기본 역할이다.
///
/// <para>
/// 역할은 세 단계로 줄 수 있다 — <b>회사 · 부서 · 사람</b>. <b>셋을 모두 합쳐</b> 적용된다.
/// 어느 한 단계가 다른 단계를 덮어쓰지 않는다
/// (자세한 규칙은 <c>RoleAssignmentService.ResolveEffectiveRolesAsync</c> 주석 참고).
/// </para>
/// </summary>
[Table("role_companies", Schema = "scom")]
public class RoleCompany : BaseEntity<int>
{
    /// <summary>연관된 역할 식별자 (ID)</summary>
    [Required]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>연관된 역할 엔티티 탐색 속성</summary>
    [ForeignKey(nameof(RoleId))]
    public virtual Role? Role { get; set; }

    /// <summary>연관된 회사 식별자 (ID)</summary>
    [Required]
    [Column("company_id")]
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>연관된 회사 엔티티 탐색 속성</summary>
    [ForeignKey(nameof(CompanyId))]
    public virtual Company? Company { get; set; }
}

/// <summary>
/// 역할 - 부서 매핑. 그 부서에 속한 사람에게 적용되는 역할이다.
/// 상위 부서에 걸린 역할도 함께 물려받는다(<see cref="RoleCompany"/> 주석 참고).
/// </summary>
/// <remarks>
/// 부서는 <c>(company_id, id)</c> 복합 대체키를 갖지만, 여기서는 부서 식별자만 들고 있다.
/// 부서 식별자 자체가 유일하고(<c>scom.departments.id</c> 가 기본키), 회사는 부서를 통해 알 수 있다.
/// </remarks>
[Table("role_departments", Schema = "scom")]
public class RoleDepartment : BaseEntity<int>
{
    /// <summary>연관된 역할 식별자 (ID)</summary>
    [Required]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>연관된 역할 엔티티 탐색 속성</summary>
    [ForeignKey(nameof(RoleId))]
    public virtual Role? Role { get; set; }

    /// <summary>연관된 부서 식별자 (ID)</summary>
    [Required]
    [Column("department_id")]
    public string DepartmentId { get; set; } = string.Empty;
}
