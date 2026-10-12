using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 차단 접근기록 — <b>쌓는 길 하나와 보는 길 셋.</b>
/// </summary>
/// <remarks>
/// <para>
/// 포털 셸이 권한 밖의 주소를 막을 때마다 <c>POST</c> 로 한 줄 밀어 넣고
/// (로그인한 사람 누구나), 포털관리의 「차단 접근기록」 화면이 <c>GET</c> 으로
/// 꺼내 본다(그 메뉴를 볼 수 있는 역할만).
/// </para>
///
/// <para>
/// [넣는 길에 권한을 묻지 않는다 — 여기서는 특히 그렇다]
/// </para>
///
/// <para>
/// 적히는 일이 <b>권한이 없다는 사실 그 자체</b>다. 여기서 또 메뉴 권한을
/// 따지면 권한이 가장 적은 사람의 줄이 먼저 빠지는데, 그 줄이야말로 이 표가
/// 보고 싶은 것이다. 적히는 이름은 몸체가 아니라 게이트웨이가 붙여 준
/// <c>X-User-Id</c> 라 남의 이름으로 쌓을 수는 없다.
/// </para>
///
/// <para>
/// [보는 길은 서버가 막는다]
/// </para>
///
/// <para>
/// 게이트웨이의 <c>auth-route</c> 는 <c>/api/auth/**</c> 를 통째로 익명으로
/// 열어 둔다(로그인이 그 길로 간다). 그래서 화면의 메뉴 권한만 믿으면 주소를
/// 아는 누구나 <b>어느 화면이 어떻게 뚫려 있는지</b> 받아 갈 수 있다 —
/// 이 표에서는 그것이 곧 공격 지도다. 막는 판정은 <see cref="MenuViewAccess"/>
/// 가 <c>scom.role_menus</c> 에 묻는다.
/// </para>
/// </remarks>
public static class BlockedAccessEndpoints
{
    /// <summary>이 화면의 열쇠. 화면이 <c>RouteKey</c> 로 선언한 글자와 같아야 한다.</summary>
    private const string BlockedRouteKey = "admin.status.blocked-access";

    /// <summary>열쇠가 아직 안 채워진 DB 를 위한 경로 대비책.</summary>
    private static readonly string[] BlockedPaths = ["/admin/status/blocked-access"];

    /// <summary>
    /// 기록 목록이 한 번에 돌려주는 최대 줄 수.
    /// </summary>
    /// <remarks>
    /// <b>잘렸다는 사실을 화면이 말한다</b> — 말 없이 자르면 「이 기간에 500건
    /// 뿐」으로 읽힌다. 더 보려면 기간을 좁히거나 사람을 고른다.
    /// </remarks>
    private const int MaxTake = 500;

    public static void MapBlockedAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/blocked-access").WithTags("BlockedAccess");

        // ── 넣기 (포털 셸이 부른다) ─────────────────────────
        //
        // **언제나 200 으로 답한다.** 보내는 쪽은 기록을 남기려다 실패해도 할
        // 수 있는 일이 없고, 실패를 알려 주면 거기서 또 예외가 날 자리만 는다.
        group.MapPost("", async (
            UserContext? user,
            [FromBody] BlockedAccessRecordDto record,
            [FromServices] IBlockedAccessService service,
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
                // 여기서 던지면 **막는 일이 적는 일 때문에 실패한다.** 본말이 뒤집힌다.
                loggers.CreateLogger(typeof(BlockedAccessEndpoints))
                    .LogWarning(ex, "차단 기록을 적지 못했습니다: {Href}", record.Href);

                return Results.Ok(ApiResponse<object>.Ok(new { stored = false }));
            }
        })
        .WithName("RecordBlockedAccess");

        // ── 화면별 집계 ─────────────────────────────────────
        //
        // **이 표가 있는 까닭 그 자체다.** 「어떤 화면이 권한관리 밖인가」 —
        // 메뉴를 만들거나 [ScreenAccess] 를 적어야 할 화면이 여기 선다.
        group.MapGet("/by-screen", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? userId,
            [FromQuery] string? reason,
            [FromQuery] string? keyword,
            [FromServices] IBlockedAccessService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetByScreenAsync(
                DateOf(from), DateOf(to), userId, reason, keyword, ct);

            return Results.Ok(ApiResponse<List<BlockedAccessByScreenDto>>.Ok(rows));
        })
        .WithName("GetBlockedAccessByScreen");

        // ── 사람별 집계 ─────────────────────────────────────
        group.MapGet("/by-user", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? reason,
            [FromQuery] string? keyword,
            [FromServices] IBlockedAccessService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetByUserAsync(DateOf(from), DateOf(to), reason, keyword, ct);

            return Results.Ok(ApiResponse<List<BlockedAccessByUserDto>>.Ok(rows));
        })
        .WithName("GetBlockedAccessByUser");

        // ── 기록 목록 ───────────────────────────────────────
        //
        // 「누가 어떤 주소를 언제 열려 했나」. 매개변수가 든 **실제 주소**가
        // 이 보기에만 있다 — 「누구의 것을 보려 했나」는 그 글자에만 남는다.
        group.MapGet("", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? userId,
            [FromQuery] string? reason,
            [FromQuery] string? keyword,
            [FromQuery] int? take,
            [FromServices] IBlockedAccessService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetLogsAsync(
                DateOf(from), DateOf(to), userId, reason, keyword,
                Math.Clamp(take ?? MaxTake, 1, MaxTake), ct);

            return Results.Ok(ApiResponse<List<BlockedAccessDto>>.Ok(rows));
        })
        .WithName("GetBlockedAccessLogs");
    }

    /// <summary>
    /// 화면이 고른 <b>한국 달력 날짜</b>. 하루의 경계는 서비스가 잡는다.
    /// </summary>
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
        var roles = http.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(string.IsNullOrWhiteSpace(user.Role) ? [] : new[] { user.Role })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (await access.CanViewAsync(roles, BlockedRouteKey, BlockedPaths, ct))
        {
            return null;
        }

        return Results.Json(
            ApiResponse<object>.Fail(
                "이 화면을 볼 권한이 없습니다. 메뉴 권한에서 역할을 확인하세요.", "403"),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
