using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using NotificationServer.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>알림관리(/api/notification/notification-policies/*). 게이트웨이는 로그인만 보므로 여기서 메뉴 권한을 다시 본다. 읽기와 쓰기 모두 같은 문으로 막는다.</summary>
public static class NotificationPolicyEndpoints
{
    /// <summary>이 화면의 열쇠. 화면이 RouteKey 로 선언한 글자와 같아야 한다.</summary>
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

        // 역할 목록은 AuthServer 와 따로 준다(역할마다 걸린 사람 수를 세려면 이 서비스의 DB 를 이어야 한다).
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

        // 이벤트 단위로 통째로 받는다(줄마다 받으면 중간에 끊겼을 때 반쯤 적용된 정책이 남는다).
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
                // 없는 역할은 서버 오류가 아니라 사람에게 할 말이다.
                return Results.BadRequest(ApiResponse<NotificationEventDto>.Fail(ex.Message, "INVALID"));
            }
        })
        .WithName("SaveNotificationPolicy");

        // 저장한 뒤에 묻는다(저장 전 체크 상태로 셈하면 저장되지 않은 값으로 「갑니다」를 보여 준다).
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

        // 미리받아보기. 이벤트가 일어나지 않아도 그 알림 한 통을 본인 또는 저장된 정책의 대상자 모두에게 보낸다.
        group.MapPost("/{eventCode}/preview-send", async (
            string eventCode,
            UserContext? user,
            HttpContext http,
            [FromBody] NotificationPolicyPreviewSendDto request,
            [FromServices] INotificationPolicyPreviewSender sender,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            try
            {
                var row = await sender.SendAsync(eventCode, request.ToTargets, user!.UserId, user.Email, ct);

                return row is null
                    ? Results.NotFound(ApiResponse<NotificationPolicyPreviewSendResultDto>.Fail(
                        "이벤트를 찾을 수 없습니다.", "NOT_FOUND"))
                    : Results.Ok(ApiResponse<NotificationPolicyPreviewSendResultDto>.Ok(row, row.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<NotificationPolicyPreviewSendResultDto>.Fail(ex.Message, "INVALID"));
            }
        })
        .WithName("SendNotificationPolicyPreview");
    }

    /// <summary>볼 수 있는 사람인가. 막아야 하면 그 응답을, 통과면 <c>null</c> 을 준다.</summary>
    private static async Task<IResult?> ForbidAsync(
        UserContext? user, HttpContext http, MenuViewAccess access, CancellationToken ct)
    {
        if (user is null) return Results.Unauthorized();

        // X-User-Role 은 첫 역할 하나뿐이라 전체 목록(X-User-Roles)으로 함께 본다.
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
