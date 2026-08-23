using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 사용자별 즐겨찾기 메뉴 엔드포인트.
///
/// <para>
/// 세 경로 모두 <b>갱신된 목록 전체</b>를 돌려준다. 화면이 추가·해제 후 목록을 다시
/// 받으러 한 번 더 부르지 않아도 되고, 두 창을 열어 둔 경우에도 방금 누른 창은
/// 곧바로 맞는 상태가 된다.
/// </para>
///
/// <para>
/// 대상은 <b>경로</b>로 받는다. 탭이 아는 것이 경로뿐이기 때문이다
/// (메뉴 조회 API 응답에는 메뉴 식별자가 없다). 저장은 식별자로 한다 —
/// <see cref="Entities.MenuFavorite"/> 주석 참고.
/// </para>
/// </summary>
public static class MenuFavoriteEndpoints
{
    /// <summary>즐겨찾기 엔드포인트를 등록한다.</summary>
    public static void MapMenuFavoriteEndpoints(this IEndpointRouteBuilder app)
    {
        // 메뉴 관련이므로 기존 /menu 묶음 아래에 둔다.
        var group = app.MapGroup("/menu/favorites").WithTags("MenuFavorites");

        /// <summary>내 즐겨찾기 목록.</summary>
        group.MapGet("/", async (UserContext? user, [FromServices] IMenuFavoriteService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await service.GetFavoritesAsync(user.UserId);
            return Results.Ok(ApiResponse<List<MenuFavoriteDto>>.Ok(list));
        })
        .WithName("GetMenuFavorites")
        .WithOpenApi();

        /// <summary>즐겨찾기에 담는다. 이미 있으면 그대로 둔다.</summary>
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
        .WithName("AddMenuFavorite")
        .WithOpenApi();

        /// <summary>
        /// 즐겨찾기에서 뺀다. 없으면 아무 일도 하지 않는다.
        ///
        /// 경로를 본문이 아니라 쿼리로 받는다. DELETE 에 본문을 싣는 것은 프록시·클라이언트마다
        /// 취급이 달라 게이트웨이를 거치는 이 구조에서는 쿼리가 안전하다.
        /// </summary>
        group.MapDelete("/", async (
            UserContext? user,
            [FromQuery] string path,
            [FromServices] IMenuFavoriteService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var list = await service.RemoveFavoriteAsync(user.UserId, path);
            return Results.Ok(ApiResponse<List<MenuFavoriteDto>>.Ok(list, "즐겨찾기에서 제거했습니다."));
        })
        .WithName("RemoveMenuFavorite")
        .WithOpenApi();
    }
}
