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
}
