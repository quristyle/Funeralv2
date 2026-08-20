using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Utilities;
using System.Security.Claims;

namespace HelpDeskServer.Endpoints;

public static class MenuEndpoints {
    public record MenuMoveDto(int Id, int? ParentId, int NewSortOrder);
    
    public record MenuDto {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? To { get; set; }
        public string? Url { get; set; }
        public int? ParentId { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool Visible { get; set; }
        public bool UseCreate { get; set; }
        public bool UseRead { get; set; }
        public bool UseUpdate { get; set; }
        public bool UseDelete { get; set; }
        public bool UseExt1 { get; set; }
        public string? Ext1Name { get; set; }
        public bool UseExt2 { get; set; }
        public string? Ext2Name { get; set; }
        public bool UseExt3 { get; set; }
        public string? Ext3Name { get; set; }
        public bool UseExt4 { get; set; }
        public string? Ext4Name { get; set; }
        public bool UseExt5 { get; set; }
        public string? Ext5Name { get; set; }
        public bool UseExt6 { get; set; }
        public string? Ext6Name { get; set; }
        public bool UseExt7 { get; set; }
        public string? Ext7Name { get; set; }
        public bool UseExt8 { get; set; }
        public string? Ext8Name { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<MenuDto> Items { get; set; } = new();
    }

    public static void MapMenuEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/menus");

        // 1. 사용자별 사이드바 메뉴 조회 (Visible = true 필터링)
        group.MapGet("/", (HttpContext http, AppDbContext db) => ApiResponseBuilder.CreateAsync(async () => {
            var userIdStr = http.User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
            var userType = http.User.Claims.FirstOrDefault(c => c.Type == "login_type")?.Value;

            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return new List<MenuDto>();

            var userRoleIds = await db.UserRoles.Where(ur => ur.UserType == userType && ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync();
            var allowedMenuIds = await db.RoleMenuPermissions.Where(p => userRoleIds.Contains(p.RoleId) && p.CanRead).Select(p => p.MenuId).Distinct().ToListAsync();

            var allMenus = await db.Menus.Include(m => m.MenuRoles)
                .Where(m => m.IsActive) // 활성화된 메뉴만
                .OrderBy(m => m.SortOrder).ToListAsync();

            var filteredMenus = allMenus.Where(m => allowedMenuIds.Contains(m.Id)).ToList();
            return BuildMenuTreeDto(filteredMenus, null);
        })).RequireAuthorization();

        // 2. 관리용 전체 메뉴 조회
        group.MapGet("/manage", (AppDbContext db) => ApiResponseBuilder.CreateAsync(async () => {
            var menus = await db.Menus.Include(m => m.MenuRoles).OrderBy(m => m.SortOrder).ToListAsync();
            return menus.Select(m => new {
                m.Id, m.Label, m.Icon, m.To, m.Url, m.ParentId, m.SortOrder, m.IsActive, m.Visible,
                m.UseCreate, m.UseRead, m.UseUpdate, m.UseDelete,
                m.UseExt1, m.Ext1Name, m.UseExt2, m.Ext2Name, m.UseExt3, m.Ext3Name, m.UseExt4, m.Ext4Name,
                m.UseExt5, m.Ext5Name, m.UseExt6, m.Ext6Name, m.UseExt7, m.Ext7Name, m.UseExt8, m.Ext8Name,
                Roles = m.MenuRoles.Select(r => r.RoleName).ToList()
            }).ToList();
        })).RequireAuthorization();

        // 3. 생성
        group.MapPost("/", (AppDbContext db, MenuDto input) => ApiResponseBuilder.CreateAsync(async () => {
            var menu = new Menu {
                Label = input.Label, Icon = input.Icon, To = input.To, Url = input.Url,
                ParentId = input.ParentId, SortOrder = input.SortOrder, IsActive = input.IsActive, Visible = input.Visible,
                UseCreate = input.UseCreate, UseRead = input.UseRead, UseUpdate = input.UseUpdate, UseDelete = input.UseDelete,
                UseExt1 = input.UseExt1, Ext1Name = input.Ext1Name, UseExt2 = input.UseExt2, Ext2Name = input.Ext2Name,
                UseExt3 = input.UseExt3, Ext3Name = input.Ext3Name, UseExt4 = input.UseExt4, Ext4Name = input.Ext4Name,
                UseExt5 = input.UseExt5, Ext5Name = input.Ext5Name, UseExt6 = input.UseExt6, Ext6Name = input.Ext6Name,
                UseExt7 = input.UseExt7, Ext7Name = input.Ext7Name, UseExt8 = input.UseExt8, Ext8Name = input.Ext8Name,
                MenuRoles = input.Roles.Select(r => new MenuRole { RoleName = r }).ToList()
            };
            db.Menus.Add(menu);
            await db.SaveChangesAsync();
            return menu;
        }, "Menu created successfully.", 201)).RequireAuthorization();

        // 4. 수정
        group.MapPut("/{id}", (AppDbContext db, int id, MenuDto input) => ApiResponseBuilder.CreateAsync(async () => {
            var menu = await db.Menus.Include(m => m.MenuRoles).FirstOrDefaultAsync(m => m.Id == id);
            if (menu is null) return null;
            menu.Label = input.Label; menu.Icon = input.Icon; menu.To = input.To; menu.Url = input.Url;
            menu.ParentId = input.ParentId; menu.SortOrder = input.SortOrder; menu.IsActive = input.IsActive; menu.Visible = input.Visible;
            menu.UseCreate = input.UseCreate; menu.UseRead = input.UseRead; menu.UseUpdate = input.UseUpdate; menu.UseDelete = input.UseDelete;
            menu.UseExt1 = input.UseExt1; menu.Ext1Name = input.Ext1Name; menu.UseExt2 = input.UseExt2; menu.Ext2Name = input.Ext2Name;
            menu.UseExt3 = input.UseExt3; menu.Ext3Name = input.Ext3Name; menu.UseExt4 = input.UseExt4; menu.Ext4Name = input.Ext4Name;
            menu.UseExt5 = input.UseExt5; menu.Ext5Name = input.Ext5Name; menu.UseExt6 = input.UseExt6; menu.Ext6Name = input.Ext6Name;
            menu.UseExt7 = input.UseExt7; menu.Ext7Name = input.Ext7Name; menu.UseExt8 = input.UseExt8; menu.Ext8Name = input.Ext8Name;
            db.MenuRoles.RemoveRange(menu.MenuRoles);
            menu.MenuRoles = input.Roles.Select(r => new MenuRole { RoleName = r }).ToList();
            await db.SaveChangesAsync();
            return menu;
        }, "Menu updated successfully.")).RequireAuthorization();

        // ... 나머지 삭제 및 이동 엔드포인트 기존 유지
        group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {
            var menu = await db.Menus.FindAsync(id);
            if (menu is null) return null;
            db.Menus.Remove(menu);
            await db.SaveChangesAsync();
            return new { DeletedId = id };
        })).RequireAuthorization();

        group.MapPost("/move", (AppDbContext db, MenuMoveDto moveDto) => ApiResponseBuilder.CreateAsync(async () => {
            var menu = await db.Menus.FindAsync(moveDto.Id);
            if (menu is null) return null;
            menu.ParentId = moveDto.ParentId;
            menu.SortOrder = moveDto.NewSortOrder;
            await db.SaveChangesAsync();
            return menu;
        })).RequireAuthorization();
    }

    private static List<MenuDto> BuildMenuTreeDto(List<Menu> menus, int? parentId) {
        return menus.Where(m => m.ParentId == parentId).OrderBy(m => m.SortOrder).Select(m => new MenuDto {
            Id = m.Id, Label = m.Label, Icon = m.Icon, To = m.To, Url = m.Url, ParentId = m.ParentId, SortOrder = m.SortOrder, IsActive = m.IsActive, Visible = m.Visible,
            UseRead = m.UseRead, UseCreate = m.UseCreate, UseUpdate = m.UseUpdate, UseDelete = m.UseDelete,
            Roles = m.MenuRoles.Select(r => r.RoleName).ToList(),
            Items = BuildMenuTreeDto(menus, m.Id)
        }).ToList();
    }
}
