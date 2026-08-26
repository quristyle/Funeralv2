namespace AuthServer.DTOs;

/// <summary>
/// 메뉴 하나를 기준으로 본 권한 현황.
/// </summary>
/// <remarks>
/// <c>/system/role-map</c> 은 <b>역할</b>에서 출발한다 — "이 역할은 어떤 메뉴를 쓰나".
/// 이 DTO 는 그 반대다 — <b>"이 메뉴는 누가 쓸 수 있나"</b>.
///
/// <para>
/// 같은 데이터(<c>role_menus</c> · <c>role_companies</c> · <c>role_departments</c> ·
/// <c>role_accounts</c>)를 거꾸로 훑는다. 지금까지는 그 방향으로 볼 방법이 없어서
/// "이 메뉴에 파트너도 들어오나?" 를 알려면 역할을 하나씩 열어 봐야 했다.
/// </para>
/// </remarks>
public class MenuRoleDto
{
    /// <summary>메뉴 식별자</summary>
    public string MenuId { get; set; } = string.Empty;

    /// <summary>메뉴 이름</summary>
    public string MenuName { get; set; } = string.Empty;

    /// <summary>메뉴 경로</summary>
    public string? MenuPath { get; set; }

    /// <summary>
    /// 이 메뉴가 실제로 쓰는 권한 항목.
    /// </summary>
    /// <remarks>
    /// 메뉴가 쓰지 않는 항목은 역할에 켜 두어도 효과가 없다
    /// (<c>MenuService</c> 가 메뉴의 <c>use_*</c> 와 AND 로 묶는다).
    /// 화면이 쓸모없는 체크박스를 보여 주지 않도록 함께 내려준다.
    /// </remarks>
    public MenuUsedPermissionDto Used { get; set; } = new();

    /// <summary>
    /// 모든 역할과 이 메뉴에 대한 권한.
    /// </summary>
    /// <remarks>
    /// 권한이 걸려 있지 않은 역할도 <b>모두 담는다.</b> 화면에서 새로 켜 줄 수 있어야 하는데
    /// 걸린 것만 주면 "없는 역할" 을 고를 방법이 없다. 걸린 적 없는 역할은 전부 false 다.
    /// </remarks>
    public List<MenuRoleGrantDto> Roles { get; set; } = new();

    /// <summary>이 메뉴에 닿는 회사</summary>
    public List<MenuRoleTargetDto> Companies { get; set; } = new();

    /// <summary>이 메뉴에 닿는 부서</summary>
    public List<MenuRoleTargetDto> Departments { get; set; } = new();

    /// <summary>이 메뉴에 닿는 사용자 (사람에게 직접 걸린 역할)</summary>
    public List<MenuRoleTargetDto> Accounts { get; set; } = new();

    /// <summary>
    /// 이 메뉴를 실제로 열람할 수 있는 사용자 수.
    /// </summary>
    /// <remarks>
    /// 회사·부서·사람 세 단계에 걸린 역할을 모두 합쳐 계산한 뒤 사람 단위로 중복을 없앤다.
    /// 목록의 건수를 더한 값과 다를 수 있다 — 한 사람이 회사와 부서 양쪽에서 걸릴 수 있다.
    /// </remarks>
    public int EffectiveUserCount { get; set; }
}

/// <summary>메뉴가 실제로 쓰는 권한 항목 (<c>system_menus.use_*</c>)</summary>
public class MenuUsedPermissionDto
{
    public bool View { get; set; }
    public bool Search { get; set; }
    public bool Create { get; set; }
    public bool Update { get; set; }
    public bool Delete { get; set; }
    public bool Print { get; set; }
    public bool Excel { get; set; }
    public bool Cust1 { get; set; }
    public bool Cust2 { get; set; }
    public bool Cust3 { get; set; }
    public bool Cust4 { get; set; }
    public bool Cust5 { get; set; }
    public bool Cust6 { get; set; }
    public bool Cust7 { get; set; }
    public bool Cust8 { get; set; }

    /// <summary>사용자 정의 항목의 이름. 메뉴 관리 화면에서 붙인다.</summary>
    public string? Cust1Name { get; set; }
    public string? Cust2Name { get; set; }
    public string? Cust3Name { get; set; }
    public string? Cust4Name { get; set; }
    public string? Cust5Name { get; set; }
    public string? Cust6Name { get; set; }
    public string? Cust7Name { get; set; }
    public string? Cust8Name { get; set; }
}

/// <summary>역할 하나가 이 메뉴에 대해 가진 권한</summary>
public class MenuRoleGrantDto
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>이 역할에 이 메뉴 권한이 한 줄이라도 걸려 있는지</summary>
    public bool Granted { get; set; }

    public bool CanView { get; set; }
    public bool CanSearch { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
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

    /// <summary>이 역할이 걸린 회사·부서·사람 수. "이 역할을 끄면 몇 명이 영향받나" 를 알려 준다.</summary>
    public int CompanyCount { get; set; }
    public int DepartmentCount { get; set; }
    public int AccountCount { get; set; }
}

/// <summary>
/// 이 메뉴에 닿는 대상 하나 (회사 · 부서 · 사람).
/// </summary>
public class MenuRoleTargetDto
{
    /// <summary>대상 식별자</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>대상 이름</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>부서·사람이면 소속 회사명. 같은 이름이 여러 회사에 있어 구분이 필요하다.</summary>
    public string? CompanyName { get; set; }

    /// <summary>사람이면 로그인 아이디</summary>
    public string? LoginId { get; set; }

    /// <summary>
    /// 어느 역할 때문에 닿는지.
    /// </summary>
    /// <remarks>
    /// 여러 역할이 동시에 걸릴 수 있다. 어느 역할을 풀어야 하는지 알려면 이름이 필요하다.
    /// </remarks>
    public List<string> ViaRoleNames { get; set; } = new();

    /// <summary>어느 역할 때문에 닿는지 (식별자). 화면이 해제 요청을 보낼 때 쓴다.</summary>
    public List<string> ViaRoleIds { get; set; } = new();

    /// <summary>
    /// 이 대상에 딸린 사람 수.
    /// 회사면 그 회사 인원, 부서면 그 부서 인원, 사람이면 1 이다.
    /// </summary>
    public int UserCount { get; set; }
}
