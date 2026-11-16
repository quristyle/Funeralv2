using JSini.Shared.DTOs;

namespace LifeEnvServer.Endpoints;

/// <summary>기준 정보(관측 지역·기준·대응·기록)를 바꾸는 문. 생활과환경 메뉴는 ALL_USERS 도 열 수 있어 화면 권한만으로는 쓰기를 막지 못한다.</summary>
public static class AdminWriteFilter
{
    private static readonly string[] AdminRoles = ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR", "SERVER_ADMIN"];

    public static TBuilder RequireLifeEnvAdmin<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var headers = context.HttpContext.Request.Headers;
            var userId = headers["X-User-Id"].ToString();

            if (string.IsNullOrEmpty(userId))
            {
                return Results.Json(ApiResponse<object>.Fail("로그인이 필요합니다.", "401"),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            // 게이트웨이는 로그인한 요청에 X-User-Role 을 늘 붙인다. 없으면 서비스 간 직접 호출이다.
            var isService = userId.StartsWith("system:", StringComparison.Ordinal)
                            || !headers.ContainsKey("X-User-Role");

            var isAdmin = headers["X-User-Roles"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Append(headers["X-User-Role"].ToString())
                .Any(r => AdminRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

            if (!isService && !isAdmin)
            {
                return Results.Json(ApiResponse<object>.Fail("기준 정보를 바꿀 권한이 없습니다.", "403"),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        });
}
