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
