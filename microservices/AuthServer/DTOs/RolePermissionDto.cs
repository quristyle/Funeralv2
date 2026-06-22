using System.Collections.Generic;

namespace AuthServer.DTOs;

/// <summary>
/// 역할별 지정 사용자 정보를 담는 DTO
/// </summary>
public class RoleUserDto
{
    public string Id { get; set; } = string.Empty;
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? DeptName { get; set; }
    public string? CompanyName { get; set; }
}

/// <summary>
/// 역할별 사용자 할당 요청 DTO
/// </summary>
public class AssignRoleAccountsDto
{
    public List<string> AccountIds { get; set; } = new();
}

/// <summary>
/// 역할 메뉴 및 세부 권한 정보를 담는 DTO
/// </summary>
public class RoleMenuDto
{
    public string MenuId { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public bool CanView { get; set; }
    public bool CanSearch { get; set; }
    public bool CanCreate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanPrint { get; set; }
    public bool CanExcel { get; set; }
    public bool CanCust1 { get; set; }
    public bool CanCust2 { get; set; }
    public bool CanCust3 { get; set; }
    public bool CanCust4 { get; set; }
    public bool CanCust5 { get; set; }
    public bool CanCust6 { get; set; }
    public bool CanCust7 { get; set; }
    public bool CanCust8 { get; set; }
}

/// <summary>
/// 역할 메뉴 세부 권한 저장 요청 DTO
/// </summary>
public class SaveRoleMenuDto
{
    public string MenuId { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanSearch { get; set; }
    public bool CanCreate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanPrint { get; set; }
    public bool CanExcel { get; set; }
    public bool CanCust1 { get; set; }
    public bool CanCust2 { get; set; }
    public bool CanCust3 { get; set; }
    public bool CanCust4 { get; set; }
    public bool CanCust5 { get; set; }
    public bool CanCust6 { get; set; }
    public bool CanCust7 { get; set; }
    public bool CanCust8 { get; set; }
}
