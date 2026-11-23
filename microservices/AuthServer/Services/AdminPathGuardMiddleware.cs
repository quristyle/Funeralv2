using JSini.Shared.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 관리용 경로의 문지기. 게이트웨이의 auth-route 는 <c>/api/auth/**</c> 를 통째로 익명으로 넘기고,
/// <c>/system/*</c> · <c>/server-ping</c> 같은 묶음의 처리기는 대부분 <c>UserContext</c> 를 받지 않아
/// 스스로는 아무것도 막지 않는다. 그래서 경로 단위로 여기서 한 번에 막는다.
/// <list type="bullet">
/// <item>신원(X-User-Id) 없는 호출은 401 — 게이트웨이가 바깥의 X-User-* 를 지우고 검증한 토큰에서만 싣는다.</item>
/// <item>계정·역할·권한·조직·메뉴·가입 승인을 <b>바꾸는</b> 호출은 관리자 역할만 — 아니면 누구나 자기에게 시스템관리자를 줄 수 있다.</item>
/// <item>서버 점검(server-ping)은 임의 주소·포트를 두드리는 길이라 읽기도 관리자·서버관리자만.</item>
/// </list>
/// 신원 없이 받는 예외는 서버 대 서버 읽기 둘(헬프데스크의 회사 목록, 장례식장의 공통코드)뿐이다.
/// </summary>
public sealed class AdminPathGuardMiddleware(RequestDelegate next, ILogger<AdminPathGuardMiddleware> logger)
{
    private static readonly string[] AdminRoles = ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR"];
    private static readonly string[] ServerPingRoles = ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR", "SERVER_ADMIN"];

    /// <summary>신원이 있어야 하는 묶음.</summary>
    private static readonly string[] LoginRequired =
    [
        "/system", "/server-ping", "/menu/move", "/menu/keep-alive", "/notices",
    ];

    /// <summary>바꾸는 호출(GET 외)은 관리자만.</summary>
    private static readonly string[] AdminWrite =
    [
        "/system/account", "/system/role", "/system/role-permission", "/system/role-scope",
        "/system/signup", "/system/companies", "/system/company-usage-managers", "/system/dept", "/system/menu", "/system/i18n",
        "/system/biz-select", "/system/common-code", "/menu/move", "/notices",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var method = context.Request.Method;
        var isRead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

        if (!Matches(path, LoginRequired) || IsPublic(path, isRead))
        {
            await next(context);
            return;
        }

        var userId = context.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Deny(context, StatusCodes.Status401Unauthorized, "로그인이 필요합니다.", "401");
            return;
        }

        string[]? required = null;
        if (path.StartsWithSegments("/server-ping", StringComparison.OrdinalIgnoreCase))
        {
            required = ServerPingRoles;
        }
        else if (!isRead && Matches(path, AdminWrite))
        {
            required = AdminRoles;
        }

        if (required is not null)
        {
            var roles = Roles(context);
            if (!roles.Any(r => required.Contains(r, StringComparer.OrdinalIgnoreCase)))
            {
                logger.LogWarning("[AdminPathGuard] 거부: {Method} {Path} user={UserId} roles={Roles}",
                    method, path, userId, string.Join(',', roles));
                await Deny(context, StatusCodes.Status403Forbidden, "권한이 없습니다.", "403");
                return;
            }
        }

        await next(context);
    }

    /// <summary>신원 없이 받는 길. 로그인 전 공지 팝업과 서버 대 서버 읽기.</summary>
    private static bool IsPublic(PathString path, bool isRead)
    {
        if (!isRead) return false;

        var value = (path.Value ?? string.Empty).TrimEnd('/');
        return value.Equals("/notices/popup/public", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/system/companies", StringComparison.OrdinalIgnoreCase)
            || (path.StartsWithSegments("/system/common-code", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWithSegments("/system/common-code/groups", StringComparison.OrdinalIgnoreCase)
                && value.Count(c => c == '/') == 3);
    }

    private static bool Matches(PathString path, string[] prefixes) =>
        prefixes.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    private static string[] Roles(HttpContext context)
    {
        var roles = context.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (roles.Length > 0) return roles;

        var single = context.Request.Headers["X-User-Role"].ToString();
        return string.IsNullOrWhiteSpace(single) ? [] : [single];
    }

    private static Task Deny(HttpContext context, int status, string message, string code)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message, code));
    }
}
