using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 시스템 메뉴 관리 기능을 구현하는 서비스 클래스
/// </summary>
public class SystemMenuService : ISystemMenuService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// SystemMenuService의 생성자
    /// </summary>
    /// <param name="db">데이터베이스 컨텍스트</param>
    public SystemMenuService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 데이터베이스에서 전체 메뉴를 조회하여 계층형 트리 구조로 변환 후 반환합니다.
    ///
    /// <para>
    /// [제목의 다국어를 여기서 붙인다]
    ///
    /// 예전에는 화면이 제목마다 <c>$t()</c> 를 불러 옮겼다. 그런데 저장된 제목
    /// 대부분(180건 중 166건)이 번역 키가 아니라 이미 완성된 글자라서, vue-i18n 이
    /// "그런 키는 없다" 는 경고를 <b>한 번 새로 그릴 때마다 492줄</b> 쏟아냈다.
    /// 옮길 것이 적은데도 화면이 늦게 뜬 이유가 이것이다.
    ///
    /// 그래서 번역을 <b>서버에서 한 번에</b> 붙인다. 언어 하나에 해당하는
    /// <c>scom.i18n_resources</c> 를 사전 하나로 읽어(왕복 한 번) 제목을 맞춰 본다.
    /// 화면은 내려온 글자를 그대로 찍기만 하므로 옮기는 일이 아예 없어진다.
    /// </para>
    /// </summary>
    /// <param name="locale">제목을 옮길 언어. 비우면 <c>ko</c>.</param>
    /// <returns>계층화된 시스템 메뉴 목록</returns>
    public async Task<List<SystemMenuDto>> GetMenuListAsync(string? locale = null)
    {
        var menus = await _db.SystemMenus.OrderBy(m => m.OrderNo).ToListAsync();
        var titles = await MenuTitleTranslator.LoadAsync(_db, menus, locale);
        return BuildMenuTree(menus, null, titles);
    }


    /// <summary>
    /// 평면적인 메뉴 리스트를 재귀적으로 호출하여 트리 구조로 조립합니다.
    /// </summary>
    /// <param name="menus">전체 메뉴 리스트</param>
    /// <param name="parentId">부모 메뉴 ID</param>
    /// <param name="titles">제목 → 옮긴 글자 사전</param>
    /// <returns>자식 메뉴들을 포함한 메뉴 리스트</returns>
    private List<SystemMenuDto> BuildMenuTree(
        List<SystemMenu> menus, string? parentId, Dictionary<string, string> titles)
    {
        return menus
            .Where(m => m.Pid == parentId)
            .Select(m => new SystemMenuDto
            {
                Id = m.Id,
                Name = m.Name,
                Path = m.Path,
                Component = m.Component,
                Pid = m.Pid,
                Redirect = m.Redirect,
                Type = m.Type,
                AuthCode = m.AuthCode,
                // 이 한 줄이 빠져 있어서 메뉴 관리 화면이 상태를 받지 못했다.
                // 폼의 스위치는 기본값 1(활성)을 들고 있었으므로 비활성 메뉴도 '활성'으로 보였다.
                Status = m.Status,
                Meta = new SystemMenuMetaDto
                {
                    Title = m.Title,
                    // 사전에서 찾았을 때만 담는다. 못 찾으면 null 이라 화면이 알던 방식으로
                    // (프론트 언어 파일까지 보는 `$tIfKey`) 처리한다.
                    TitleText = MenuTitleTranslator.Resolve(m.Title, titles),
                    Icon = m.Icon,
                    Order = m.OrderNo,
                    HideInMenu = m.HideInMenu,
                    HideChildrenInMenu = m.HideChildrenInMenu,
                    HideInBreadcrumb = m.HideInBreadcrumb,
                    HideInTab = m.HideInTab,
                    KeepAlive = m.KeepAlive,
                    AffixTab = m.AffixTab,
                    DomCached = m.DomCached,
                    Authority = string.IsNullOrEmpty(m.Authority) 
                        ? null 
                        : m.Authority.Split(',').ToList(),
                    MenuVisibleWithForbidden = m.MenuVisibleWithForbidden,
                    Link = m.Link,
                    IframeSrc = m.IframeSrc,
                    BadgeType = m.BadgeType,
                    Badge = m.Badge,
                    UseMobile = m.UseMobile,
                    UseTablet = m.UseTablet
                },
                Permissions = ToPermissionsDto(m),
                // 같은 가지를 두 번 조립하지 않는다. 예전에는 `Any()` 로 한 번,
                // 값으로 또 한 번 만들어 메뉴 깊이만큼 일이 곱절로 늘었다.
                Children = BuildMenuTree(menus, m.Id, titles) is { Count: > 0 } kids ? kids : null
            })
            .ToList();
    }

    /// <summary>
    /// 엔티티의 권한 항목 설정을 DTO 로 옮깁니다.
    /// </summary>
    private static MenuPermissionItemsDto ToPermissionsDto(SystemMenu m) => new()
    {
        UseView = m.UseView,
        UseSearch = m.UseSearch,
        UseCreate = m.UseCreate,
        UseDelete = m.UseDelete,
        UseUpdate = m.UseUpdate,
        UsePrint = m.UsePrint,
        UseExcel = m.UseExcel,
        UseCust1 = m.UseCust1,
        UseCust2 = m.UseCust2,
        UseCust3 = m.UseCust3,
        UseCust4 = m.UseCust4,
        UseCust5 = m.UseCust5,
        UseCust6 = m.UseCust6,
        UseCust7 = m.UseCust7,
        UseCust8 = m.UseCust8,
        Cust1Name = m.Cust1Name,
        Cust2Name = m.Cust2Name,
        Cust3Name = m.Cust3Name,
        Cust4Name = m.Cust4Name,
        Cust5Name = m.Cust5Name,
        Cust6Name = m.Cust6Name,
        Cust7Name = m.Cust7Name,
        Cust8Name = m.Cust8Name
    };

    /// <summary>
    /// 요청의 권한 항목 설정을 엔티티에 반영합니다.
    /// 사용하지 않는 사용자 정의 항목의 이름은 남겨두지 않는다 —
    /// 꺼둔 칸에 예전 이름이 남아 있으면 다시 켰을 때 엉뚱한 이름이 붙는다.
    /// </summary>
    private static void ApplyPermissions(SystemMenu menu, MenuPermissionItemsDto p)
    {
        menu.UseView = p.UseView;
        menu.UseSearch = p.UseSearch;
        menu.UseCreate = p.UseCreate;
        menu.UseDelete = p.UseDelete;
        menu.UseUpdate = p.UseUpdate;
        menu.UsePrint = p.UsePrint;
        menu.UseExcel = p.UseExcel;

        menu.UseCust1 = p.UseCust1;
        menu.UseCust2 = p.UseCust2;
        menu.UseCust3 = p.UseCust3;
        menu.UseCust4 = p.UseCust4;
        menu.UseCust5 = p.UseCust5;
        menu.UseCust6 = p.UseCust6;
        menu.UseCust7 = p.UseCust7;
        menu.UseCust8 = p.UseCust8;

        menu.Cust1Name = p.UseCust1 ? p.Cust1Name : null;
        menu.Cust2Name = p.UseCust2 ? p.Cust2Name : null;
        menu.Cust3Name = p.UseCust3 ? p.Cust3Name : null;
        menu.Cust4Name = p.UseCust4 ? p.Cust4Name : null;
        menu.Cust5Name = p.UseCust5 ? p.Cust5Name : null;
        menu.Cust6Name = p.UseCust6 ? p.Cust6Name : null;
        menu.Cust7Name = p.UseCust7 ? p.Cust7Name : null;
        menu.Cust8Name = p.UseCust8 ? p.Cust8Name : null;
    }

    /// <summary>
    /// 메뉴 명칭의 중복 여부를 데이터베이스에서 확인합니다.
    /// </summary>
    public async Task<bool> IsNameExistsAsync(string name, string? id)
    {
        return await _db.SystemMenus.AnyAsync(m => m.Name == name && m.Id != id);
    }

    /// <summary>
    /// 메뉴 접속 경로의 중복 여부를 데이터베이스에서 확인합니다.
    /// </summary>
    public async Task<bool> IsPathExistsAsync(string path, string? id)
    {
        return await _db.SystemMenus.AnyAsync(m => m.Path == path && m.Id != id);
    }

    /// <summary>
    /// 새로운 메뉴 엔티티를 생성하고 저장합니다.
    /// </summary>
    public async Task<SystemMenuDto> CreateMenuAsync(CreateSystemMenuDto request)
    {
        var menu = new SystemMenu
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Path = request.Path,
            Component = request.Component,
            Pid = request.Pid,
            Redirect = request.Redirect,
            Type = request.Type,
            AuthCode = request.AuthCode,
            // 안 보내면 엔티티 기본값(활성)을 쓴다.
            Status = request.Status ?? 1,
            Title = request.Meta.Title,
            Icon = request.Meta.Icon,
            OrderNo = request.Meta.Order,
            HideInMenu = request.Meta.HideInMenu,
            HideChildrenInMenu = request.Meta.HideChildrenInMenu,
            HideInBreadcrumb = request.Meta.HideInBreadcrumb,
            HideInTab = request.Meta.HideInTab,
            KeepAlive = request.Meta.KeepAlive,
            AffixTab = request.Meta.AffixTab,
            DomCached = request.Meta.DomCached,
            Authority = request.Meta.Authority != null 
                ? string.Join(',', request.Meta.Authority) 
                : null,
            MenuVisibleWithForbidden = request.Meta.MenuVisibleWithForbidden,
            Link = request.Meta.Link,
            IframeSrc = request.Meta.IframeSrc,
            BadgeType = request.Meta.BadgeType,
            Badge = request.Meta.Badge,
            UseMobile = request.Meta.UseMobile,
            UseTablet = request.Meta.UseTablet
        };
        ApplyPermissions(menu, request.Permissions);
        _db.SystemMenus.Add(menu);
        await _db.SaveChangesAsync();

        return new SystemMenuDto { Id = menu.Id, Name = menu.Name, Path = menu.Path };
    }

    /// <summary>
    /// 기존 메뉴 엔티티 정보를 업데이트합니다.
    /// </summary>
    public async Task<bool> UpdateMenuAsync(string id, CreateSystemMenuDto request)
    {
        var menu = await _db.SystemMenus.FindAsync(id);
        if (menu == null) return false;

        menu.Name = request.Name;
        menu.Path = request.Path;
        menu.Component = request.Component;
        menu.Pid = request.Pid;
        menu.Redirect = request.Redirect;
        menu.Type = request.Type;
        menu.AuthCode = request.AuthCode;
        // 값을 실어 보낸 요청만 상태를 바꾼다. 안 보낸 요청은 지금 상태를 그대로 둔다.
        if (request.Status.HasValue) menu.Status = request.Status.Value;
        menu.Title = request.Meta.Title;
        menu.Icon = request.Meta.Icon;
        menu.OrderNo = request.Meta.Order;
        menu.HideInMenu = request.Meta.HideInMenu;
        menu.HideChildrenInMenu = request.Meta.HideChildrenInMenu;
        menu.HideInBreadcrumb = request.Meta.HideInBreadcrumb;
        menu.HideInTab = request.Meta.HideInTab;
        menu.KeepAlive = request.Meta.KeepAlive;
        menu.AffixTab = request.Meta.AffixTab;
        menu.DomCached = request.Meta.DomCached;
        menu.Authority = request.Meta.Authority != null 
            ? string.Join(',', request.Meta.Authority) 
            : null;
        menu.MenuVisibleWithForbidden = request.Meta.MenuVisibleWithForbidden;
        menu.Link = request.Meta.Link;
        menu.IframeSrc = request.Meta.IframeSrc;
        menu.BadgeType = request.Meta.BadgeType;
        menu.Badge = request.Meta.Badge;
        // 메타에 실려 오지 않으면 DTO 기본값(true)이 들어온다.
        // 메뉴 관리 화면은 조회한 meta 를 통째로 되돌려 보내므로 값이 항상 실린다.
        menu.UseMobile = request.Meta.UseMobile;
        menu.UseTablet = request.Meta.UseTablet;
        ApplyPermissions(menu, request.Permissions);

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 특정 메뉴를 삭제합니다. (하위 메뉴 처리는 필요 시 추가 구현)
    /// </summary>
    public async Task<bool> DeleteMenuAsync(string id)
    {
        var menu = await _db.SystemMenus.FindAsync(id);
        if (menu == null) return false;

        _db.SystemMenus.Remove(menu);
        await _db.SaveChangesAsync();
        return true;
    }
}
