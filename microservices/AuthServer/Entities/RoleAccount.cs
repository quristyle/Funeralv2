using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 역할 - 사용자 계정 매핑 엔티티 클래스 (N:M 관계 해소용 매핑 테이블)
/// </summary>
[Table("role_accounts", Schema = "scom")]
public class RoleAccount : BaseEntity<int>
{
    /// <summary>
    /// 연관된 역할 식별자 (ID)
    /// </summary>
    [Required]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 역할 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(RoleId))]
    public virtual Role? Role { get; set; }

    /// <summary>
    /// 연관된 사용자 계정 식별자 (ID)
    /// </summary>
    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// 연관된 사용자 계정 엔티티 탐색 속성
    /// </summary>
    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }
}
