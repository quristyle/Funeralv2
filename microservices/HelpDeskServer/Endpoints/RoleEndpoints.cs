using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using HelpDeskServer.Utilities;

namespace HelpDeskServer.Endpoints;

public static class RoleEndpoints {
    public record UserRoleRequest(int RoleId, string UserType, int UserId);
    
    public static void MapRoleEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/roles");

        // 1. 모든 권한 그룹 조회
        group.MapGet("/", (AppDbContext db) => ApiResponseBuilder.CreateAsync(
            () => db.Roles.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync()
        )).RequireAuthorization();

        // 2. 권한 그룹 생성
        group.MapPost("/", (AppDbContext db, AppRole role) => ApiResponseBuilder.CreateAsync(async () => {
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            return role;
        }, "Role created successfully.", 201)).RequireAuthorization();

        // 3. 권한 그룹 수정
        group.MapPut("/{id}", (AppDbContext db, int id, AppRole input) => ApiResponseBuilder.CreateAsync(async () => {
            var role = await db.Roles.FindAsync(id);
            if (role is null) return null;
            role.Name = input.Name;
            role.DisplayName = input.DisplayName;
            role.Description = input.Description;
            role.SortOrder = input.SortOrder;
            await db.SaveChangesAsync();
            return role;
        })).RequireAuthorization();

        // 4. 권한 그룹 삭제
        group.MapDelete("/{id}", (AppDbContext db, int id) => ApiResponseBuilder.CreateAsync(async () => {
            var role = await db.Roles.FindAsync(id);
            if (role is null) return null;
            db.Roles.Remove(role);
            await db.SaveChangesAsync();
            return new { DeletedId = id };
        })).RequireAuthorization();

        // 5. 그룹별 소속 사용자 조회
        group.MapGet("/{roleId}/users", (AppDbContext db, int roleId) => ApiResponseBuilder.CreateAsync(async () => {
            var userRoles = await db.UserRoles.Where(ur => ur.RoleId == roleId).ToListAsync();
            
            var adminIds = userRoles.Where(ur => ur.UserType == "admin").Select(ur => ur.UserId).ToList();
            var customerIds = userRoles.Where(ur => ur.UserType == "customer").Select(ur => ur.UserId).ToList();

            var admins = await db.Admins.Where(a => adminIds.Contains(a.Id))
                .Select(a => new { a.Id, Name = a.UserName, LoginId = a.LoginId, Type = "admin", CompanyId = (int?)null }).ToListAsync();
            
            var customers = await db.Customers.Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, Name = c.UserName, LoginId = c.LoginId, Type = "customer", CompanyId = (int?)c.CompanyId }).ToListAsync();

            return admins.Cast<object>().Concat(customers.Cast<object>()).ToList();
        })).RequireAuthorization();

        // 6. 그룹에 사용자 추가
        group.MapPost("/users", (AppDbContext db, UserRoleRequest req) => ApiResponseBuilder.CreateAsync(async () => {
            var exists = await db.UserRoles.AnyAsync(ur => ur.RoleId == req.RoleId && ur.UserType == req.UserType && ur.UserId == req.UserId);
            if (exists) return null;

            var userRole = new AppUserRole { RoleId = req.RoleId, UserType = req.UserType, UserId = req.UserId };
            db.UserRoles.Add(userRole);
            await db.SaveChangesAsync();
            return userRole;
        })).RequireAuthorization();

        // 7. 그룹에서 사용자 제거
        group.MapDelete("/{roleId}/users/{userType}/{userId}", (AppDbContext db, int roleId, string userType, int userId) => ApiResponseBuilder.CreateAsync(async () => {
            var userRole = await db.UserRoles.FirstOrDefaultAsync(ur => ur.RoleId == roleId && ur.UserType == userType && ur.UserId == userId);
            if (userRole is null) return null;

            db.UserRoles.Remove(userRole);
            await db.SaveChangesAsync();
            return new { Success = true };
        })).RequireAuthorization();

        // 8. 검색용 전체 사용자 목록
        routes.MapGet("/api/common/users", (AppDbContext db) => ApiResponseBuilder.CreateAsync(async () => {
            var admins = await db.Admins.Select(a => new { a.Id, Name = a.UserName, LoginId = a.LoginId, Type = "admin", CompanyId = (int?)null }).ToListAsync();
            var customers = await db.Customers.Select(c => new { c.Id, Name = c.UserName, LoginId = c.LoginId, Type = "customer", CompanyId = (int?)c.CompanyId }).ToListAsync();
            return admins.Cast<object>().Concat(customers.Cast<object>()).ToList();
        })).RequireAuthorization();

        // 9. 어떤 그룹에도 소속되지 않은 사용자 목록 조회
        routes.MapGet("/api/common/unassigned-users", (AppDbContext db) => ApiResponseBuilder.CreateAsync(async () => {
            var assignedUserIds = await db.UserRoles.Select(ur => new { ur.UserType, ur.UserId }).ToListAsync();
            var assignedAdminIds = assignedUserIds.Where(x => x.UserType == "admin").Select(x => x.UserId).ToHashSet();
            var assignedCustomerIds = assignedUserIds.Where(x => x.UserType == "customer").Select(x => x.UserId).ToHashSet();

            var unassignedAdmins = await db.Admins
                .Where(a => !assignedAdminIds.Contains(a.Id))
                .Select(a => new { a.Id, Name = a.UserName, LoginId = a.LoginId, Type = "admin", CompanyId = (int?)null })
                .ToListAsync();
            
            var unassignedCustomers = await db.Customers
                .Where(c => !assignedCustomerIds.Contains(c.Id))
                .Select(c => new { c.Id, Name = c.UserName, LoginId = c.LoginId, Type = "customer", CompanyId = (int?)c.CompanyId })
                .ToListAsync();

            return unassignedAdmins.Cast<object>().Concat(unassignedCustomers.Cast<object>()).ToList();
        })).RequireAuthorization();

        // 10. 역할별 메뉴 권한 조회
        group.MapGet("/{roleId}/permissions", (AppDbContext db, int roleId) => ApiResponseBuilder.CreateAsync(async () => {
            return await db.RoleMenuPermissions.Where(p => p.RoleId == roleId).ToListAsync();
        })).RequireAuthorization();

        // 11. 역할별 메뉴 권한 저장 (단일)
        group.MapPost("/permissions", (AppDbContext db, RoleMenuPermission input) => ApiResponseBuilder.CreateAsync(async () => {
            var permission = await db.RoleMenuPermissions.FirstOrDefaultAsync(p => p.RoleId == input.RoleId && p.MenuId == input.MenuId);
            
            if (permission == null) {
                permission = new RoleMenuPermission { RoleId = input.RoleId, MenuId = input.MenuId };
                db.RoleMenuPermissions.Add(permission);
            }

            permission.CanCreate = input.CanCreate;
            permission.CanRead = input.CanRead;
            permission.CanUpdate = input.CanUpdate;
            permission.CanDelete = input.CanDelete;
            permission.Ext1 = input.Ext1; permission.Ext2 = input.Ext2; permission.Ext3 = input.Ext3; permission.Ext4 = input.Ext4;
            permission.Ext5 = input.Ext5; permission.Ext6 = input.Ext6; permission.Ext7 = input.Ext7; permission.Ext8 = input.Ext8;

            await db.SaveChangesAsync();
            return permission;
        })).RequireAuthorization();

        // 12. 역할별 메뉴 권한 대량 저장 (배치)
        group.MapPost("/permissions/batch", (AppDbContext db, List<RoleMenuPermission> inputs) => ApiResponseBuilder.CreateAsync(async () => {
            if (inputs == null || !inputs.Any()) return null;
            
            var roleId = inputs.First().RoleId;
            var menuIds = inputs.Select(i => i.MenuId).ToList();
            
            // 기존 권한 데이터 한 번에 가져오기
            var existingPerms = await db.RoleMenuPermissions
                .Where(p => p.RoleId == roleId && menuIds.Contains(p.MenuId))
                .ToListAsync();

            foreach (var input in inputs) {
                var perm = existingPerms.FirstOrDefault(p => p.MenuId == input.MenuId);
                if (perm == null) {
                    perm = new RoleMenuPermission { RoleId = roleId, MenuId = input.MenuId };
                    db.RoleMenuPermissions.Add(perm);
                }
                // 모든 상태 필드 업데이트 (누락 방지를 위해 명시적 할당)
                perm.CanCreate = input.CanCreate;
                perm.CanRead = input.CanRead;
                perm.CanUpdate = input.CanUpdate;
                perm.CanDelete = input.CanDelete;
                perm.Ext1 = input.Ext1;
                perm.Ext2 = input.Ext2;
                perm.Ext3 = input.Ext3;
                perm.Ext4 = input.Ext4;
                perm.Ext5 = input.Ext5;
                perm.Ext6 = input.Ext6;
                perm.Ext7 = input.Ext7;
                perm.Ext8 = input.Ext8;
            }

            await db.SaveChangesAsync();
            return new { Success = true, Count = inputs.Count };
        })).RequireAuthorization();
    }
}
