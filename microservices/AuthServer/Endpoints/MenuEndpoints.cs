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

        // locale 은 선택이다. 안 주면 ko 로 옮긴다.
        // 제목 번역은 서버가 붙여 meta.titleText 로 내려보낸다 — 화면이 제목마다 번역 함수를 부르면 키가 아닌 제목마다 경고가 쏟아진다.
        group.MapGet("/all", async (
            UserContext? user,
            [FromQuery] string? locale,
            [FromServices] IMenuService menuService) =>
        {
            if (user is null) return Results.Unauthorized();
            var menus = await menuService.GetAllMenusAsync(user.UserId, locale);
            return Results.Ok(ApiResponse<List<MenuDto>>.Ok(menus));
        })
        .WithName("GetAllMenus");
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
        .WithName("MoveMenu");

        // 로그인한 사용자가 메뉴별로 가진 실제 권한.
        // 화면은 이 값만 보고 버튼(등록·수정·삭제·출력·엑셀 …)을 켜고 끈다.
        group.MapGet("/permissions", async (UserContext? user, [FromServices] IMenuService menuService) =>
        {
            if (user is null) return Results.Unauthorized();
            var permissions = await menuService.GetMenuPermissionsAsync(user.UserId);
            return Results.Ok(ApiResponse<List<MenuPermissionDto>>.Ok(permissions));
        })
        .WithName("GetMenuPermissions");

        // 탭 컨텍스트 메뉴 또는 관리 화면에서 화면 유지(keepAlive) 여부를 설정한다.
        group.MapPut("/keep-alive", async (
            [FromBody] SetMenuKeepAliveRequest request,
            [FromServices] IMenuService menuService) =>
        {
            var success = await menuService.SetKeepAliveAsync(request.Path, request.RouteKey, request.KeepAlive);
            return success
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("메뉴를 찾을 수 없습니다.", "404"));
        })
        .WithName("SetMenuKeepAlive");
    }
}

public class SetMenuKeepAliveRequest
{
    public string? Path { get; set; }
    public string? RouteKey { get; set; }
    public bool KeepAlive { get; set; }
}

public class MoveMenuRequest
{
    public string MenuId { get; set; } = string.Empty;
    public string? NewParentId { get; set; }
    public int NewOrderNo { get; set; }
}
