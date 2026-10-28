using AuthServer.Data;
using AuthServer.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>사이드바 메뉴 트리 조회 서비스 구현체</summary>
public class MenuService : IMenuService
{
    private readonly AppDbContext _context;
    private readonly MenuTreeCache _cache;

    public MenuService(AppDbContext context, MenuTreeCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>사용자가 접근 가능한 모든 메뉴 목록을 트리 구조로 반환한다.</summary>
    /// <param name="userId">사용자 식별자. 지금은 쓰지 않는다 — 응답은 활성 메뉴 전부이고 사용자별 거르기는 프론트가 한다.</param>
    /// <param name="locale">번역할 언어. 비우면 기본 언어.</param>
    public Task<List<MenuDto>> GetAllMenusAsync(string userId, string? locale = null) =>
        _cache.GetOrLoadAsync(locale, () => LoadAllMenusAsync(locale));

    /// <summary>DB 에서 실제로 읽어 트리를 만든다. 캐시가 비었을 때만 돈다.</summary>
    private async Task<List<MenuDto>> LoadAllMenusAsync(string? locale)
    {
        var allMenus = await _context.SystemMenus
            .Where(m => m.Status == 1)
            .OrderBy(m => m.OrderNo)
            .ToListAsync();

        // 제목 다국어를 사전으로 한 번에 붙인다 — 메뉴를 내려보내는 곳(SystemMenuService)과 같은 규칙이다.
        var titles = await MenuTitleTranslator.LoadAsync(_context, allMenus, locale);

        var menuTree = BuildMenuTree(allMenus, null, titles);

        return menuTree;
    }

    /// <summary>메뉴의 위치(부모)와 순서를 변경한다.</summary>
    public async Task<bool> MoveMenuAsync(string menuId, string? newParentId, int newOrderNo)
    {
        var menu = await _context.SystemMenus.FindAsync(menuId);
        if (menu == null)
        {
            throw new KeyNotFoundException($"메뉴 ID '{menuId}'에 해당하는 메뉴를 찾을 수 없습니다.");
        }

        menu.Pid = newParentId;
        menu.OrderNo = newOrderNo;

        var siblings = await _context.SystemMenus
            .Where(m => m.Pid == newParentId)
            .ToListAsync();

        // 새 순서는 대상 노드 기준으로 정렬하고, 같거나 큰 형제는 뒤로 밀어 순차 번호를 만든다.
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

        for (int i = 0; i < sortedSiblings.Count; i++)
        {
            sortedSiblings[i].OrderNo = i;
        }

        await _context.SaveChangesAsync();
        _cache.Invalidate();
        return true;
    }

    /// <summary>로그인한 사용자가 메뉴별로 실제 가진 권한을 조회한다. 역할들의 권한은 OR 로 합치고, 메뉴가 사용하지 않는다고 지정한 권한 항목은 꺼서 내려준다(<c>system_menus.use_*</c>).</summary>
    public async Task<List<MenuPermissionDto>> GetMenuPermissionsAsync(string userId)
    {
        // 계정·역할·부여·메뉴를 한 번의 왕복으로 읽는다 — DbContext 는 동시 사용을 허용하지 않으므로 병렬로 돌릴 수도 없다.
        var rows = await (
            from a in _context.Accounts
                .Where(a => !a.IsDeleted && (a.UserId == userId || a.Id == userId))
                .Take(1)
            join ra in _context.RoleAccounts on a.Id equals ra.AccountId into ras
            from ra in ras.Where(r => !r.IsDeleted)
            join rm in _context.RoleMenus on ra.RoleId equals rm.RoleId into rms
            from rm in rms.Where(r => !r.IsDeleted)
            join m in _context.SystemMenus on rm.MenuId equals m.Id
            select new { Grant = rm, Menu = m })
            .AsNoTracking()
            .ToListAsync();

        if (rows.Count == 0)
        {
            return new List<MenuPermissionDto>();
        }

        // 메뉴가 없는 부여는 안쪽 조인이 걸러 준다 — 받는 쪽은 빈 줄을 버리므로 결과가 같다.
        return rows
            .GroupBy(row => row.Grant.MenuId)
            .Select(g =>
            {
                var menu = g.First().Menu;

                bool Allow(Func<Entities.RoleMenu, bool> pick, Func<Entities.SystemMenu, bool> used)
                    => used(menu) && g.Any(row => pick(row.Grant));

                return new MenuPermissionDto
                {
                    MenuId = g.Key,
                    Path = menu.Path ?? string.Empty,
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

    /// <summary>사용자가 특정 메뉴 경로에서 실제로 가진 권한. 권한이 없으면 없는 것으로 돌려준다 — 역할이 없는 계정을 전부 허용으로 다루면 안 된다.</summary>
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


    /// <summary>여러 메뉴의 부모와 순서를 한 번에 반영한다. 화면이 확정한 배치를 그대로 받아 서버가 순번을 다시 추측하지 않는다.</summary>
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
        _cache.Invalidate();
        return true;
    }

    private List<MenuDto> BuildMenuTree(
        List<Entities.SystemMenu> allMenus, string? pid, Dictionary<string, string> titles)
    {
        return allMenus
            .Where(m => m.Pid == pid)
            .Select(m => new MenuDto
            {
                Name = m.Name,
                Path = m.Path,
                // 프론트가 링크 주소를 푸는 열쇠. Path 가 아니라 이쪽이다.
                RouteKey = m.RouteKey,
                // 엔티티는 비워 둘 수 있지만 DTO 는 빈 문자열까지만 허용한다.
                // 프론트(Blazor)는 이 칸을 읽지 않는다 — 라우트는 @page 가 정한다.
                Component = m.Component ?? string.Empty,
                Meta = new MenuMetaDto
                {
                    Title = m.Title ?? m.Name,
                    // 사전에서 찾았을 때만 담는다. 못 찾으면 null 이라
                    // 화면이 저장된 제목을 그대로 쓴다.
                    TitleText = MenuTitleTranslator.Resolve(m.Title, titles),
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
                    Component = m.Component ?? string.Empty,
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
                    // 화면 크기별 메뉴목록 노출 — 여기서 거르지 않는 것은 라우트를 살리기 위해서다. 휴대폰에서 목록에 없어도 주소·즐겨찾기로는 열려야 한다.
                    UseMobile = m.UseMobile,
                    UseTablet = m.UseTablet
                },
                Children = BuildMenuTree(allMenus, m.Id, titles)
            })
            .ToList();
    }

    /// <summary>메뉴의 keepAlive 설정을 변경한다. 메뉴 관리 화면과 동일하게 scom.system_menus.keep_alive 를 갱신한다.</summary>
    public async Task<bool> SetKeepAliveAsync(string? path, string? routeKey, bool keepAlive)
    {
        var menu = await _context.SystemMenus
            .FirstOrDefaultAsync(m =>
                (!string.IsNullOrEmpty(routeKey) && m.RouteKey == routeKey)
                || (!string.IsNullOrEmpty(path) && m.Path == path));

        if (menu == null && !string.IsNullOrEmpty(path))
        {
            var trimmed = path.TrimStart('/');
            menu = await _context.SystemMenus
                .FirstOrDefaultAsync(m => m.Path == "/" + trimmed || m.Path == trimmed);
        }

        if (menu == null)
        {
            return false;
        }

        menu.KeepAlive = keepAlive;
        await _context.SaveChangesAsync();
        _cache.Invalidate();
        return true;
    }
}
