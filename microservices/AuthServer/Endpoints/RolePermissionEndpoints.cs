using Microsoft.AspNetCore.Mvc;
using AuthServer.Services;
using AuthServer.DTOs;
using JSini.Shared.DTOs;

namespace AuthServer.Endpoints;

public static class RolePermissionEndpoints
{
    public static void MapRolePermissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system/role-permission");

        // 1. 역할별 지정 사용자 목록 조회
        group.MapGet("/roles/{roleId}/users", async (string roleId, [FromServices] IRolePermissionService service) =>
        {
            var result = await service.GetUsersByRoleAsync(roleId);
            return Results.Ok(ApiResponse<List<RoleUserDto>>.Ok(result));
        })
        .WithName("GetUsersByRole")
        .WithOpenApi();

        // 2. 특정 역할에 추가 가능한(아직 해당 역할이 없는) 계정 목록 조회
        group.MapGet("/roles/{roleId}/eligible-users", async (string roleId, [FromServices] IRolePermissionService service) =>
        {
            var result = await service.GetEligibleUsersForRoleAsync(roleId);
            return Results.Ok(ApiResponse<List<RoleUserDto>>.Ok(result));
        })
        .WithName("GetEligibleUsersForRole")
        .WithOpenApi();

        // 3. 역할에 사용자 할당
        group.MapPost("/roles/{roleId}/users/assign", async (string roleId, [FromBody] AssignRoleAccountsDto request, [FromServices] IRolePermissionService service) =>
        {
            try
            {
                await service.AssignUsersToRoleAsync(roleId, request.AccountIds);
                return Results.Ok(ApiResponse<bool>.Ok(true));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "400"));
            }
        })
        .WithName("AssignUsersToRole")
        .WithOpenApi();

        // 4. 역할에서 사용자 지정 해제
        group.MapDelete("/roles/{roleId}/users/{userId}", async (string roleId, string userId, [FromServices] IRolePermissionService service) =>
        {
            await service.RemoveUserFromRoleAsync(roleId, userId);
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("RemoveUserFromRole")
        .WithOpenApi();

        // 5. 역할의 세부 메뉴 권한 조회
        group.MapGet("/roles/{roleId}/menus", async (string roleId, [FromServices] IRolePermissionService service) =>
        {
            var result = await service.GetMenusByRoleAsync(roleId);
            return Results.Ok(ApiResponse<List<RoleMenuDto>>.Ok(result));
        })
        .WithName("GetMenusByRole")
        .WithOpenApi();

        // 6. 역할의 세부 메뉴 권한 일괄 업데이트
        group.MapPost("/roles/{roleId}/menus/save", async (string roleId, [FromBody] List<SaveRoleMenuDto> request, [FromServices] IRolePermissionService service) =>
        {
            try
            {
                await service.SaveRoleMenusAsync(roleId, request);
                return Results.Ok(ApiResponse<bool>.Ok(true));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "400"));
            }
        })
        .WithName("SaveRoleMenus")
        .WithOpenApi();
    }
}
