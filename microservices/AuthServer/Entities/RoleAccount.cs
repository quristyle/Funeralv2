using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 역할 - 사용자 계정 매핑 엔티티
/// </summary>
[Table("role_accounts", Schema = "scom")]
public class RoleAccount : BaseEntity<int>
{
    [Required]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [ForeignKey(nameof(RoleId))]
    public virtual Role? Role { get; set; }

    [Required]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }
}
