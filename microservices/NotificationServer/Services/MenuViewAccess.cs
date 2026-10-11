using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NotificationServer.Data;

namespace NotificationServer.Services;

/// <summary>
/// 「이 화면을 볼 수 있는 사람인가」를 <b>사이드바와 같은 표에 묻는다</b>
/// (<c>scom.role_menus</c>).
/// </summary>
/// <remarks>
/// <para>
/// AuthServer 의 같은 이름(<c>AuthServer/Services/MenuViewAccess.cs</c>)과 한
/// 벌이다. <b>베껴 둔 것이 맞다</b> — 공용 프로젝트로 올리려면
/// <c>JSini.Shared.Infrastructure</c> 가 EF 와 각 서비스의 <c>DbContext</c> 를
/// 알아야 하는데, 그 셋은 서로 다른 DB 를 보는 서비스들이다. 지금 이것을 쓰는
/// 서비스가 셋째이므로(인증 · AI · 알림) 넷째가 생기면 그때 승격한다
/// (<c>web/CLAUDE.md</c> 「두 모듈이 쓰면 복제, 세 번째부터 승격」과 같은 선).
/// </para>
///
/// <para>
/// [게이트웨이가 막아 주는데 왜 또 보나]
/// </para>
///
/// <para>
/// <c>/api/notification/**</c> 는 익명이 아니라 <b>로그인만</b> 요구한다. 그래서
/// 이 길은 포털에 로그인한 누구나 두드릴 수 있고, 막지 않으면 <b>일반 사용자가
/// 회사의 알림 규칙을 통째로 바꿀 수 있다.</b>
/// </para>
///
/// <para>
/// [역할 이름을 코드에 적지 않는다]
/// </para>
///
/// <para>
/// 적어 두면 그 메뉴의 권한을 한 역할에 더하는 날 <b>사이드바에는 메뉴가
/// 보이는데 조회하면 403</b> 이 된다(AI 사용량이 실제로 그랬다). 판정이 두
/// 군데가 되면 반드시 어긋나므로 <b>메뉴 권한 화면이 정한 것</b>을 읽는다.
/// </para>
///
/// <para>
/// [못 찾으면 막는다]
/// </para>
///
/// <para>
/// 메뉴 줄이 없다는 것은 그 화면에 권한을 준 적이 없다는 뜻이다. 그때
/// 통과시키면 메뉴 SQL 을 안 돌린 환경에서 이 길이 통째로 열린다 — 틀리는
/// 방향은 「안 보이는」 쪽이어야 한다.
/// </para>
/// </remarks>
public sealed class MenuViewAccess(
    IServiceScopeFactory scopes,
    IMemoryCache cache,
    ILogger<MenuViewAccess> logger)
{
    /// <summary>들고 있는 시간. 권한 변경이 늦게 반영되는 쪽이 더 나쁘므로 짧다.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);

    /// <summary>그 역할들 중 하나라도 이 화면을 열 수 있으면 참.</summary>
    /// <param name="roleIds">게이트웨이가 준 역할 전부(<c>X-User-Roles</c>).</param>
    /// <param name="routeKey">화면이 선언한 열쇠.</param>
    /// <param name="paths">열쇠가 아직 안 채워진 DB 를 위한 경로 대비책.</param>
    /// <param name="ct">취소 토큰.</param>
    public async Task<bool> CanViewAsync(
        IReadOnlyCollection<string> roleIds,
        string routeKey,
        string[] paths,
        CancellationToken ct = default)
    {
        if (roleIds.Count == 0) return false;

        var allowed = await AllowedRolesAsync(routeKey, paths, ct);

        return roleIds.Any(allowed.Contains);
    }

    /// <summary>이 화면을 열 수 있는 역할들. 못 찾으면 <b>빈 집합</b>이다.</summary>
    private async Task<HashSet<string>> AllowedRolesAsync(
        string routeKey, string[] paths, CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey(routeKey), out HashSet<string>? hit) && hit is not null)
        {
            return hit;
        }

        HashSet<string> roles;

        try
        {
            // 범위를 따로 연다. 이 서비스는 singleton 이라 요청의 DbContext 를
            // 들고 있을 수 없다.
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var menuId = await db.SystemMenus
                .AsNoTracking()
                .Where(m => !m.IsDeleted && (m.RouteKey == routeKey || paths.Contains(m.Path)))
                .Select(m => m.Id)
                .FirstOrDefaultAsync(ct);

            if (menuId is null)
            {
                logger.LogWarning(
                    "메뉴를 찾지 못해 조회를 막습니다: {RouteKey}. 메뉴 SQL 을 돌렸는지 확인하세요.",
                    routeKey);

                roles = [];
            }
            else
            {
                roles = (await db.RoleMenus
                        .AsNoTracking()
                        .Where(rm => rm.MenuId == menuId && rm.CanView && !rm.IsDeleted)
                        .Select(rm => rm.RoleId)
                        .ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            // **캐시하지 않고 막는다.** DB 를 못 읽은 것을 「권한 없음」으로
            // 담아 두면 한 번 끊긴 순간이 1분 동안 이어진다.
            logger.LogWarning(ex, "메뉴 권한을 읽지 못했습니다: {RouteKey}", routeKey);
            return [];
        }

        cache.Set(CacheKey(routeKey), roles, Ttl);
        return roles;
    }

    private static string CacheKey(string routeKey) => $"menu-view-access:{routeKey}";
}
