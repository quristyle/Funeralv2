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
    public List<string> Roles { get; set; } = new();
    public string? RoleNames { get; set; }
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

    /// <summary>
    /// 화면에 찍을 메뉴 이름.
    ///
    /// <para>
    /// <b><c>system_menus.name</c> 이 아니다.</b> 그 칸에는 <c>PmAiDashboard</c> ·
    /// <c>HelpDeskPushDashboard</c> 같은 개발용 이름이 들어 있어서(215건 중 161건)
    /// 권한 화면이 사람 못 읽는 코드를 늘어놓고 있었다. 사이드바·메뉴롤·사람롤이
    /// 쓰는 것과 <b>같은 규칙</b>으로 고른다 —
    /// 옮긴 제목(<c>i18n_resources</c>) → 저장된 제목(<c>title</c>) → 이름(<c>name</c>).
    /// </para>
    /// </summary>
    public string MenuName { get; set; } = string.Empty;

    /// <summary>
    /// <c>system_menus.name</c> 그대로. 화면이 이름 아래 작게 적는다.
    ///
    /// <para>
    /// 이름만 보여 주면 「대시보드」가 업무마다 여럿이라 어느 것인지 갈리지 않는다.
    /// 코드를 버리지 않고 자리를 낮춘 이유가 그것이다.
    /// </para>
    /// </summary>
    public string MenuCode { get; set; } = string.Empty;

    /// <summary>메뉴가 가리키는 주소(<c>system_menus.path</c>). 코드 옆에 함께 적는다.</summary>
    public string? MenuPath { get; set; }

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

    // ── 이 메뉴가 쓰는 권한 항목 ────────────────────────────
    //
    // 메뉴마다 의미 있는 권한이 다르다. 권한 화면은 아래 값을 보고
    // 쓰지 않는 항목의 체크박스를 잠그고, 사용자 정의 1~8 은 지정된 이름으로 보여준다.
    // 값 자체는 system_menus 에 있고 여기서는 읽기용으로 함께 내려줄 뿐이다.

    public bool UseView { get; set; }
    public bool UseSearch { get; set; }
    public bool UseCreate { get; set; }
    public bool UseDelete { get; set; }
    public bool UseUpdate { get; set; }
    public bool UsePrint { get; set; }
    public bool UseExcel { get; set; }

    public bool UseCust1 { get; set; }
    public bool UseCust2 { get; set; }
    public bool UseCust3 { get; set; }
    public bool UseCust4 { get; set; }
    public bool UseCust5 { get; set; }
    public bool UseCust6 { get; set; }
    public bool UseCust7 { get; set; }
    public bool UseCust8 { get; set; }

    public string? Cust1Name { get; set; }
    public string? Cust2Name { get; set; }
    public string? Cust3Name { get; set; }
    public string? Cust4Name { get; set; }
    public string? Cust5Name { get; set; }
    public string? Cust6Name { get; set; }
    public string? Cust7Name { get; set; }
    public string? Cust8Name { get; set; }
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
