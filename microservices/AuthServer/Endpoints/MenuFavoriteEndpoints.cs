using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>사용자별 즐겨찾기 메뉴 엔드포인트. 세 경로 모두 갱신된 목록 전체를 돌려줘서 화면이 다시 받으러 가지 않아도 된다. 대상은 경로로 받는다.</summary>
public static class MenuFavoriteEndpoints
{
    /// <summary>즐겨찾기 엔드포인트를 등록한다.</summary>
    public static void MapMenuFavoriteEndpoints(this IEndpointRouteBuilder app)
    {
        // 메뉴 관련이므로 기존 /menu 묶음 아래에 둔다.
        var group = app.MapGroup("/menu/favorites").WithTags("MenuFavorites");

        // 내 즐겨찾기 목록.
        group.MapGet("/", async (UserContext? user, [FromServices] IMenuFavoriteService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await service.GetFavoritesAsync(user.UserId);
            return Results.Ok(ApiResponse<List<MenuFavoriteDto>>.Ok(list));
        })
        .WithName("GetMenuFavorites");

        // 즐겨찾기에 담는다. 이미 있으면 그대로 둔다.
        group.MapPost("/", async (
            UserContext? user,
            [FromBody] MenuFavoriteRequest request,
            [FromServices] IMenuFavoriteService service) =>
        {
            if (user is null) return Results.Unauthorized();

            try
            {
                var list = await service.AddFavoriteAsync(user.UserId, request.Path);
                return Results.Ok(ApiResponse<List<MenuFavoriteDto>>.Ok(list, "즐겨찾기에 추가했습니다."));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(
                    ApiResponse<List<MenuFavoriteDto>>.Fail("즐겨찾기에 추가하지 못했습니다.", "B404", realMessage: ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(
                    ApiResponse<List<MenuFavoriteDto>>.Fail(ex.Message, "B400", realMessage: ex.Message));
            }
        })
        .WithName("AddMenuFavorite");

        // 경로를 본문이 아니라 쿼리로 받는다 — DELETE 에 본문을 싣는 것은 프록시·클라이언트마다 취급이 달라 게이트웨이를 거치면 안전하지 않다.
        group.MapDelete("/", async (
            UserContext? user,
            [FromQuery] string path,
            [FromServices] IMenuFavoriteService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await service.RemoveFavoriteAsync(user.UserId, path);
            return Results.Ok(ApiResponse<List<MenuFavoriteDto>>.Ok(list, "즐겨찾기에서 제거했습니다."));
        })
        .WithName("RemoveMenuFavorite");

        // 즐겨찾기 순서를 경로 목록의 순서대로 다시 매긴다.
        // 고정탭 관리 화면이 드래그 정렬 결과를 통째로 보낸다.
        group.MapPut("/order", async (
            UserContext? user,
            [FromBody] MenuFavoriteOrderRequest request,
            [FromServices] IMenuFavoriteService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await service.ReorderFavoritesAsync(user.UserId, request.Paths ?? new List<string>());
            return Results.Ok(ApiResponse<List<MenuFavoriteDto>>.Ok(list, "순서를 저장했습니다."));
        })
        .WithName("ReorderMenuFavorites");
    }
}

/// <summary>순서 변경 요청 — 원하는 순서대로의 메뉴 경로 목록.</summary>
public class MenuFavoriteOrderRequest
{
    /// <summary>원하는 순서대로의 메뉴 경로들</summary>
    public List<string>? Paths { get; set; }
}
