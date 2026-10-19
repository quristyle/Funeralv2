using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>메뉴 사용기록 — <b>쌓는 길 하나와 보는 길 넷.</b></summary>
/// 쌓는 길은 로그인한 누구나 밀어 넣고 권한을 묻지 않는다 — 권한 없이 열린 화면이 빠지면 가장 알고 싶은 줄이 빠진다.
/// 보는 길은 /api/auth/** 가 익명이라 서버가 MenuViewAccess 로 막는다.
public static class MenuUsageEndpoints
{
    /// <summary>이 화면의 열쇠. 화면이 <c>RouteKey</c> 로 선언한 글자와 같아야 한다.</summary>
    private const string UsageRouteKey = "admin.status.menu-usage";

    /// <summary>열쇠가 아직 안 채워진 DB 를 위한 경로 대비책.</summary>
    private static readonly string[] UsagePaths = ["/admin/status/menu-usage"];

    /// <summary>기록 목록이 한 번에 돌려주는 최대 줄 수. 잘렸다는 사실을 화면이 말한다.</summary>
    private const int MaxTake = 500;

    /// <summary>타임라인이 한 번에 돌려주는 최대 칸 수.</summary>
    private const int MaxTimelineTake = 1000;

    public static void MapMenuUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/menu-usage").WithTags("MenuUsage");

        // ── 넣기 (포털 셸이 부른다) ─────────────────────────
        // 언제나 200 으로 답한다 — 기록 실패를 알려 줘도 보내는 쪽이 할 일이 없다(PortalErrorEndpoints 와 같은 선).
        group.MapPost("", async (
            UserContext? user,
            [FromBody] MenuUsageRecordDto record,
            [FromServices] IMenuUsageService service,
            ILoggerFactory loggers,
            CancellationToken ct) =>
        {
            if (user is null) return Results.Unauthorized();

            try
            {
                var stored = await service.RecordAsync(user.UserId, record, ct);
                return Results.Ok(ApiResponse<object>.Ok(new { stored }));
            }
            catch (Exception ex)
            {
                // 여기서 던지면 화면을 여는 일이 기록 때문에 실패한다. 본말이 뒤집힌다.
                loggers.CreateLogger(typeof(MenuUsageEndpoints))
                    .LogWarning(ex, "메뉴 사용기록을 적지 못했습니다: {Path}", record.MenuPath);

                return Results.Ok(ApiResponse<object>.Ok(new { stored = false }));
            }
        })
        .WithName("RecordMenuUsage");

        // ── 기록 목록 ───────────────────────────────────────
        //
        // 「누가 어떤 화면을 언제 보았나」. 이 표가 있는 까닭 그 자체다.
        group.MapGet("", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? userId,
            [FromQuery] string? keyword,
            [FromQuery] int? take,
            [FromServices] IMenuUsageService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetLogsAsync(
                DateOf(from), DateOf(to), userId, keyword,
                Math.Clamp(take ?? MaxTake, 1, MaxTake), ct);

            return Results.Ok(ApiResponse<List<MenuUsageDto>>.Ok(rows));
        })
        .WithName("GetMenuUsageLogs");

        // ── 사람별 집계 ─────────────────────────────────────
        group.MapGet("/by-user", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? keyword,
            [FromServices] IMenuUsageService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetByUserAsync(DateOf(from), DateOf(to), keyword, ct);

            return Results.Ok(ApiResponse<List<MenuUsageByUserDto>>.Ok(rows));
        })
        .WithName("GetMenuUsageByUser");

        // ── 화면별 집계 ─────────────────────────────────────
        group.MapGet("/by-menu", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? userId,
            [FromQuery] string? keyword,
            [FromServices] IMenuUsageService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetByMenuAsync(DateOf(from), DateOf(to), userId, keyword, ct);

            return Results.Ok(ApiResponse<List<MenuUsageByMenuDto>>.Ok(rows));
        })
        .WithName("GetMenuUsageByMenu");

        // ── 일자별 타임라인 ─────────────────────────────────
        //
        // 집계가 답하지 못하는 것을 이쪽이 답한다 — 「하루가 어떻게 흘렀나」다.
        // 어느 화면에서 어느 화면으로 갔고 사이가 얼마나 떴나.
        group.MapGet("/timeline", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? userId,
            [FromQuery] string? keyword,
            [FromQuery] int? take,
            [FromServices] IMenuUsageService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var days = await service.GetTimelineAsync(
                DateOf(from), DateOf(to), userId, keyword,
                Math.Clamp(take ?? MaxTimelineTake, 1, MaxTimelineTake), ct);

            return Results.Ok(ApiResponse<List<MenuUsageTimelineDayDto>>.Ok(days));
        })
        .WithName("GetMenuUsageTimeline");
    }

    /// <summary>화면이 고른 한국 달력 날짜. 하루의 경계는 서비스가 잡는다.</summary>
    /// 날짜로 받아 자정 언저리에서 시간대 해석이 갈리지 않게 한다.
    private static DateOnly? DateOf(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);

    /// <summary>볼 수 있는 사람인가. 막아야 하면 그 응답을, 통과면 <c>null</c> 을 준다.</summary>
    private static async Task<IResult?> ForbidAsync(
        UserContext? user, HttpContext http, MenuViewAccess access, CancellationToken ct)
    {
        if (user is null) return Results.Unauthorized();

        // 역할이 여럿이면 X-User-Role 하나만 보면 나머지 역할의 권한이 빠지므로 전체 목록(X-User-Roles)으로 본다.
        var roles = http.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(string.IsNullOrWhiteSpace(user.Role) ? [] : new[] { user.Role })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (await access.CanViewAsync(roles, UsageRouteKey, UsagePaths, ct))
        {
            return null;
        }

        return Results.Json(
            ApiResponse<object>.Fail(
                "이 화면을 볼 권한이 없습니다. 메뉴 권한에서 역할을 확인하세요.", "403"),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
