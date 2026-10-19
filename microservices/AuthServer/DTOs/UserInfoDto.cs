namespace AuthServer.DTOs;

/// <summary>프론트엔드 UserInfo 타입에 대응하는 사용자 정보 DTO</summary>
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

    /// <summary>소속 회사 ID</summary>
    public string? CompanyId { get; set; }

    /// <summary>소속 회사명</summary>
    public string? CompanyName { get; set; }

    /// <summary>부서명</summary>
    public string? DeptName { get; set; }

    /// <summary>아바타 이미지 URL</summary>
    public string Avatar { get; set; } = "https://gw.alipayobjects.com/zos/antfincdn/XAosXuNZyF/BiazfanxmamNRoxxVxka.png";

    /// <summary>아바타 이미지 파일 그룹 ID</summary>
    public string? AvatarGroupId { get; set; }

    /// <summary>화면에 로그인 아이디 워터마크를 깔지. 관리자가 정하며 셸이 이 값을 보고 깔거나 걷는다. 워터마크만 따로 묻는 통로를 두면 화면이 뜰 때 왕복이 하나 늘어 워터마크 없는 화면이 잠깐 보이므로 여기 담는다.</summary>
    public bool Watermark { get; set; } = true;

    /// <summary>사용자 설명 또는 이메일</summary>
    public string? Desc { get; set; }

    /// <summary>로그인 후 리다이렉트될 기본 홈 경로</summary>
    public string HomePath { get; set; } = "/workspace";

    /// <summary>사용자가 보유한 권한(Role) 식별자 목록 (ADMINISTRATOR 등)</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>사용자가 보유한 권한(Role) 표시 이름 목록 (관리자, 시스템관리자 …). 화면에 역할을 보여 줄 때 쓴다 — 식별자는 사람이 읽기 어렵다.</summary>
    public List<string> RoleNames { get; set; } = new();

    /// <summary>이 계정이 어느 MSA 레코드에서 만들어졌는지. 형식은 <c>&lt;서비스&gt;:&lt;테이블&gt;:&lt;원본키&gt;</c> (<c>helpdesk:admin:4</c> · <c>projmng:dev_user:jskim</c>).</summary>
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
    public List<string> Phones { get; set; } = new();
    public string? Email { get; set; }
    public List<string> Emails { get; set; } = new();

    /// <summary>연결/결합된 소셜 공급자 목록 (google, naver, kakao 등)</summary>
    public List<string> SocialProviders { get; set; } = new();

    /// <summary>소셜 계정과 연결되어 있는지 여부</summary>
    public bool IsSocialLinked { get; set; }

    /// <summary>가입 신청 등을 통해 결합(Merge)된 소셜 계정이 있는지 여부</summary>
    public bool IsSocialMerged { get; set; }

    /// <summary>연결된 소셜 공급자 표시 텍스트 (예: "카카오, 네이버")</summary>
    public string? SocialProviderText { get; set; }

    /// <summary>결합된 소셜 계정들의 상세 정보 목록</summary>
    public List<SocialMergedAccountDto> MergedSocialAccounts { get; set; } = new();
    
    public bool SecurityPhone { get; set; }
    public bool SecurityQuestion { get; set; }
    public bool SecurityEmail { get; set; }
    public bool SecurityMfa { get; set; }
    
    public bool SystemMessage { get; set; }
    public bool TodoTask { get; set; }
    public bool AccountPasswordNotify { get; set; }

    // 계정 이력(/profile 의 계정 정보 탭) — 읽기 전용이고 시스템이 남기는 기록이다.

    /// <summary>가입일. 계정을 만든 시각이다.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>최근 로그인 성공 시각. 지금 이 화면을 보는 로그인이 곧 최근 로그인이다.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>최근 로그인 시 접속 IP. 클라이언트가 보낸 헤더에서 온 값이라 위조할 수 있으므로 참고용이다.</summary>
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

/// <summary>사용자 계정에 연결 또는 결합된 소셜 계정 상세 정보 DTO</summary>
public class SocialMergedAccountDto
{
    /// <summary>공급자 키 (google, naver, kakao 등)</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>공급자 표시명 (구글, 네이버, 카카오 등)</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>공급자 사용자 고유 식별자</summary>
    public string? ProviderUserId { get; set; }

    /// <summary>소셜 계정 이메일</summary>
    public string? Email { get; set; }

    /// <summary>소셜 계정 표시 이름 또는 닉네임</summary>
    public string? DisplayName { get; set; }

    /// <summary>연동 또는 결합 일시</summary>
    public DateTime? LinkedAt { get; set; }

    /// <summary>마지막 로그인 일시</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>소셜 가입 신청 결합(Merge) 여부</summary>
    public bool IsMerged { get; set; }

    /// <summary>결합된 신청 계정의 아이디 (예: kakao_12345)</summary>
    public string? MergedLoginId { get; set; }

    /// <summary>결합 비고 또는 승인자 정보</summary>
    public string? Remark { get; set; }
}
