using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 메뉴 사용기록 — <b>쌓는 길 하나와 보는 길 넷.</b>
/// </summary>
/// <remarks>
/// <para>
/// 포털 셸이 화면을 열 때마다 <c>POST</c> 로 한 줄 밀어 넣고(로그인한 사람
/// 누구나), 포털관리의 「메뉴 사용기록」 화면이 <c>GET</c> 으로 꺼내 본다
/// (그 메뉴를 볼 수 있는 역할만).
/// </para>
///
/// <para>
/// [넣는 길에 권한을 묻지 않는다]
/// </para>
///
/// <para>
/// 자기가 연 화면을 적는 일이다. 메뉴 권한을 또 따지면 <b>권한 없이 열린
/// 화면이 기록에서 빠지는데</b>, 그것이야말로 가장 알고 싶은 줄이다.
/// 적히는 이름은 몸체가 아니라 게이트웨이가 붙여 준 <c>X-User-Id</c> 라
/// 남의 이름으로 쌓을 수는 없다.
/// </para>
///
/// <para>
/// [보는 길은 서버가 막는다]
/// </para>
///
/// <para>
/// 게이트웨이의 <c>auth-route</c> 는 <c>/api/auth/**</c> 를 통째로 익명으로
/// 열어 둔다(로그인이 그 길로 간다). 그래서 화면의 메뉴 권한만 믿으면 주소를
/// 아는 누구나 <b>동료가 언제 무엇을 보았는지</b> 받아 갈 수 있다.
/// 막는 판정은 <see cref="MenuViewAccess"/> 가 <c>scom.role_menus</c> 에 묻는다 —
/// 역할 이름을 여기 적지 않는 까닭은 그 클래스 머리말에 있다.
/// </para>
/// </remarks>
public static class MenuUsageEndpoints
{
    /// <summary>이 화면의 열쇠. 화면이 <c>RouteKey</c> 로 선언한 글자와 같아야 한다.</summary>
    private const string UsageRouteKey = "admin.status.menu-usage";

    /// <summary>열쇠가 아직 안 채워진 DB 를 위한 경로 대비책.</summary>
    private static readonly string[] UsagePaths = ["/admin/status/menu-usage"];

    /// <summary>
    /// 기록 목록이 한 번에 돌려주는 최대 줄 수.
    /// </summary>
    /// <remarks>
    /// <b>잘렸다는 사실을 화면이 말한다</b> — 말 없이 자르면 「이 기간에 500건
    /// 뿐」으로 읽힌다. 더 보려면 기간을 좁히거나 사람을 고른다.
    /// </remarks>
    private const int MaxTake = 500;

    /// <summary>타임라인이 한 번에 돌려주는 최대 칸 수. 머리말은 위와 같다.</summary>
    private const int MaxTimelineTake = 1000;

    public static void MapMenuUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/menu-usage").WithTags("MenuUsage");

        // ── 넣기 (포털 셸이 부른다) ─────────────────────────
        //
        // **언제나 200 으로 답한다.** 보내는 쪽은 기록을 남기려다 실패해도 할
        // 수 있는 일이 없고, 실패를 알려 주면 거기서 또 예외가 날 자리만 는다
        // (`PortalErrorEndpoints` 와 같은 선이다).
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

    /// <summary>
    /// 화면이 고른 <b>한국 달력 날짜</b>. 하루의 경계는 서비스가 잡는다.
    /// </summary>
    /// <remarks>
    /// 시각이 아니라 날짜로 받는 까닭은 자정 언저리 때문이다 — 시각으로 받으면
    /// 보내는 쪽이 어느 시간대의 자정을 적을지 정해야 하고, 거기서 갈린다.
    /// </remarks>
    private static DateOnly? DateOf(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);

    /// <summary>
    /// 볼 수 있는 사람인가. 막아야 하면 그 응답을, 통과면 <c>null</c> 을 준다.
    /// </summary>
    private static async Task<IResult?> ForbidAsync(
        UserContext? user, HttpContext http, MenuViewAccess access, CancellationToken ct)
    {
        if (user is null) return Results.Unauthorized();

        // `X-User-Role` 은 첫 역할 하나뿐이라 전체 목록(`X-User-Roles`)으로 함께 본다.
        // 역할이 여럿인 계정에서 그 하나만 보면 나머지 역할로 받은 권한이 사라진다.
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
