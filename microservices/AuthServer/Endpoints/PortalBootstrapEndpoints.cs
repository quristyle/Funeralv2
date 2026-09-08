using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 포털 레이아웃이 뜰 때 필요한 것을 <b>한 번에</b> 내려보낸다.
///
/// [왜 묶었나]
///
/// 셸의 <c>MainLayout</c> 은 넷을 <b>순차로</b> 불렀다 — 권한표 · 메뉴 ·
/// 즐겨찾기 · 내정보. 넷 다 같은 사용자의 것이고 서로 기다릴 이유가 없는데
/// 왕복이 네 번이었다. 게다가 포털은 <b>업무 모듈을 넘나들 때마다</b> 이
/// 넷을 다시 읽는다(Piral 모듈 컨테이너가 갈리면서 레이아웃이 새로 생긴다).
/// 그래서 화면 전환 한 번의 값이 왕복 네 번이었다.
///
/// [순서를 서버가 지킨다]
///
/// 프론트가 넷을 병렬로 쏘는 방법도 있었지만 그러면 <b>순서 제약이 깨진다</b> —
/// 권한표가 메뉴보다 먼저 와야 걸러지지 않은 사이드바가 한 번 번쩍이지 않는다.
/// 한 응답으로 묶으면 그 제약이 사라진다. 넷이 같은 시점의 값이라는 것도
/// 덤으로 보장된다.
///
/// [옛 엔드포인트 넷은 그대로 둔다]
///
/// 지우면 이 배포가 프론트 배포와 순서를 타게 된다. 그리고 개별 새로고침이
/// 여전히 필요하다 — 즐겨찾기는 별을 누를 때마다 자기만 다시 읽는다.
/// </summary>
public static class PortalBootstrapEndpoints
{
    public static void MapPortalBootstrapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/portal/bootstrap", async (
            UserContext? user,
            [FromQuery] string? locale,
            [FromServices] IMenuService menuService,
            [FromServices] IMenuFavoriteService favoriteService,
            [FromServices] IUserService userService) =>
        {
            if (user is null)
            {
                return Results.Unauthorized();
            }

            // 넷을 함께 기다린다. 서로 다른 표를 읽으므로 겹치지 않는다.
            //
            // **DbContext 를 나눠 쓰는 서비스끼리 병렬로 돌리지 않는다.**
            // EF Core 의 DbContext 는 동시 사용을 허용하지 않아서 그렇게 하면
            // "A second operation was started on this context" 로 죽는다.
            // 순서대로 기다리되, 왕복이 한 번이라는 것이 이 엔드포인트의 값이다.
            var menus = await menuService.GetAllMenusAsync(user.UserId, locale);
            var permissions = await menuService.GetMenuPermissionsAsync(user.UserId);
            var favorites = await favoriteService.GetFavoritesAsync(user.UserId);
            var info = await userService.GetUserInfoAsync(user.UserId);

            return Results.Ok(ApiResponse<PortalBootstrapDto>.Ok(new PortalBootstrapDto
            {
                Menus = menus,
                Permissions = permissions,
                Favorites = favorites,
                User = info,
            }));
        })
        .WithName("GetPortalBootstrap");
    }
}
