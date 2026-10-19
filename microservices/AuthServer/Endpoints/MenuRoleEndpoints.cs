using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;

namespace AuthServer.Endpoints;

/// <summary>메뉴 기준 권한 현황 (<c>/auth/menu-role</c> 화면).</summary>
/// <summary>메뉴 기준 권한 현황(/auth/menu-role 화면). 역할에서 출발하는 role-map 과 반대로 「이 메뉴는 누가 쓰나」를 본다.</summary>
/// 읽기만 둔다. 저장은 역할-메뉴·역할-범위 기존 경로를 그대로 쓴다 — 같은 저장 경로를 두 개 만들면 한쪽에만 규칙이 붙는다.
public static class MenuRoleEndpoints
{
    public static void MapMenuRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system/menu-role").WithTags("MenuRole");

        group.MapGet("/{menuId}", async (string menuId, UserContext? user,
            IMenuRoleService service) =>
        {
            if (user is null)
            {
                return Results.Json(
                    ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"),
                    statusCode: 401);
            }

            var result = await service.GetByMenuIdAsync(menuId);
            return result is null
                ? Results.NotFound(ApiResponse<MenuRoleDto>.Fail("메뉴를 찾을 수 없습니다.", "NOT_FOUND"))
                : Results.Ok(ApiResponse<MenuRoleDto>.Ok(result));
        })
        .WithName("GetMenuRole");
    }
}
