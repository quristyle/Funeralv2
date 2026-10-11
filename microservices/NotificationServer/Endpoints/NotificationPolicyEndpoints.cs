using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using NotificationServer.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>
/// 알림관리 — 어떤 이벤트를 어느 역할이 어느 길로 받나
/// (<c>/api/notification/notification-policies/*</c>).
/// </summary>
/// <remarks>
/// <para>
/// [로그인만으로는 부족하다 — 서버가 또 막는다]
/// </para>
///
/// <para>
/// 게이트웨이의 <c>notification-route</c> 는 익명이 아니지만 <b>로그인한 누구나</b>
/// 통과한다. 이 길은 회사의 알림 규칙을 통째로 바꾸는 자리라, 막지 않으면 일반
/// 사용자가 자기를 모든 알림의 수신자로 걸 수 있다. 판정은
/// <see cref="MenuViewAccess"/> 가 <c>scom.role_menus</c> 에 묻는다 — 역할 이름을
/// 여기 적지 않는 까닭은 그 클래스 머리말에 있다.
/// </para>
///
/// <para>
/// [읽기와 쓰기를 같은 문으로 막는다]
/// </para>
///
/// <para>
/// 보고서 메일과 같은 판단이다(<c>AuthServer/Endpoints/ReportMailEndpoints</c>) —
/// 이 화면은 <b>열람 권한이 곧 관리 권한</b>이고, 권한 항목을 쪼개면 「볼 수는
/// 있는데 저장하면 403」이 되는 자리를 하나 더 만든다.
/// </para>
/// </remarks>
public static class NotificationPolicyEndpoints
{
    /// <summary>이 화면의 열쇠. 화면이 <c>RouteKey</c> 로 선언한 글자와 같아야 한다.</summary>
    private const string RouteKey = "admin.system.notify-policy";

    /// <summary>열쇠가 아직 안 채워진 DB 를 위한 경로 대비책.</summary>
    private static readonly string[] Paths = ["/admin/system/notify-policy"];

    public static void MapNotificationPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notification-policies").WithTags("NotificationPolicies");

        // ── 이벤트 목록(정책 줄까지) ────────────────────────
        group.MapGet("", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] string? keyword,
            [FromQuery] bool? activeOnly,
            [FromServices] INotificationPolicyService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetEventsAsync(keyword, activeOnly ?? false, ct);
            return Results.Ok(ApiResponse<List<NotificationEventDto>>.Ok(rows));
        })
        .WithName("GetNotificationEvents");

        // ── 고를 수 있는 역할 ───────────────────────────────
        //
        // 역할 목록은 AuthServer 에도 있지만 여기서 따로 준다. 화면이 역할마다
        // **걸린 사람 수**를 함께 보여 주는데, 그 셈은 역할표와 계정표를 이어야
        // 나오고 그 둘이 이 서비스의 DB 에 있다. 저쪽을 고쳐 칸을 늘리면
        // 「알림관리 때문에 계정 관리 응답이 무거워지는」 자리가 생긴다.
        group.MapGet("/roles", async (
            UserContext? user,
            HttpContext http,
            [FromServices] INotificationPolicyService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetRolesAsync(ct);
            return Results.Ok(ApiResponse<List<NotificationRoleDto>>.Ok(rows));
        })
        .WithName("GetNotificationPolicyRoles");

        // ── 저장 ────────────────────────────────────────────
        //
        // 이벤트 단위로 통째로 받는다. 줄마다 받으면 중간에 끊겼을 때 반쯤
        // 적용된 정책이 남는다(`SaveNotificationPolicyDto` 머리말).
        group.MapPut("/{eventCode}", async (
            string eventCode,
            UserContext? user,
            HttpContext http,
            [FromBody] SaveNotificationPolicyDto request,
            [FromServices] INotificationPolicyService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            try
            {
                var row = await service.SaveAsync(eventCode, request, user!.UserId, ct);
                return Results.Ok(ApiResponse<NotificationEventDto>.Ok(row, "알림 설정을 저장했습니다."));
            }
            catch (InvalidOperationException ex)
            {
                // 「없는 역할이다」 같은 것은 서버 오류가 아니라 사람에게 할 말이다.
                return Results.BadRequest(ApiResponse<NotificationEventDto>.Fail(ex.Message, "INVALID"));
            }
        })
        .WithName("SaveNotificationPolicy");

        // ── 「지금 이 설정이면 누구에게 가나」 ───────────────
        //
        // 저장한 뒤에 묻는다 — 저장 전 화면의 체크 상태로 셈하려면 요청 몸통에
        // 정책을 실어야 하는데, 그러면 **실제로 저장된 것이 아닌 값**으로
        // 「갑니다」를 보여 주게 된다. 보고서 메일의 「받는 사람」과 같은 선이다.
        group.MapGet("/{eventCode}/recipients", async (
            string eventCode,
            UserContext? user,
            HttpContext http,
            [FromServices] INotificationPolicyService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var row = await service.PreviewAsync(eventCode, ct);

            return row is null
                ? Results.NotFound(ApiResponse<NotificationPolicyPreviewDto>.Fail(
                    "이벤트를 찾을 수 없습니다.", "NOT_FOUND"))
                : Results.Ok(ApiResponse<NotificationPolicyPreviewDto>.Ok(row));
        })
        .WithName("GetNotificationPolicyRecipients");
    }

    /// <summary>볼 수 있는 사람인가. 막아야 하면 그 응답을, 통과면 <c>null</c> 을 준다.</summary>
    private static async Task<IResult?> ForbidAsync(
        UserContext? user, HttpContext http, MenuViewAccess access, CancellationToken ct)
    {
        if (user is null) return Results.Unauthorized();

        // `X-User-Role` 은 첫 역할 하나뿐이라 전체 목록(`X-User-Roles`)으로 함께 본다.
        var roles = http.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(string.IsNullOrWhiteSpace(user.Role) ? [] : new[] { user.Role })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (await access.CanViewAsync(roles, RouteKey, Paths, ct)) return null;

        return Results.Json(
            ApiResponse<object>.Fail(
                "이 화면을 볼 권한이 없습니다. 메뉴 권한에서 역할을 확인하세요.", "403"),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
