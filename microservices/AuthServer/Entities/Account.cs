using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

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
    /// 최근 로그인 성공 시각 (UTC). 아직 한 번도 로그인하지 않았으면 null 이다.
    /// </summary>
    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 최근 로그인 시 접속 IP.
    /// 게이트웨이 뒤에 있으므로 <c>X-Forwarded-For</c> 의 첫 값을 쓴다.
    /// </summary>
    [Column("last_login_ip")]
    public string? LastLoginIp { get; set; }

    /// <summary>
    /// 마지막으로 포털을 <b>쓴</b> 시각 (UTC). 아직 한 번도 안 썼으면 null 이다.
    /// </summary>
    /// 로그인 시각과 다른 값이다 — 로그인은 들어온 때, 이것은 마지막으로 무언가를 한 때다. 로그인만 보면 토큰을 들고 로그인 화면을 안 거치는 사람이 전부 오래 전 사람으로 보인다.
    /// 포털은 메뉴 전환에 서버 요청이 나가지 않으므로 화면을 옮길 때 찍는다(docs/account-activity.md). 표를 세지 않고 칸으로 두는 까닭은 기록 표가 한 해 뒤 걷히기 때문이다.
    [Column("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// 비밀번호를 마지막으로 바꾼 시각 (UTC). 90일 만료 계산의 기준이다.
    /// null 이면 기준을 알 수 없어 만료로 보지 않는다. 기존 계정은 실행 시각으로 채워 두었다(docs/sql/account_login_audit.sql).
    /// </summary>
    [Column("password_changed_at")]
    public DateTime? PasswordChangedAt { get; set; }

    /// <summary>
    /// 생년월일. <see cref="BirthDateIsLunar"/> 가 참이면 음력 월·일로 해석한다.
    /// 생일의 정본은 여기(포털 계정)다. 입력·수정은 계정 관리 화면이, 생일 화면(/birthday/*)은 읽기만 한다.
    /// </summary>
    [Column("birth_date")]
    public DateOnly? BirthDate { get; set; }

    /// <summary>
    /// 생년월일이 음력인지
    /// </summary>
    [Column("birth_date_is_lunar")]
    public bool BirthDateIsLunar { get; set; }

    /// <summary>
    /// 생일 축하(생일 화면 노출·메시지) 대상인지. 본인이 원치 않으면 끈다.
    /// </summary>
    [Column("birthday_celebrated")]
    public bool BirthdayCelebrated { get; set; } = true;

    /// <summary>
    /// 사용자 프로필 상세 정보 목록 탐색 속성 (1:N 관계)
    /// </summary>
    public ICollection<AccountProfileDetail>? ProfileDetails { get; set; }
}
