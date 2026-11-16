using JSini.Shared.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>누가 불렀나 — 서비스 간 직접 호출 · 관리자 · 화면 메뉴 권한. 게이트웨이는 로그인만 보므로 발송·조회 문을 여기서 다시 막는다.</summary>
public static class CallerAccess
{
    /// <summary>화면 메뉴 권한과 상관없이 모든 문을 여는 역할.</summary>
    public static readonly string[] AdminRoles = ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR"];

    /// <summary>
    /// 게이트웨이를 거치지 않은 서비스 간 호출인가. 게이트웨이는 로그인한 요청에 X-User-Role 을 늘 붙이고(역할이 없으면 User)
    /// 바깥이 보낸 같은 이름 헤더는 지우므로, 그것이 없으면 루프백 호출이다(AI_TASK · SITE_INQUIRY · 생일 축하처럼 사람 아이디를 싣는 것도 있다).
    /// </summary>
    public static bool IsService(UserContext user, HttpContext http) =>
        user.UserId.StartsWith("system:", StringComparison.Ordinal)
        || !http.Request.Headers.ContainsKey("X-User-Role");

    /// <summary>X-User-Role 은 첫 역할 하나뿐이라 전체 목록(X-User-Roles)으로 함께 본다.</summary>
    public static string[] Roles(UserContext user, HttpContext http) =>
        http.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(string.IsNullOrWhiteSpace(user.Role) ? [] : new[] { user.Role })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static bool IsAdmin(UserContext user, HttpContext http) =>
        Roles(user, http).Any(r => AdminRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

    /// <summary>서비스 호출이거나 관리자다.</summary>
    public static bool IsServiceOrAdmin(UserContext user, HttpContext http) =>
        IsService(user, http) || IsAdmin(user, http);

    /// <summary>서비스 호출 · 관리자 · 그 화면을 볼 수 있는 역할 중 하나다. 화면은 (열쇠, 경로들) 짝으로 여럿 줄 수 있다.</summary>
    public static async Task<bool> CanUseScreenAsync(
        UserContext user, HttpContext http, MenuViewAccess access, CancellationToken ct,
        params (string RouteKey, string[] Paths)[] screens)
    {
        if (IsServiceOrAdmin(user, http)) return true;

        var roles = Roles(user, http);
        foreach (var (routeKey, paths) in screens)
        {
            if (await access.CanViewAsync(roles, routeKey, paths, ct)) return true;
        }

        return false;
    }

    public static IResult Forbidden(string message = "이 기능을 쓸 권한이 없습니다.") =>
        Results.Json(ApiResponse<object>.Fail(message, "403"), statusCode: StatusCodes.Status403Forbidden);

    // 화면 열쇠. 화면 [RouteKey] 와 글자가 같아야 하고, 경로는 열쇠가 안 채워진 DB 대비책이다.
    public static readonly (string, string[]) PushSendScreen = ("admin.push.send", ["/admin/push/send"]);
    public static readonly (string, string[]) MailSendScreen = ("admin.mail.send", ["/admin/mail/send"]);
    public static readonly (string, string[]) PushLogsScreen = ("admin.push.logs", ["/system/push/logs", "/admin/push/logs"]);
    public static readonly (string, string[]) PushDashboardScreen = ("admin.push.dashboard", ["/system/push/dashboard", "/admin/push/dashboard"]);
    public static readonly (string, string[]) AccountScreen = ("admin.system.account", ["/admin/system/account"]);
    public static readonly (string, string[]) LocationMapScreen = ("admin.location.map", ["/admin/location/map"]);
}
