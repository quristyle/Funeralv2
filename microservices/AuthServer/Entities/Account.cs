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
    /// <summary>
    /// Account 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public Account()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 사용자 아이디 (로그인 아이디)
    /// </summary>
    [Required]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 사용자 실명 또는 닉네임
    /// </summary>
    [Column("user_name")]
    public string? UserName { get; set; }

    /// <summary>
    /// 사용자 실명 (반드시 2글자 이상 입력되어야 함)
    /// </summary>
    [Column("real_name")]
    public string? RealName { get; set; }

    /// <summary>
    /// 암호화된 비밀번호
    /// </summary>
    [Required]
    [Column("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 소속 회사 식별자 (ID)
    /// </summary>
    [Column("company_id")]
    public string? CompanyId { get; set; }

    /// <summary>
    /// 소속 회사 엔티티 탐색 속성
    /// </summary>
    [ForeignKey("CompanyId")]
    public Company? Company { get; set; }

    /// <summary>
    /// 소속 부서 식별자 (ID)
    /// </summary>
    [Column("department_id")]
    public string? DepartmentId { get; set; }

    /// <summary>
    /// 소속 부서 엔티티 탐색 속성
    /// </summary>
    public Department? Department { get; set; }

    /// <summary>
    /// 아바타 이미지 파일 그룹 식별자 (ID)
    /// </summary>
    [Column("avatar_group_id")]
    public string? AvatarGroupId { get; set; }

    /// <summary>
    /// 사용자 프로필 상세 정보 목록 탐색 속성 (1:N 관계)
    /// </summary>
    public ICollection<AccountProfileDetail>? ProfileDetails { get; set; }
}
