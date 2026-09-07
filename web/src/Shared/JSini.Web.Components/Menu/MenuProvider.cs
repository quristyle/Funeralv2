using JSini.Web.Abstractions;
using JSini.Web.Models.Menu;
using JSini.Web.Http;


namespace JSini.Web.Components.Menu;

/// <summary>
/// 메뉴 트리를 서버에서 읽고, 권한·화면 크기로 걸러 사이드바에 내놓는다.
///
/// scoped 다 — 사용자마다 다른 메뉴를 보고, 화면 크기도 회로마다 다르다.
///
/// [Vue 때보다 훨씬 단순해진 자리]
///
/// 예전에는 메뉴를 다시 읽을 때 라우트까지 다시 만들어야 했다. 없어진 화면의
/// 라우트를 <c>router.removeRoute</c> 로 걷어내고, 새 화면의 라우트를 더하고,
/// 이름이 겹치는지 검사하고… (<c>router/access.ts</c> 의 <c>refreshAccessMenus</c>).
/// 라우트가 DB 에서 만들어졌기 때문에 생긴 일이었다.
///
/// 이제 라우트는 <c>@page</c> 로 고정이라 <b>절대 바뀌지 않는다</b>. 메뉴가
/// 바뀌면 이 트리만 다시 읽으면 되고, 그 코드가 통째로 사라졌다.
/// </summary>
public sealed class MenuProvider(
    GatewayClient gateway,
    IPermissionContext permissions,
    RouteInventory routes,
    ILogger<MenuProvider> logger) : IMenuProvider
{
    private IReadOnlyList<MenuNode> _all = [];
    private Viewport _viewport = Viewport.Desktop;

    public IReadOnlyList<MenuNode> VisibleMenus { get; private set; } = [];

    public IReadOnlyList<MenuNode> AllMenus => _all;

    public event Action? MenusChanged;

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Apply(await gateway.GetListAsync<MenuWireDto>("auth/menu/all", cancellationToken));
            return;
        }
        catch (ApiException ex)
        {
            // 메뉴를 못 읽으면 사이드바가 빈다. 그래도 라우트는 살아 있으므로
            // 주소를 아는 사람은 화면을 열 수 있다 — 통째로 죽이지 않는다.
            logger.LogError(ex, "메뉴를 읽지 못했다. 사이드바가 빈 채로 뜬다.");
            _all = [];
        }

        Reapply();
    }

    /// <summary>
    /// 이미 받아 둔 메뉴를 채운다. 부트스트랩 한 방(<c>PortalBootstrap</c>)이
    /// 쓰는 길이다 — 게이트웨이를 다시 부르지 않는다.
    /// </summary>
    public void Apply(IReadOnlyList<MenuWireDto> wire)
    {
        // 링크 주소를 채운다. Path 는 그대로 둔다 — 권한표와 즐겨찾기의
        // 열쇠가 그 값이기 때문이다.
        _all = [.. wire.Select(w => WithHref(w.ToNode()))];

        logger.LogInformation("메뉴를 읽었다: 최상위 {Count}개", _all.Count);
        ReportUnresolvedKeys();

        Reapply();
    }

    /// <summary>
    /// 트리 전체에 링크 주소를 채운다. 외부 링크는 건드리지 않는다 —
    /// 앱 라우트가 아니라 옮길 대상이 아니다.
    /// </summary>
    private MenuNode WithHref(MenuNode node) => node with
    {
        Href = HrefOf(node),
        Children = [.. node.Children.Select(WithHref)],
    };

    /// <summary>
    /// 이 메뉴가 걸 주소를 정한다. <b>순서가 곧 이행 상태다.</b>
    ///
    /// <list type="number">
    ///   <item>외부 링크는 그대로 나간다.</item>
    ///   <item><b>열쇠가 있으면 카탈로그에서 푼다.</b> 이것이 정본이다 —
    ///         DB 는 URL 을 모르고, 실려 있는 화면이 자기 주소를 안다.</item>
    ///   <item>열쇠가 없거나 모르는 열쇠면 옛 길로 떨어진다
    ///         (<c>RouteAliases</c> 가 <c>Path</c> 를 옮긴다).</item>
    /// </list>
    ///
    /// 3번이 남아 있는 동안에는 DB 백필이 안 끝난 것이다. 다 끝나면 3번과
    /// <c>RouteAliases</c> 를 함께 지운다.
    /// </summary>
    private string HrefOf(MenuNode node)
    {
        if (node.IsExternalLink)
        {
            return node.Link ?? node.Path;
        }

        return routes.Resolve(node.RouteKey) ?? RouteAliases.Resolve(node.Path);
    }

    /// <summary>
    /// 열쇠는 붙어 있는데 그런 화면이 안 실려 있는 메뉴를 로그로 남긴다.
    ///
    /// 이 상태는 <b>메뉴는 보이는데 눌러도 「준비 중」</b>으로 나타난다. 옛
    /// 경로 불일치와 증상이 같지만 원인이 다르다 — 열쇠를 잘못 적었거나,
    /// 화면이 지워졌는데 메뉴가 남았거나, 그 모듈이 안 실렸거나다.
    ///
    /// 기동을 세우지 않는 이유는 <c>ReportRouteMismatch</c> 와 같다.
    /// </summary>
    private void ReportUnresolvedKeys()
    {
        var unresolved = new List<string>();
        Walk(_all);

        if (unresolved.Count > 0)
        {
            logger.LogWarning(
                "열쇠는 있는데 그 화면이 없는 메뉴 {Count}개: {Keys}",
                unresolved.Count, string.Join(", ", unresolved.Take(20)));
        }

        void Walk(IReadOnlyList<MenuNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!node.IsCatalog
                    && !node.IsExternalLink
                    && !string.IsNullOrWhiteSpace(node.RouteKey)
                    && routes.Resolve(node.RouteKey) is null)
                {
                    unresolved.Add(node.RouteKey);
                }

                Walk(node.Children);
            }
        }
    }

    public void SetViewport(Viewport viewport)
    {
        if (_viewport == viewport)
        {
            return;
        }

        _viewport = viewport;
        Reapply();
    }

    /// <summary>
    /// 이 주소에 이르는 메뉴 줄기를 뿌리부터.
    ///
    /// 가장 <b>긴</b> 짝을 고른다. <c>/funeral</c> 과 <c>/funeral/status</c> 가
    /// 둘 다 있을 때 앞엣것에 먼저 걸리면 브레드크럼이 한 칸에서 멈춘다.
    /// </summary>
    public IReadOnlyList<MenuNode> Trail(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return [];
        }

        var best = new List<MenuNode>();
        Walk(_all, [], href, best);
        return best;

        static void Walk(
            IReadOnlyList<MenuNode> nodes,
            List<MenuNode> path,
            string href,
            List<MenuNode> best)
        {
            foreach (var node in nodes)
            {
                path.Add(node);

                if (string.Equals(node.LinkTarget, href, StringComparison.OrdinalIgnoreCase)
                    && path.Count > best.Count)
                {
                    best.Clear();
                    best.AddRange(path);
                }

                Walk(node.Children, path, href, best);
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    /// <summary>들고 있던 원본을 지금 기준으로 다시 거른다.</summary>
    private void Reapply()
    {
        VisibleMenus = MenuFilter.Filter(_all, _viewport, permissions.CanView);
        MenusChanged?.Invoke();
    }

    /// <summary>
    /// DB 의 메뉴 경로와 모듈이 실제로 가진 라우트를 대조한다.
    ///
    /// 어긋나는 쪽이 둘 다 문제다.
    ///   · DB 에 있는데 라우트가 없다 → 메뉴는 보이는데 눌러도 404
    ///   · 라우트는 있는데 DB 에 없다 → 화면이 있는데 아무도 못 찾는다(권한표에도 없다)
    ///
    /// 기동을 세우지는 않는다 — 이행하는 동안에는 어긋나는 것이 정상이고,
    /// 여기서 죽이면 한 화면 때문에 포털 전체가 안 뜬다. 대신 로그로 정확히
    /// 몇 개가 어느 쪽으로 어긋났는지 남긴다. 이행이 끝나면 이 로그가 비어야 한다.
    /// </summary>
    /// <param name="routePaths">모듈들이 가진 <c>@page</c> 경로 전부</param>
    public void ReportRouteMismatch(IReadOnlySet<string> routePaths)
    {
        var menuPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Collect(_all, menuPaths);

        var missingRoutes = menuPaths.Except(routePaths, StringComparer.OrdinalIgnoreCase).ToList();
        if (missingRoutes.Count > 0)
        {
            logger.LogWarning(
                "메뉴에는 있는데 화면이 없는 경로 {Count}개: {Paths}",
                missingRoutes.Count, string.Join(", ", missingRoutes.Take(20)));
        }

        var orphanRoutes = routePaths.Except(menuPaths, StringComparer.OrdinalIgnoreCase).ToList();
        if (orphanRoutes.Count > 0)
        {
            logger.LogInformation(
                "화면은 있는데 메뉴에 없는 경로 {Count}개: {Paths}",
                orphanRoutes.Count, string.Join(", ", orphanRoutes.Take(20)));
        }

        static void Collect(IReadOnlyList<MenuNode> nodes, HashSet<string> into)
        {
            foreach (var node in nodes)
            {
                // 묶음과 외부 링크는 화면이 없는 것이 정상이다.
                // **Path 가 아니라 LinkTarget 으로 대조한다.** 사용자가 실제로
                // 열게 되는 주소가 그것이고, 라우트가 있느냐는 그 주소로 판정해야 한다.
                if (!node.IsCatalog && !node.IsExternalLink && !string.IsNullOrWhiteSpace(node.LinkTarget))
                {
                    into.Add(node.LinkTarget);
                }
                Collect(node.Children, into);
            }
        }
    }
}
