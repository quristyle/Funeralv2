namespace AuthServer.DTOs;

/// <summary>
/// 프론트엔드 UserInfo 타입에 대응하는 사용자 정보 DTO
/// </summary>
public class UserInfoDto
{
    /// <summary>사용자 고유 식별자</summary>
    public string? Id { get; set; }
    /// <summary>사용자 ID</summary>
    public string? UserId { get; set; }

    /// <summary>로그인 아이디</summary>
    public string? Username { get; set; }

    /// <summary>사용자 실명</summary>
    public string? RealName { get; set; }

    /// <summary>소속 회사명</summary>
    public string? CompanyName { get; set; }

    /// <summary>부서명</summary>
    public string? DeptName { get; set; }

    /// <summary>아바타 이미지 URL</summary>
    public string Avatar { get; set; } = "https://gw.alipayobjects.com/zos/antfincdn/XAosXuNZyF/BiazfanxmamNRoxxVxka.png";

    /// <summary>아바타 이미지 파일 그룹 ID</summary>
    public string? AvatarGroupId { get; set; }

    /// <summary>사용자 설명 또는 이메일</summary>
    public string? Desc { get; set; }

    /// <summary>로그인 후 리다이렉트될 기본 홈 경로</summary>
    public string HomePath { get; set; } = "/workspace";

    /// <summary>사용자가 보유한 권한(Role) 식별자 목록 (ADMINISTRATOR 등)</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// 사용자가 보유한 권한(Role) 표시 이름 목록 (관리자, 시스템관리자 …).
    /// 화면에 역할을 보여 줄 때 쓴다 — 식별자는 사람이 읽기 어렵다.
    /// </summary>
    public List<string> RoleNames { get; set; } = new();

    /// <summary>
    /// 이 계정이 어느 MSA 레코드에서 만들어졌는지. 형식은 <c>&lt;서비스&gt;:&lt;테이블&gt;:&lt;원본키&gt;</c> 다.
    /// (<c>helpdesk:admin:4</c> · <c>projmng:dev_user:jskim</c>)
    ///
    /// <para>
    /// 이관 계정은 아이디 충돌을 피하려고 접두어가 붙어 있다(<c>jskim</c> → <c>pm_jskim</c>).
    /// 그래서 화면이 포털 로그인 아이디로 저쪽 레코드를 찾으면 아무것도 못 찾는다.
    /// 어느 레코드에서 왔는지는 포털만 알고 있으므로 여기서 함께 내려준다.
    /// </para>
    ///
    /// <para>이관으로 만들어진 계정만 값이 있다. 원래부터 있던 계정은 null 이다.</para>
    /// </summary>
    public string? MsaSource { get; set; }

    /// <summary>추가 토큰 정보 (필요 시)</summary>
    public string Token { get; set; } = string.Empty;

    // 프로필 확장 필드 (기본 설정, 보안 설정, 알림 설정 값 바인딩용)
    /// <summary>생년월일. <see cref="BirthDateIsLunar"/> 가 참이면 음력 월·일이다.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>생년월일이 음력인지</summary>
    public bool BirthDateIsLunar { get; set; }

    public string? Introduction { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    
    public bool SecurityPhone { get; set; }
    public bool SecurityQuestion { get; set; }
    public bool SecurityEmail { get; set; }
    public bool SecurityMfa { get; set; }
    
    public bool SystemMessage { get; set; }
    public bool TodoTask { get; set; }
    public bool AccountPasswordNotify { get; set; }

    // ── 계정 이력 (/profile 의 '계정 정보' 탭) ──────────────
    //
    // 읽기 전용이다. 사용자가 고칠 수 있는 값이 아니라 시스템이 남기는 기록이다.

    /// <summary>가입일. 계정을 만든 시각이다.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>최근 로그인 성공 시각. 지금 이 화면을 보는 로그인이 곧 최근 로그인이다.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 최근 로그인 시 접속 IP.
    /// 클라이언트가 보낸 헤더에서 온 값이라 위조할 수 있으므로 참고용이다.
    /// </summary>
    public string? LastLoginIp { get; set; }

    /// <summary>비밀번호를 마지막으로 바꾼 시각.</summary>
    public DateTime? PasswordChangedAt { get; set; }

    /// <summary>비밀번호가 만료되는 시각. 정책이 꺼져 있거나 기준 시각을 모르면 null 이다.</summary>
    public DateTime? PasswordExpiresAt { get; set; }

    /// <summary>만료 기준 일수(기본 90). 정책이 꺼져 있으면 null 이다.</summary>
    public int? PasswordExpiryDays { get; set; }

    /// <summary>만료까지 남은 일수. 이미 지났으면 0, 정책이 꺼져 있으면 null 이다.</summary>
    public int? PasswordDaysRemaining { get; set; }

    /// <summary>비밀번호 사용 기간이 지났는지.</summary>
    public bool PasswordExpired { get; set; }
}
