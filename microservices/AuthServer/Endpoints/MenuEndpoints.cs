using Microsoft.AspNetCore.Mvc;
using AuthServer.Entities;
using AuthServer.Services;
using AuthServer.DTOs;
using Funeralv2.Shared.DTOs;

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
    }
}

public class MoveMenuRequest
{
    public string MenuId { get; set; } = string.Empty;
    public string? NewParentId { get; set; }
    public int NewOrderNo { get; set; }
}
