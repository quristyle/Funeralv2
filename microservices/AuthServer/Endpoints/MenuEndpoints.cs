using Microsoft.AspNetCore.Mvc;
using AuthServer.Entities;
using AuthServer.Services;
using AuthServer.DTOs;

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
            return Results.Ok(ApiResponse<List<MenuDto>>.Success(menus));
        })
        .WithName("GetAllMenus")
        .WithOpenApi();
    }
}
