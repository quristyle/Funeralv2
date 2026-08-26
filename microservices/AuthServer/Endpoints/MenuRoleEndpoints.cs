using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;

namespace AuthServer.Endpoints;

/// <summary>
/// 메뉴 기준 권한 현황 (<c>/auth/menu-role</c> 화면).
/// </summary>
/// <remarks>
/// <c>/system/role-map</c> 은 역할에서 출발한다 — "이 역할은 어떤 메뉴를 쓰나".
/// 이쪽은 반대로 <b>"이 메뉴는 누가 쓸 수 있나"</b> 를 본다.
///
/// <para>
/// <b>읽기만 있다.</b> 저장은 이미 있는 경로를 그대로 쓴다 —
/// 같은 일을 하는 저장 경로를 두 개 만들면 한쪽에만 규칙이 붙는다.
/// </para>
/// <list type="bullet">
///   <item>역할↔메뉴 권한 → <c>POST /system/role-permission/roles/{roleId}/menus/save</c></item>
///   <item>역할↔회사·부서·사람 → <c>POST /system/role-scope/assign</c> · <c>/remove</c></item>
/// </list>
/// </remarks>
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
        .WithName("GetMenuRole")
        .WithOpenApi();
    }
}
