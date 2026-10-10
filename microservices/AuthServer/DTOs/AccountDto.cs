using System;

namespace AuthServer.DTOs;

/// <summary>
/// 계정 정보 반환용 DTO
/// </summary>
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
    public List<string> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();

    /// <summary>
    /// 프로필 사진 주소. 프로필의 <c>Avatar</c> 값이다.
    ///
    /// <para>
    /// 값이 <c>/api/file/download/...</c> 형태이면 화면에서 <c>/api/file/thumbnail/...</c> 로
    /// 바꿔 쓴다(목록에 원본을 그대로 받으면 무겁다). 이 변환은 포털이 이미 쓰는 규칙이다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 개발 업무용 확장 속성 — 사번 · 장비 · 계정 발급 현황 ….
    /// </summary>
    /// <remarks>
    /// <c>account_profile_details</c> 의 <c>Dev.*</c> 를 접두사 없이 담는다.
    /// 자세한 것은 <c>docs/projmng-account-merge.md</c>.
    /// </remarks>
    public Dictionary<string, string?> DevAttributes { get; set; } = [];

    /// <summary>
    /// 화면에 로그인 아이디 워터마크를 깔지.
    ///
    /// <para>
    /// <b>관리자가 정한다.</b> 사용자 환경설정에 두지 않는 이유는 이 표시가
    /// 「찍힌 사진에서 누구 화면인지 드러나게」 하려고 있는 것이라, 당사자가
    /// 스스로 끌 수 있으면 목적이 사라지기 때문이다.
    /// </para>
    ///
    /// <para>
    /// <c>account_profile_details</c> 의 <c>Watermark</c> 에 담는다. <b>값이
    /// 없으면 켜진 것</b>이다 — 설정을 안 건드린 계정이 조용히 꺼지면 안 된다.
    /// </para>
    /// </summary>
    public bool Watermark { get; set; } = true;

    /// <summary>
    /// 계정을 만들면서 발급한 첫 비밀번호. <b>등록 응답에만 담긴다</b> —
    /// 목록·수정 응답에서는 언제나 <c>null</c> 이다.
    ///
    /// <para>
    /// 저장은 해시로 하므로 <b>이 순간이 지나면 아무도 값을 알 수 없다.</b>
    /// 관리자 화면이 이 값을 사람에게 한 번 보여 주고, 그다음은 본인이
    /// 첫 로그인에서 바꾼다(<see cref="Services.PasswordPolicy.AlreadyExpiredAt"/>).
    /// </para>
    /// </summary>
    public string? InitialPassword { get; set; }
}

/// <summary>
/// 계정 생성을 위한 DTO
/// </summary>
public class CreateAccountDto
{
    /// <summary>
    /// 워터마크를 깔지. <b><c>null</c> 은 「건드리지 않음」</b>이다 —
    /// 사진·생년월일과 같은 규칙이다.
    /// </summary>
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

/// <summary>
/// 계정 수정을 위한 DTO
/// </summary>
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

    /// <summary>
    /// 개발 업무용 확장 속성.
    /// </summary>
    /// <remarks>
    /// <b><c>null</c> 은 「건드리지 않음」</b>이고, 사전을 주면 그것이 그
    /// 계정의 전부다(빠진 열쇠는 지운다). 이 속성을 모르는 화면이 저장해도
    /// 값이 사라지지 않게 하려는 것이다.
    /// </remarks>
    public Dictionary<string, string?>? DevAttributes { get; set; }
}

/// <summary>
/// 관리자가 비밀번호를 초기화하고 돌려받는 값.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AccountDto.InitialPassword"/> 와 같은 성격이다 — <b>발급한 평문이
/// 손에 있는 유일한 순간</b>이고, 저장은 해시로만 하므로 이 응답을 놓치면
/// 아무도 그 값을 알 수 없다.
/// </para>
///
/// <para>
/// 계정 한 벌을 통째로 돌려주지 않는 이유는, 이 길로 바뀌는 것이 비밀번호
/// 하나뿐이기 때문이다. 계정을 돌려주면 받는 쪽이 그것으로 목록을 갈아 끼우고
/// 싶어지는데, 그 모양은 조회가 만드는 것(회사명·역할명·알림 상태)과 달라
/// 조용히 빈 칸이 생긴다.
/// </para>
/// </remarks>
public class IssuedPasswordDto
{
    /// <summary>누구의 것인지. 창에 함께 띄워 엉뚱한 사람에게 전하지 않게 한다.</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>그 사람의 이름. 아이디만으로는 누구인지 확신하기 어렵다.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>발급한 평문. <b>이 응답에만 담긴다.</b></summary>
    public string Password { get; set; } = string.Empty;
}
