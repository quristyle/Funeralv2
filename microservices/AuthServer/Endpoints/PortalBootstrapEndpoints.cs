using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>포털 레이아웃이 뜰 때 필요한 것을 한 번에 내려보낸다. 권한표·메뉴·즐겨찾기·내정보를 순차로 부르던 왕복을 묶는다.</summary>
public static class PortalBootstrapEndpoints
{
    public static void MapPortalBootstrapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/portal/bootstrap", async (
            UserContext? user,
            [FromQuery] string? locale,
            [FromServices] IMenuService menuService,
            [FromServices] IMenuFavoriteService favoriteService,
            [FromServices] IUserService userService,
            [FromServices] IAccountPreferenceService preferenceService,
            [FromServices] AuthServer.Data.AppDbContext db) =>
        {
            if (user is null)
            {
                return Results.Unauthorized();
            }

            // DbContext 를 나눠 쓰는 서비스끼리는 병렬로 돌리지 않는다 — EF Core 는 동시 사용을 허용하지 않아 "A second operation..." 으로 죽는다. 순서대로 기다린다.
            var menus = await menuService.GetAllMenusAsync(user.UserId, locale);
            var permissions = await menuService.GetMenuPermissionsAsync(user.UserId);
            var favorites = await favoriteService.GetFavoritesAsync(user.UserId);
            var info = await userService.GetUserInfoAsync(user.UserId);

            // 다섯째 — 휴대폰 아래 띠·떠다니는 단추·토스트 위치는 계정에 있어 레이아웃이 뜰 때 반드시 있어야 하므로 이 묶음에 넣는다.
            var preferences = await preferenceService.GetAsync(user.UserId);

            // 레이아웃 상단 도구 아이콘 목록 및 기본값 설정
            var headerIcons = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                System.Linq.Queryable.Select(
                    System.Linq.Queryable.OrderBy(
                        System.Linq.Queryable.Where(db.LayoutHeaderIcons, x => !x.IsDeleted),
                        x => x.OrderNo),
                    x => new LayoutHeaderIconDto(
                        x.Id,
                        x.IconKey,
                        x.Title,
                        x.Icon,
                        x.RouteKey,
                        x.Path,
                        x.DefaultEnabled,
                        x.OrderNo,
                        x.Description)));

            return Results.Ok(ApiResponse<PortalBootstrapDto>.Ok(new PortalBootstrapDto
            {
                Menus = menus,
                Permissions = permissions,
                Favorites = favorites,
                User = info,
                Preferences = preferences,
                HeaderIcons = headerIcons,
            }));
        })
        .WithName("GetPortalBootstrap");

        app.MapGet("/layout/header-icons", async (
            [FromServices] AuthServer.Data.AppDbContext db) =>
        {
            var headerIcons = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                System.Linq.Queryable.Select(
                    System.Linq.Queryable.OrderBy(
                        System.Linq.Queryable.Where(db.LayoutHeaderIcons, x => !x.IsDeleted),
                        x => x.OrderNo),
                    x => new LayoutHeaderIconDto(
                        x.Id,
                        x.IconKey,
                        x.Title,
                        x.Icon,
                        x.RouteKey,
                        x.Path,
                        x.DefaultEnabled,
                        x.OrderNo,
                        x.Description)));

            return Results.Ok(ApiResponse<List<LayoutHeaderIconDto>>.Ok(headerIcons));
        })
        .WithName("GetLayoutHeaderIcons");
    }
}
