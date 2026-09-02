using AuthServer.Data;
using AuthServer.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 사이드바 메뉴 트리 조회 서비스 구현체
/// </summary>
public class MenuService : IMenuService
{
    private readonly AppDbContext _context;

    public MenuService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 사용자가 접근 가능한 모든 메뉴 목록을 트리 구조로 반환
    /// </summary>
    /// <param name="userId">사용자 식별자</param>
    public async Task<List<MenuDto>> GetAllMenusAsync(string userId)
    {
        // 1. 모든 활성 메뉴 조회
        var allMenus = await _context.SystemMenus
            .Where(m => m.Status == 1)
            .OrderBy(m => m.OrderNo)
            .ToListAsync();

        // 2. 트리 구조로 변환
        var menuTree = BuildMenuTree(allMenus, null);

        return menuTree;
    }

    /// <summary>
    /// 메뉴의 위치(부모)와 순서를 변경합니다.
    /// </summary>
    public async Task<bool> MoveMenuAsync(string menuId, string? newParentId, int newOrderNo)
    {
        var menu = await _context.SystemMenus.FindAsync(menuId);
        if (menu == null)
        {
            throw new KeyNotFoundException($"메뉴 ID '{menuId}'에 해당하는 메뉴를 찾을 수 없습니다.");
        }

        // 1. 부모 정보 변경
        menu.Pid = newParentId;
        // 임시 순서 설정
        menu.OrderNo = newOrderNo;

        // 2. 새 부모 아래의 모든 형제 노드 조회 (변경 대상 포함)
        var siblings = await _context.SystemMenus
            .Where(m => m.Pid == newParentId)
            .ToListAsync();

        // 3. 정렬 적용
        // targetNode는 새 newOrderNo를 기준으로 두고, 다른 형제들은 기존 OrderNo를 기준으로 정렬하되
        // newOrderNo보다 크거나 같은 노드들은 뒤로 밀리도록 값을 보정해 순차적인 순서를 만듭니다.
        var sortedSiblings = siblings
            .OrderBy(m => {
                if (m.Id == menuId)
                {
                    return newOrderNo;
                }
                return m.OrderNo >= newOrderNo ? m.OrderNo + 1 : m.OrderNo;
            })
            .ThenBy(m => m.Id == menuId ? 0 : 1) // 동일 값일 때 이동된 노드를 우선 배치
            .ToList();

        // 4. 순서대로 0부터 1씩 증가시키며 OrderNo를 새로 할당
        for (int i = 0; i < sortedSiblings.Count; i++)
        {
            sortedSiblings[i].OrderNo = i;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 로그인한 사용자가 메뉴별로 실제 가진 권한을 조회합니다.
    /// </summary>
    /// <remarks>
    /// 한 사람이 여러 역할에 속할 수 있으므로 역할들의 권한을 OR 로 합친다.
    /// 여기에 더해, 메뉴가 "사용하지 않는다"고 지정한 권한 항목은 켜져 있어도 꺼서 내려준다.
    /// (system_menus.use_* — 메뉴 관리 화면에서 정한다)
    /// 그래야 화면이 이 값 하나만 보고 버튼을 켜고 끌 수 있다.
    /// </remarks>
    public async Task<List<MenuPermissionDto>> GetMenuPermissionsAsync(string userId)
    {
        // 게이트웨이가 넘겨주는 X-User-Id 는 **로그인 아이디**(accounts.user_id)다.
        // JWT 의 NameIdentifier 에 account.UserId 를 담기 때문이다(AuthEndpoints.cs).
        // 반면 role_accounts.account_id 는 **계정 키**(accounts.id)를 가리킨다.
        // 둘은 다른 값이라(예: id=jsini-boss-quristyle / user_id=quristyle)
        // 로그인 아이디로 바로 조회하면 아무 역할도 찾지 못한다.
        // 그래서 계정을 먼저 찾아 실제 키로 바꾼 뒤 역할을 조회한다.
        var accountId = await _context.Accounts
            .Where(a => !a.IsDeleted && (a.UserId == userId || a.Id == userId))
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(accountId))
        {
            return new List<MenuPermissionDto>();
        }

        // 이 사용자가 속한 역할
        var roleIds = await _context.RoleAccounts
            .Where(ra => ra.AccountId == accountId && !ra.IsDeleted)
            .Select(ra => ra.RoleId)
            .Distinct()
            .ToListAsync();

        if (roleIds.Count == 0)
        {
            return new List<MenuPermissionDto>();
        }

        var grants = await _context.RoleMenus
            .Where(rm => roleIds.Contains(rm.RoleId) && !rm.IsDeleted)
            .ToListAsync();

        if (grants.Count == 0)
        {
            return new List<MenuPermissionDto>();
        }

        // 메뉴가 실제로 쓰는 권한 항목
        var menuIds = grants.Select(g => g.MenuId).Distinct().ToList();
        var menus = await _context.SystemMenus
            .Where(m => menuIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        return grants
            .GroupBy(g => g.MenuId)
            .Select(g =>
            {
                menus.TryGetValue(g.Key, out var menu);

                // 메뉴 정보를 못 찾으면 보수적으로 전부 막는다.
                bool Allow(Func<Entities.RoleMenu, bool> pick, Func<Entities.SystemMenu, bool> used)
                    => (menu is not null && used(menu)) && g.Any(pick);

                return new MenuPermissionDto
                {
                    MenuId = g.Key,
                    Path = menu?.Path ?? string.Empty,
                    CanView = Allow(rm => rm.CanView, m => m.UseView),
                    CanSearch = Allow(rm => rm.CanSearch, m => m.UseSearch),
                    CanCreate = Allow(rm => rm.CanCreate, m => m.UseCreate),
                    CanUpdate = Allow(rm => rm.CanUpdate, m => m.UseUpdate),
                    CanDelete = Allow(rm => rm.CanDelete, m => m.UseDelete),
                    CanPrint = Allow(rm => rm.CanPrint, m => m.UsePrint),
                    CanExcel = Allow(rm => rm.CanExcel, m => m.UseExcel),
                    CanCust1 = Allow(rm => rm.CanCust1, m => m.UseCust1),
                    CanCust2 = Allow(rm => rm.CanCust2, m => m.UseCust2),
                    CanCust3 = Allow(rm => rm.CanCust3, m => m.UseCust3),
                    CanCust4 = Allow(rm => rm.CanCust4, m => m.UseCust4),
                    CanCust5 = Allow(rm => rm.CanCust5, m => m.UseCust5),
                    CanCust6 = Allow(rm => rm.CanCust6, m => m.UseCust6),
                    CanCust7 = Allow(rm => rm.CanCust7, m => m.UseCust7),
                    CanCust8 = Allow(rm => rm.CanCust8, m => m.UseCust8)
                };
            })
            .ToList();
    }

    /// <summary>
    /// 사용자가 특정 메뉴 경로에서 실제로 가진 권한.
    /// </summary>
    /// <remarks>
    /// <b>권한이 없으면 없는 것으로 돌려준다.</b> 예전에는 권한 정보가 아예 없는 계정
    /// (역할 미배정)을 '전부 허용' 으로 다뤘다. 그러면 역할이 하나도 없는 계정이
    /// 도움말 F.A.Q 를 쓰고 자료실에 파일을 올릴 수 있는 <b>관리자</b>가 된다 —
    /// 권한을 하나도 주지 않았는데 가장 센 권한을 갖는 셈이라 방향이 거꾸로였다.
    ///
    /// <para>
    /// 조회 실패(DB 접속 불가 등)는 예외로 올라가므로 이 자리에서 빈 목록과 섞이지 않는다.
    /// 즉 빈 목록은 "못 읽었다" 가 아니라 "읽었더니 없다" 다.
    /// </para>
    /// </remarks>
    public async Task<MenuPermissionDto> GetEffectivePermissionAsync(string userId, string path)
    {
        var all = await GetMenuPermissionsAsync(userId);

        var target = Normalize(path);
        return all.FirstOrDefault(p => Normalize(p.Path) == target)
               ?? new MenuPermissionDto { Path = path };
    }

    /// <summary>끝 슬래시와 대소문자 차이를 없앤다. 화면 쪽 정규화와 같다.</summary>
    private static string Normalize(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim().ToLowerInvariant();
        return trimmed.Length > 1 && trimmed.EndsWith('/')
            ? trimmed[..^1]
            : trimmed;
    }

    // '전부 허용' 을 만들어 주던 AllowAll() 은 지웠다.
    // 권한 정보가 없는 계정을 관리자로 만들던 유일한 자리였고, 남겨 두면
    // 다음 사람이 같은 실수를 하기 쉽다. 권한이 없으면 없는 것으로 돌려준다.

    /// <summary>
    /// 여러 메뉴의 부모와 순서를 한 번에 반영합니다.
    /// </summary>
    /// <remarks>
    /// 화면(트리 그리드)이 드래그 결과로 확정한 배치를 그대로 받는다.
    /// 서버가 순번을 다시 추측하지 않으므로 화면에 보이는 순서와 저장 결과가 어긋나지 않고,
    /// 형제가 여러 개 밀려도 왕복이 한 번으로 끝난다.
    /// </remarks>
    public async Task<bool> ReorderMenusAsync(List<MenuOrderDto> items)
    {
        if (items == null || items.Count == 0)
        {
            return true;
        }

        var ids = items.Select(i => i.Id).Distinct().ToList();

        var menus = await _context.SystemMenus
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        var missing = ids.Where(id => !menus.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new KeyNotFoundException($"메뉴를 찾을 수 없습니다: {string.Join(", ", missing)}");
        }

        // 자기 자신이나 자기 하위를 부모로 지정하면 트리가 끊기므로 미리 막는다.
        var allMenus = await _context.SystemMenus.Select(m => new { m.Id, m.Pid }).ToListAsync();
        var parentMap = allMenus.ToDictionary(m => m.Id, m => m.Pid);
        foreach (var item in items)
        {
            parentMap[item.Id] = item.Pid;
        }

        foreach (var item in items)
        {
            var cursor = item.Pid;
            var hops = 0;
            while (cursor != null)
            {
                if (cursor == item.Id)
                {
                    throw new InvalidOperationException($"메뉴 '{item.Id}' 를 자기 자신의 하위로 옮길 수 없습니다.");
                }

                if (++hops > allMenus.Count)
                {
                    throw new InvalidOperationException("메뉴 계층에 순환이 있습니다.");
                }

                parentMap.TryGetValue(cursor, out cursor);
            }
        }

        foreach (var item in items)
        {
            var menu = menus[item.Id];
            menu.Pid = string.IsNullOrEmpty(item.Pid) ? null : item.Pid;
            menu.OrderNo = item.OrderNo;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    private List<MenuDto> BuildMenuTree(List<Entities.SystemMenu> allMenus, string? pid)
    {
        return allMenus
            .Where(m => m.Pid == pid)
            .Select(m => new MenuDto
            {
                Name = m.Name,
                Path = m.Path,
                Component = m.Component, // ?? "BasicLayout",
                Meta = new MenuMetaDto
                {
                    Title = m.Title ?? m.Name,
                    Icon = m.Icon,
                    Order = m.OrderNo,
                    HideInMenu = m.HideInMenu,
                    // 목록 · 브레드크럼 · 탭 바에서만 감춘다. 라우트는 그대로 만들어진다.
                    HideChildrenInMenu = m.HideChildrenInMenu,
                    HideInBreadcrumb = m.HideInBreadcrumb,
                    HideInTab = m.HideInTab,
                    KeepAlive = m.KeepAlive,
                    AffixTab = m.AffixTab,
                    DomCached = m.DomCached,
                    Component = m.Component,
                    Authority = string.IsNullOrEmpty(m.Authority) 
                        ? null 
                        : m.Authority.Split(',').ToList(),
                    MenuVisibleWithForbidden = m.MenuVisibleWithForbidden,
                    Link = m.Link,
                    IframeSrc = m.IframeSrc,
                    BadgeType = m.BadgeType,
                    Badge = m.Badge,
                    // 묶음(CATALOG)인지 화면이 있는 메뉴인지. 사이드바 거르기가 쓴다.
                    Type = m.Type,
                    // 화면 크기별 메뉴목록 노출. 화면이 이 값으로 사이드바를 걸러 낸다.
                    // 여기서 걸러 내지 않는 것은 라우트를 살려 두기 위해서다 —
                    // 휴대폰에서 목록에 없더라도 주소·즐겨찾기로는 열려야 한다.
                    UseMobile = m.UseMobile,
                    UseTablet = m.UseTablet
                },
                Children = BuildMenuTree(allMenus, m.Id)
            })
            .ToList();
    }
}
