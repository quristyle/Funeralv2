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
    private readonly MenuTreeCache _cache;

    public MenuService(AppDbContext context, MenuTreeCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// 사용자가 접근 가능한 모든 메뉴 목록을 트리 구조로 반환
    /// </summary>
    /// <param name="userId">
    /// 사용자 식별자. <b>지금은 쓰지 않는다</b> — 이 응답은 활성 메뉴 전부이고
    /// 사용자별로 거르는 일은 프론트가 권한표로 한다. 그래서 트리를 사람마다
    /// 만들지 않고 <see cref="MenuTreeCache"/> 에 한 벌만 둔다.
    /// 여기서 거르도록 바꾼다면 그 캐시부터 손봐야 한다.
    /// </param>
    /// <param name="locale">제목을 옮길 언어. 비우면 <c>ko</c>. 캐시도 이 값마다 따로 둔다.</param>
    public Task<List<MenuDto>> GetAllMenusAsync(string userId, string? locale = null) =>
        _cache.GetOrLoadAsync(locale, () => LoadAllMenusAsync(locale));

    /// <summary>DB 에서 실제로 읽어 트리를 만든다. 캐시가 비었을 때만 돈다.</summary>
    private async Task<List<MenuDto>> LoadAllMenusAsync(string? locale)
    {
        // 1. 모든 활성 메뉴 조회
        var allMenus = await _context.SystemMenus
            .Where(m => m.Status == 1)
            .OrderBy(m => m.OrderNo)
            .ToListAsync();

        // 2. 제목의 다국어를 붙일 사전을 읽는다 (왕복 한 번).
        //
        // **사이드바가 키를 그대로 보여 주던 것을 여기서 고쳤다.** 메뉴를
        // 내려보내는 곳이 둘인데(여기와 SystemMenuService) 한동안 뒤엣것만
        // 번역을 붙였다. 그래서 메뉴 관리 화면에서는 "메뉴 관리" 로 보이는
        // 항목이 사이드바에서는 system.menu.title 로 보였다.
        var titles = await MenuTitleTranslator.LoadAsync(_context, allMenus, locale);

        // 3. 트리 구조로 변환
        var menuTree = BuildMenuTree(allMenus, null, titles);

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
        _cache.Invalidate();
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
        // ── 왕복 하나로 읽는다 ───────────────────────────────
        //
        // 계정 → 역할 → 부여 → 메뉴를 **차례로** 네 번 읽던 자리다. 네 번째가
        // 세 번째의 결과를 필요로 하는 모양이라 병렬로 돌릴 수도 없었고
        // (DbContext 는 동시 사용을 허용하지 않는다), 개발 장비처럼 DB 가 원격이면
        // 왕복 하나가 28.8ms 라 그것만 115ms 였다.
        //
        // 조인 하나로 쓸 수 있다. EF 가 계정 서브질의에 LIMIT 1 을 붙인 **한
        // 문장**으로 번역하는 것을 확인했고, 실제 계정 넷과 없는 계정으로 옛
        // 방식과 결과가 같은 것도 대조했다.
        //
        // [계정을 왜 두 열로 찾나]
        //
        // 게이트웨이가 넘겨주는 X-User-Id 는 **로그인 아이디**(accounts.user_id)다.
        // JWT 의 NameIdentifier 에 account.UserId 를 담기 때문이다(AuthEndpoints.cs).
        // 반면 role_accounts.account_id 는 **계정 키**(accounts.id)를 가리킨다.
        // 둘은 다른 값이라(예: id=jsini-boss-quristyle / user_id=quristyle)
        // 로그인 아이디로 바로 조회하면 아무 역할도 찾지 못한다.
        //
        // [Take(1) 을 빼면 안 된다]
        //
        // 옛 코드의 FirstOrDefault 자리다. 빼면 두 열 중 어느 쪽으로든 걸리는
        // 계정이 **여럿** 매칭될 수 있고, 그러면 그 계정들의 역할이 OR 로 합쳐진다 —
        // 틀리는 방향이 「없는 권한이 생기는」 쪽이라 특히 나쁘다.
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

        // [메뉴가 없는 부여는 조인이 걸러 준다]
        //
        // 옛 코드는 메뉴를 못 찾으면 권한을 전부 끈 줄을 그대로 내려보냈다.
        // 안쪽 조인은 그 줄을 아예 빼는데, 받는 쪽에게는 같다 — 프론트가
        // `Path` 가 빈 줄을 버리고(PermissionContext.Apply), 경로로 찾는
        // GetEffectivePermissionAsync 도 빈 경로에는 걸리지 않는다.
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
                    // 화면 크기별 메뉴목록 노출. 화면이 이 값으로 사이드바를 걸러 낸다.
                    // 여기서 걸러 내지 않는 것은 라우트를 살려 두기 위해서다 —
                    // 휴대폰에서 목록에 없더라도 주소·즐겨찾기로는 열려야 한다.
                    UseMobile = m.UseMobile,
                    UseTablet = m.UseTablet
                },
                Children = BuildMenuTree(allMenus, m.Id, titles)
            })
            .ToList();
    }
}
