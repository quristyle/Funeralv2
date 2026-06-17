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
    /// </summary>
    /// <returns>계층화된 시스템 메뉴 목록</returns>
    public async Task<List<SystemMenuDto>> GetMenuListAsync()
    {
        var menus = await _db.SystemMenus.OrderBy(m => m.OrderNo).ToListAsync();
        return BuildMenuTree(menus, null);
    }

    /// <summary>
    /// 평면적인 메뉴 리스트를 재귀적으로 호출하여 트리 구조로 조립합니다.
    /// </summary>
    /// <param name="menus">전체 메뉴 리스트</param>
    /// <param name="parentId">부모 메뉴 ID</param>
    /// <returns>자식 메뉴들을 포함한 메뉴 리스트</returns>
    private List<SystemMenuDto> BuildMenuTree(List<SystemMenu> menus, string? parentId)
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
                Meta = new SystemMenuMetaDto
                {
                    Title = m.Title,
                    Icon = m.Icon,
                    Order = m.OrderNo,
                    HideInMenu = m.HideInMenu,
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
                    Badge = m.Badge
                },
                Children = BuildMenuTree(menus, m.Id).Any() ? BuildMenuTree(menus, m.Id) : null
            })
            .ToList();
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
            Title = request.Meta.Title,
            Icon = request.Meta.Icon,
            OrderNo = request.Meta.Order,
            HideInMenu = request.Meta.HideInMenu,
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
            Badge = request.Meta.Badge
        };
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
        menu.Title = request.Meta.Title;
        menu.Icon = request.Meta.Icon;
        menu.OrderNo = request.Meta.Order;
        menu.HideInMenu = request.Meta.HideInMenu;
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
