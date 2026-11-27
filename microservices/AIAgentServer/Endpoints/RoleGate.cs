using JSini.Shared.DTOs;

namespace AIAgentServer;

/// <summary>
/// 묶음 단위 역할 문지기. 게이트웨이는 로그인만 보고, 메뉴 권한은 화면에서만 걸러지므로
/// 주소를 아는 사람은 메뉴 없이도 이 서비스를 바로 부를 수 있다 — 남의 메일함 · 전 직원 업무 자료를 여는
/// 묶음은 여기서 역할을 다시 본다. 역할은 게이트웨이가 검증한 토큰에서만 싣는 <c>X-User-Roles</c> 다.
/// </summary>
public static class RoleGate
{
    public static readonly string[] Admins = ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR"];

    public static TBuilder RequireRoles<TBuilder>(this TBuilder builder, Func<IServiceProvider, string[]> allowed)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            if (string.IsNullOrWhiteSpace(http.Request.Headers["X-User-Id"].ToString()))
            {
                return Results.Json(ApiResponse<object>.Fail("로그인이 필요합니다.", "C401"), statusCode: 401);
            }

            var roles = Roles(http);
            var need = allowed(http.RequestServices);
            if (!roles.Any(r => need.Contains(r, StringComparer.OrdinalIgnoreCase)))
            {
                return Results.Json(ApiResponse<object>.Fail("권한이 없습니다.", "C403"), statusCode: 403);
            }

            return await next(ctx);
        });

    public static TBuilder RequireRoles<TBuilder>(this TBuilder builder, params string[] allowed)
        where TBuilder : IEndpointConventionBuilder => builder.RequireRoles(_ => allowed);

    public static bool HasAny(HttpContext http, IEnumerable<string> allowed) =>
        Roles(http).Any(r => allowed.Contains(r, StringComparer.OrdinalIgnoreCase));

    private static string[] Roles(HttpContext http)
    {
        var header = http.Request.Headers["X-User-Roles"].ToString();
        if (!string.IsNullOrWhiteSpace(header))
        {
            return header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var single = http.Request.Headers["X-User-Role"].ToString();
        return string.IsNullOrWhiteSpace(single) ? [] : [single];
    }
}
