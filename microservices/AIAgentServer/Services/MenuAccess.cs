using AIAgentServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AIAgentServer.Services;

/// <summary>
/// 「이 화면을 볼 수 있는 사람인가」를 <b>사이드바와 같은 표에 묻는다</b>
/// (<c>scom.role_menus</c>).
/// </summary>
/// <remarks>
/// <para>
/// [역할 이름을 손으로 적지 않는다 — 실제로 밟았다]
/// </para>
///
/// <para>
/// 처음에는 배포 현황·오류 추적이 하던 대로 역할 이름을 코드에 적어 두었다
/// (<c>ADMINISTRATOR</c> · <c>SYSTEM_ADMINISTRATOR</c>). 그런데 「AI 사용량」
/// 메뉴는 <c>SERVER_ADMIN</c> 에게도 열려 있어서, 그 역할을 가진 사람은
/// <b>사이드바에는 메뉴가 보이는데 눌러서 조회하면 403</b> 이었다. 이 저장소가
/// 줄곧 경계해 온 바로 그 모양이다 — 판정이 두 군데가 되면 반드시 어긋난다
/// (<c>IPermissionContext</c> 머리말).
/// </para>
///
/// <para>
/// 그래서 <b>권한을 여기서 계산하지 않는다.</b> 메뉴 권한 화면에서 역할을
/// 더하거나 빼면 그 순간부터 이 판정도 따라간다 — 서버를 고칠 일이 없다.
/// </para>
///
/// <para>
/// [열쇠로 찾는다]
/// </para>
///
/// <para>
/// DB 의 <c>path</c> 에는 아직 옛 경로가 남아 있을 수 있고 언젠가 바뀐다.
/// 열쇠(<c>route_key</c>)는 한 번 정하면 안 바꾸는 값이라 그쪽으로 찾는다 —
/// 화면이 <c>RouteKeyAttribute</c> 로 선언한 것과 같은 글자다. 열쇠가 아직
/// 안 채워진 DB 를 위해 경로로도 한 번 더 본다.
/// </para>
///
/// <para>
/// [못 찾으면 막는다]
/// </para>
///
/// <para>
/// 메뉴 줄이 없다는 것은 <b>그 화면에 권한을 준 적이 없다</b>는 뜻이다.
/// 그때 통과시키면 메뉴 SQL 을 안 돌린 환경에서 이 길이 통째로 열린다 —
/// 틀리는 방향은 「안 보이는」 쪽이어야 한다. 화면에는 그 사정을 적어 준다.
/// </para>
///
/// <para>
/// [잠깐 들고 있는다]
/// </para>
///
/// <para>
/// 조회 한 번에 권한 질의가 한 번 더 붙는 셈이라 짧게 캐시한다. <b>1분</b>인
/// 것은 권한 변경이 늦게 반영되는 쪽이 더 나쁘기 때문이다 — 포털 부트스트랩
/// 통(2분)과 같은 생각이고, 거기보다 더 짧게 잡았다.
/// </para>
/// </remarks>
public sealed class MenuAccess(
    IServiceScopeFactory scopes,
    IMemoryCache cache,
    ILogger<MenuAccess> logger)
{
    /// <summary>들고 있는 시간. 머리말 참고.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 그 역할들 중 하나라도 이 화면을 열 수 있으면 참.
    /// </summary>
    /// <param name="roleIds">게이트웨이가 준 역할 전부(<c>X-User-Roles</c>).</param>
    /// <param name="routeKey">화면이 선언한 열쇠.</param>
    /// <param name="paths">열쇠가 아직 안 채워진 DB 를 위한 경로 대비책.</param>
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

    /// <summary>
    /// 이 화면을 열 수 있는 역할들. 못 찾으면 <b>빈 집합</b>이다(= 아무도 못 본다).
    /// </summary>
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
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

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

    private static string CacheKey(string routeKey) => $"menu-access:{routeKey}";
}
