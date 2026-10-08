using AIAgentServer.Data;
using AIAgentServer.DTOs;
using AIAgentServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Endpoints;

/// <summary>
/// 사람별 AI 사용량을 꺼내 보는 길. 포털관리의 「AI 사용량」 화면이 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// [여기에 있는 까닭]
/// </para>
///
/// <para>
/// 자료를 만드는 쪽이 여기다(<see cref="Services.AiUsageLog"/>). 읽는 길만
/// 다른 서비스에 두면 같은 표를 둘이 알게 되고, 칸을 늘릴 때 한쪽만 고쳐진다.
/// </para>
///
/// <para>
/// [<b>그 화면을 볼 수 있는 사람</b>이면 조회한다 — 서버에서 한 번 더 본다]
/// </para>
///
/// <para>
/// 게이트웨이의 <c>ai-route</c> 는 <c>/api/ai/**</c> 를 통째로 <b>익명</b>으로
/// 열어 둔다(대화 자체가 그 길로 가기 때문이다). 그래서 화면의 메뉴 권한만
/// 믿으면 주소를 아는 누구나 <b>동료가 무엇을 얼마나 묻는지</b> 받아 갈 수 있다.
/// </para>
///
/// <para>
/// 다만 <b>역할 이름을 여기 적지 않는다.</b> 처음에는 배포 현황·오류 추적처럼
/// 손으로 적어 두었는데, 이 메뉴는 <c>SERVER_ADMIN</c> 에게도 열려 있어서 그
/// 역할을 가진 사람이 <b>사이드바에는 메뉴가 보이는데 조회하면 403</b> 이었다.
/// 지금은 사이드바와 <b>같은 표</b>(<c>scom.role_menus</c>)에 묻는다 —
/// <see cref="Services.MenuAccess"/> 머리말에 자세히 적었다.
/// </para>
///
/// <para>
/// [집계는 DB 가 한다]
/// </para>
///
/// <para>
/// 줄을 다 받아다 화면에서 더하지 않는다. 한 달이면 수천 줄이고, 그것을
/// 회로로 옮기는 것만으로 화면이 느려진다 — 묻는 것은 사람당 한 줄이다.
/// </para>
/// </remarks>
public static class AiUsageEndpoints
{
    /// <summary>
    /// 한 번에 돌려주는 최근 호출의 최대 줄 수.
    /// </summary>
    /// <remarks>
    /// 파고드는 자리라 전부 줄 까닭이 없다. <b>잘렸다는 사실을 화면이 말한다</b> —
    /// 말 없이 자르면 「이 사람은 200번만 썼다」로 읽힌다.
    /// </remarks>
    private const int RecentCap = 200;

    public static void MapAiUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/usage");

        // ── 사람별 집계 ─────────────────────────────────────
        group.MapGet("/by-user", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? feature,
            [FromServices] AiUsageDbContext db,
            [FromServices] MenuAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var (start, end) = Window(from, to);

            var rows = await Filtered(db, start, end, feature)
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Calls = g.Count(),
                    FailedCalls = g.Count(x => !x.Ok),

                    // **토큰은 준 것만 더한다.** 안 준 줄을 0 으로 치면 합계가
                    // 조용히 작아진다(표 머리말). 몇 줄이 빠졌는지도 함께 준다.
                    PromptTokens = g.Sum(x => (long?)x.PromptTokens) ?? 0,
                    CompletionTokens = g.Sum(x => (long?)x.CompletionTokens) ?? 0,
                    TotalTokens = g.Sum(x => (long?)x.TotalTokens) ?? 0,
                    UnknownTokenCalls = g.Count(x => x.TotalTokens == null),

                    AvgLatencyMs = (int?)g.Average(x => (double?)x.LatencyMs),
                    LastCallAt = g.Max(x => x.OccurredAt),
                })
                .ToListAsync(ct);

            // 이름은 여기서 붙인다. 조인으로 묶으면 계정이 지워진 사람의 줄이
            // 통째로 사라져 **합계가 조용히 줄어든다** — 쓴 것은 쓴 것이다.
            var names = await NameMapAsync(db, rows.Select(r => r.UserId), ct);

            var result = rows
                .Select(r => new AiUsageByUserDto
                {
                    UserId = r.UserId,
                    UserName = r.UserId is null
                        ? null
                        : names.GetValueOrDefault(r.UserId),
                    Calls = r.Calls,
                    FailedCalls = r.FailedCalls,
                    PromptTokens = r.PromptTokens,
                    CompletionTokens = r.CompletionTokens,
                    TotalTokens = r.TotalTokens,
                    UnknownTokenCalls = r.UnknownTokenCalls,
                    AvgLatencyMs = r.AvgLatencyMs,
                    LastCallAt = r.LastCallAt,
                })
                .OrderByDescending(r => r.TotalTokens)
                .ThenByDescending(r => r.Calls)
                .ToList();

            return Results.Ok(ApiResponse<List<AiUsageByUserDto>>.Ok(result));
        })
        .WithName("GetAiUsageByUser");

        // ── 최근 호출 ───────────────────────────────────────
        //
        // 집계가 답하지 못하는 것을 이쪽이 답한다 — 「왜 많은가」다.
        // 어느 기능이 · 어느 모델이 · 한 번에 몇 토큰이었나.
        group.MapGet("/recent", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? feature,
            [FromQuery] string? userId,
            [FromServices] AiUsageDbContext db,
            [FromServices] MenuAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var (start, end) = Window(from, to);
            var query = Filtered(db, start, end, feature);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(x => x.UserId == userId);
            }

            var rows = await query
                .OrderByDescending(x => x.OccurredAt)
                .Take(RecentCap)
                .Select(x => new AiUsageCallDto
                {
                    OccurredAt = x.OccurredAt,
                    UserId = x.UserId,
                    Feature = x.Feature,
                    ProviderKey = x.ProviderKey,
                    Model = x.Model,
                    PromptTokens = x.PromptTokens,
                    CompletionTokens = x.CompletionTokens,
                    TotalTokens = x.TotalTokens,
                    LatencyMs = x.LatencyMs,
                    Ok = x.Ok,
                    FailReason = x.FailReason,
                })
                .ToListAsync(ct);

            var names = await NameMapAsync(db, rows.Select(r => r.UserId), ct);

            foreach (var row in rows)
            {
                row.UserName = row.UserId is null ? null : names.GetValueOrDefault(row.UserId);
            }

            return Results.Ok(ApiResponse<List<AiUsageCallDto>>.Ok(rows));
        })
        .WithName("GetAiUsageRecent");
    }

    /// <summary>
    /// 기간과 기능으로 좁힌 줄들. <b>두 길이 같은 조건을 써야 한다</b> —
    /// 갈라 적으면 집계와 목록의 건수가 어긋나고, 그때 어느 쪽이 맞는지
    /// 아무도 말할 수 없다.
    /// </summary>
    private static IQueryable<AiUsageRow> Filtered(
        AiUsageDbContext db, DateTime start, DateTime end, string? feature)
    {
        var query = db.AiUsageLogs
            .AsNoTracking()
            .Where(x => x.OccurredAt >= start && x.OccurredAt < end);

        return string.IsNullOrWhiteSpace(feature)
            ? query
            : query.Where(x => x.Feature == feature);
    }

    /// <summary>
    /// 로그인 아이디 → 이름. <b>못 찾는 아이디가 있는 것이 정상이다</b> —
    /// 계정을 지워도 그 사람이 쓴 줄은 남는다.
    /// </summary>
    private static async Task<Dictionary<string, string?>> NameMapAsync(
        AiUsageDbContext db, IEnumerable<string?> userIds, CancellationToken ct)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0) return [];

        return await db.Accounts
            .AsNoTracking()
            .Where(a => ids.Contains(a.UserId))
            .ToDictionaryAsync(a => a.UserId, a => a.UserName, StringComparer.Ordinal, ct);
    }

    /// <summary>
    /// 기본 기간은 <b>최근 30일</b>이다. 끝은 <b>받은 날짜의 다음날 0시</b>로
    /// 잡는다 — 그냥 그 날짜로 두면 그날 하루가 통째로 빠진다.
    /// </summary>
    /// <remarks>
    /// 들어오는 값도 돌려주는 값도 <b>UTC</b>다. 한국 시각으로 바꾸는 일은
    /// 화면이 보여 주기 직전에 한 번만 한다(docs/utc-time.md).
    /// </remarks>
    private static (DateTime Start, DateTime End) Window(DateTime? from, DateTime? to)
    {
        var end = to.HasValue
            ? DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc)
            : DateTime.UtcNow.Date.AddDays(1);

        var start = from.HasValue
            ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc)
            : end.AddDays(-31);

        return (start, end);
    }

    /// <summary>이 화면의 열쇠. 화면이 <c>RouteKey</c> 로 선언한 글자와 같아야 한다.</summary>
    private const string UsageRouteKey = "admin.status.ai-usage";

    /// <summary>열쇠가 아직 안 채워진 DB 를 위한 경로 대비책.</summary>
    private static readonly string[] UsagePaths = ["/admin/status/ai-usage"];

    /// <summary>
    /// 볼 수 있는 사람인가. 막아야 하면 그 응답을, 통과면 <c>null</c> 을 준다.
    /// </summary>
    /// <remarks>
    /// <b>역할 이름을 여기서 따지지 않는다.</b> 메뉴 권한표에 묻는다 —
    /// 사이드바가 그 메뉴를 보여 주기로 한 근거와 같은 값이라, 「보이는데
    /// 누르면 403」이 생기지 않는다(<see cref="MenuAccess"/> 머리말).
    /// </remarks>
    private static async Task<IResult?> ForbidAsync(
        UserContext? user, HttpContext http, MenuAccess access, CancellationToken ct)
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
