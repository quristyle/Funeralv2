using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 계정 정보 엔티티 클래스
/// </summary>
[Table("accounts", Schema = "scom")]
public class Account : BaseEntity<string>
{
    public Account()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>사용자 아이디 (로그인 아이디)</summary>
    [Required]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>사용자 실명 또는 닉네임</summary>
    [Column("user_name")]
    public string? UserName { get; set; }



    /// <summary>사용자 실명 : 반드시 2글자 이상 입력되어야 함.</summary>
    [Column("real_name")]
    public string? RealName { get; set; }
    



    /// <summary>암호화된 비밀번호</summary>
    [Required]
    [Column("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>소속 회사 ID</summary>
    [Column("company_id")]
    public string? CompanyId { get; set; }

    [ForeignKey("CompanyId")]
    public Company? Company { get; set; }

    /// <summary>소속 부서 ID</summary>
    [Column("department_id")]
    public string? DepartmentId { get; set; }

    public Department? Department { get; set; }

    // 관계 설정: 1(Account) : N(AccountProfileDetail)
    public ICollection<AccountProfileDetail>? ProfileDetails { get; set; }
}
