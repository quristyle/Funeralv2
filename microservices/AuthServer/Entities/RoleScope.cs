using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>역할 - 회사 매핑. 그 회사에 속한 사람 전부에게 적용되는 기본 역할이다. 역할은 회사·부서·사람 세 단계에 줄 수 있고 셋을 모두 합쳐 적용된다 — 어느 한 단계가 다른 단계를 덮어쓰지 않는다.</summary>
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

/// <summary>역할 - 부서 매핑. 그 부서에 속한 사람에게 적용되며, 상위 부서에 걸린 역할도 물려받는다.</summary>
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
