using System;

namespace AuthServer.DTOs;

/// <summary>계정 정보 반환용 DTO</summary>
public class AccountDto
{
    public string Id { get; set; } = string.Empty;
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<string> Emails { get; set; } = new();
    public string? Phone { get; set; }
    public List<string> Phones { get; set; } = new();
    public string Status { get; set; } = "ACTIVE";
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? DeptId { get; set; }
    public string? DeptName { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>마지막으로 로그인한 시각(UTC). 없으면 null.</summary>
    /// 목록은 account_login_logs 를 집계하지 않는다 — 계정 하나에 수백 줄이라 목록이 무겁다.
    public DateTime? LastLoginAt { get; set; }

    /// <summary>마지막 로그인 때의 접속 IP. 목록의 설명 풍선에 쓴다.</summary>
    public string? LastLoginIp { get; set; }

    /// <summary>마지막으로 포털을 쓴 시각(UTC). 없으면 null.</summary>
    /// 로그인 시각만 보면 토큰으로 매일 쓰는 사람도 오래된 사람으로 보이므로 LastSeenAt 을 따로 둔다.
    public DateTime? LastSeenAt { get; set; }

    public List<string> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();

    /// <summary>연결된 소셜 공급자 목록 (google, naver, kakao 등)</summary>
    public List<string> SocialProviders { get; set; } = new();

    /// <summary>소셜 계정과 결합/연결되어 있는지 여부</summary>
    public bool IsSocialLinked { get; set; }

    /// <summary>가입 신청 등을 통해 결합(Merge)된 소셜 계정이 있는지 여부</summary>
    public bool IsSocialMerged { get; set; }

    /// <summary>연결된 소셜 공급자 표시 텍스트 (예: "카카오, 네이버")</summary>
    public string? SocialProviderText { get; set; }

    /// <summary>프로필 사진 주소. 목록에서는 썸네일 경로로 바꿔 쓴다.</summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// 프로필 사진 파일 그룹 식별자. <see cref="Avatar"/> 가 비어 있을 때
    /// 이 값으로 파일 서버에서 찾을 수 있다.
    /// </summary>
    public string? AvatarGroupId { get; set; }

    /// <summary>생년월일. <see cref="BirthDateIsLunar"/> 가 참이면 음력 월·일이다.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>생년월일이 음력인지</summary>
    public bool BirthDateIsLunar { get; set; }

    /// <summary>생일 축하(생일 화면 노출·메시지) 대상인지</summary>
    public bool BirthdayCelebrated { get; set; } = true;

    /// <summary>개발 업무용 확장 속성. account_profile_details 의 Dev.* 를 접두사 없이 담는다.</summary>
    public Dictionary<string, string?> DevAttributes { get; set; } = [];

    /// <summary>화면에 로그인 아이디 워터마크를 깔지. 관리자가 정하며, 값이 없으면 켜진 것이다.</summary>
    public bool Watermark { get; set; } = true;

    /// <summary>계정을 만들면서 발급한 첫 비밀번호. 등록 응답에만 담긴다. 저장은 해시라 이 순간이 지나면 알 수 없다.</summary>
    public string? InitialPassword { get; set; }
}

/// <summary>계정 생성을 위한 DTO</summary>
public class CreateAccountDto
{
    /// <summary>워터마크. null 은 건드리지 않음.</summary>
    public bool? Watermark { get; set; }

    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<string> Emails { get; set; } = new();
    public string? Phone { get; set; }
    public List<string> Phones { get; set; } = new();
    public string Status { get; set; } = "ACTIVE";
    public string? DeptId { get; set; }
    public List<string> RoleIds { get; set; } = new();

    /// <summary>생년월일. <see cref="BirthDateIsLunar"/> 가 참이면 음력 월·일이다.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>생년월일이 음력인지</summary>
    public bool BirthDateIsLunar { get; set; }

    /// <summary>생일 축하 대상인지</summary>
    public bool BirthdayCelebrated { get; set; } = true;

    /// <summary>
    /// 개발 업무용 확장 속성 — 사번 · 장비 · 계정 발급 현황 ….
    /// <see cref="UpdateAccountDto.DevAttributes"/> 와 같은 규칙이다.
    /// </summary>
    public Dictionary<string, string?>? DevAttributes { get; set; }

}

/// <summary>계정 수정을 위한 DTO</summary>
public class UpdateAccountDto
{
    /// <summary>
    /// 워터마크를 깔지. <b><c>null</c> 은 「건드리지 않음」</b>이다 —
    /// 사진·생년월일과 같은 규칙이다.
    /// </summary>
    public bool? Watermark { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<string> Emails { get; set; } = new();
    public string? Phone { get; set; }
    public List<string> Phones { get; set; } = new();
    public string Status { get; set; } = "ACTIVE";
    public string? CompanyId { get; set; }
    public string? DeptId { get; set; }
    public List<string> RoleIds { get; set; } = new();

    /// <summary>생년월일. <see cref="BirthDateIsLunar"/> 가 참이면 음력 월·일이다.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>생년월일이 음력인지</summary>
    public bool BirthDateIsLunar { get; set; }

    /// <summary>생일 축하 대상인지</summary>
    public bool BirthdayCelebrated { get; set; } = true;

    /// <summary>개발 업무용 확장 속성. null 은 건드리지 않음, 사전을 주면 그것이 계정의 전부다.</summary>
    public Dictionary<string, string?>? DevAttributes { get; set; }
}

/// <summary>관리자가 비밀번호를 초기화하고 돌려받는 값. 평문은 이 응답에만 담긴다.</summary>
public class IssuedPasswordDto
{
    /// <summary>누구의 것인지. 창에 함께 띄워 엉뚱한 사람에게 전하지 않게 한다.</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>그 사람의 이름. 아이디만으로는 누구인지 확신하기 어렵다.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>발급한 평문. <b>이 응답에만 담긴다.</b></summary>
    public string Password { get; set; } = string.Empty;
}
