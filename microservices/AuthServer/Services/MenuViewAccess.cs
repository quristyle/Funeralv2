using AuthServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AuthServer.Services;

/// <summary>「이 화면을 볼 수 있는 사람인가」를 사이드바와 같은 표(scom.role_menus)에 묻는다.</summary>
/// 게이트웨이가 /api/auth/** 를 익명으로 열어 두므로 관리 조회는 여기서 막는다. 역할 이름을 코드에 적지 않고 메뉴 권한 화면의 판정을 쓴다. 못 찾으면 막는다.
public sealed class MenuViewAccess(
    IServiceScopeFactory scopes,
    IMemoryCache cache,
    ILogger<MenuViewAccess> logger)
{
    /// <summary>들고 있는 시간. 머리말 참고.</summary>
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

    /// <summary>이 화면을 열 수 있는 역할들. 못 찾으면 <b>빈 집합</b>이다(= 아무도 못 본다).</summary>
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
                .Where(m => !m.IsDeleted
                    && (m.RouteKey == routeKey || paths.Contains(m.Path)))
                .Select(m => m.Id)
                .FirstOrDefaultAsync(ct);

            if (menuId is null)
            {
                // 메뉴 SQL 을 안 돌렸거나 지워졌다. **조용히 열지 않는다**(머리말).
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
