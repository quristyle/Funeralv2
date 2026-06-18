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
