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
    public string? Phone { get; set; }
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
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? DeptId { get; set; }
    public List<string> RoleIds { get; set; } = new();

    /// <summary>생년월일. <see cref="BirthDateIsLunar"/> 가 참이면 음력 월·일이다.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>생년월일이 음력인지</summary>
    public bool BirthDateIsLunar { get; set; }

    /// <summary>생일 축하 대상인지</summary>
    public bool BirthdayCelebrated { get; set; } = true;
}

/// <summary>
/// 계정 수정을 위한 DTO
/// </summary>
public class UpdateAccountDto
{
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? DeptId { get; set; }
    public List<string> RoleIds { get; set; } = new();

    /// <summary>생년월일. <see cref="BirthDateIsLunar"/> 가 참이면 음력 월·일이다.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>생년월일이 음력인지</summary>
    public bool BirthDateIsLunar { get; set; }

    /// <summary>생일 축하 대상인지</summary>
    public bool BirthdayCelebrated { get; set; } = true;
}
