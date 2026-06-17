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
                    Badge = m.Badge
                },
                Children = BuildMenuTree(allMenus, m.Id)
            })
            .ToList();
    }
}
