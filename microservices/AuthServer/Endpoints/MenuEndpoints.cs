using Microsoft.AspNetCore.Mvc;
using AuthServer.Entities;
using AuthServer.Services;
using AuthServer.DTOs;
using JSini.Shared.DTOs;

namespace AuthServer.Endpoints;

public static class MenuEndpoints
{
    public static void MapMenuEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/menu");

        group.MapGet("/all", async (UserContext? user, [FromServices] IMenuService menuService) =>
        {
            if (user is null) return Results.Unauthorized();
            var menus = await menuService.GetAllMenusAsync(user.UserId);
            return Results.Ok(ApiResponse<List<MenuDto>>.Ok(menus));
        })
        .WithName("GetAllMenus")
        .WithOpenApi();
        group.MapPost("/move", async ([FromBody] MoveMenuRequest request, [FromServices] IMenuService menuService) =>
        {
            try
            {
                var success = await menuService.MoveMenuAsync(request.MenuId, request.NewParentId, request.NewOrderNo);
                return Results.Ok(ApiResponse<bool>.Ok(true));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("메뉴 이동 실패", "B400", realMessage: ex.Message));
            }
        })
        .WithName("MoveMenu")
        .WithOpenApi();

        // 로그인한 사용자가 메뉴별로 가진 실제 권한.
        // 화면은 이 값만 보고 버튼(등록·수정·삭제·출력·엑셀 …)을 켜고 끈다.
        group.MapGet("/permissions", async (UserContext? user, [FromServices] IMenuService menuService) =>
        {
            if (user is null) return Results.Unauthorized();
            var permissions = await menuService.GetMenuPermissionsAsync(user.UserId);
            return Results.Ok(ApiResponse<List<MenuPermissionDto>>.Ok(permissions));
        })
        .WithName("GetMenuPermissions")
        .WithOpenApi();
    }
}

public class MoveMenuRequest
{
    public string MenuId { get; set; } = string.Empty;
    public string? NewParentId { get; set; }
    public int NewOrderNo { get; set; }
}
