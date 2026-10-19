using JSini.Web.Abstractions;
using JSini.Web.Models.Menu;
using JSini.Web.Http;


namespace JSini.Web.Components.Menu;

/// <summary>메뉴 트리를 서버에서 읽고 권한·화면 크기로 걸러 사이드바에 내놓는다. scoped — 사용자마다 메뉴가 다르고 화면 크기도 회로마다 다르다. 라우트는 @page 로 고정이라 메뉴가 바뀌어도 이 트리만 다시 읽으면 된다.</summary>
/// <summary>걸러 둔 메뉴 트리를 기억하는 통. 사이드바 트리 만들기는 링크 채우기와 거르기 두 단계로 179노드짜리 새 객체를 만든다 — 업무를 넘나들 때마다 레이아웃이 새로 생겨 화면 전환마다 다시 돌았고, 새 객체라 DxTreeView 가 값이 안 바뀌어도 179노드를 다시 그렸다. 그래서 통을 PortalBootstrapWire 안에 둔다: 통이 살아 있다는 것이 메뉴·권한표가 그때 그대로라는 뜻이라 열쇠가 필요 없다. 권한을 열쇠에 넣으면 권한이 바뀐 뒤에도 옛 트리가 보일 수 있다. 내용은 불변(MenuNode 는 init 전용 record)이라 같은 사람의 창 둘이 나눠 써도 된다.</summary>
public sealed class MenuTree
{
    /// <summary>링크를 채운 트리. 화면 크기와 무관하다.</summary>
    internal IReadOnlyList<MenuNode>? All;

    /// <summary>걸러 둔 트리. 화면 크기마다 하나 — 그것이 거르기의 나머지 입력이다.</summary>
    internal readonly Dictionary<Viewport, IReadOnlyList<MenuNode>> Visible = [];
}

public sealed class MenuProvider(
    GatewayClient gateway,
    IPermissionContext permissions,
    RouteInventory routes,
    ILogger<MenuProvider> logger) : IMenuProvider
{
    private IReadOnlyList<MenuNode> _all = [];
    private Viewport _viewport = Viewport.Desktop;

    /// <summary>만들어 둔 트리를 기억하는 통. 부트스트랩이 넘겨 준다.</summary>
    private MenuTree? _memo;

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

    /// <summary>이미 받아 둔 메뉴를 채운다. 부트스트랩이 쓰는 길이라 게이트웨이를 다시 부르지 않는다.</summary>
    /// <param name="wire">서버가 준 메뉴 목록.</param>
    /// <param name="memo">만들어 둔 트리를 기억하는 통. 주면 같은 것을 두 번 만들지 않는다(MenuTree 머리말). 안 주면 매번 만든다 — 옛 길(ReloadAsync)이 그렇다.</param>
    public void Apply(IReadOnlyList<MenuWireDto> wire, MenuTree? memo = null)
    {
        _memo = memo;

        if (memo?.All is { } remembered)
        {
            // 링크를 채운 트리를 새 객체 그래프로 만드는 일이 비쌌다 — 레이아웃이 업무를 넘나들 때마다 다시 만들고 있었다.
            _all = remembered;
        }
        else
        {
            // 링크 주소를 채운다. Path 는 그대로 둔다 — 권한표와 즐겨찾기의
            // 열쇠가 그 값이기 때문이다.
            _all = [.. wire.Select(w => WithHref(w.ToNode()))];

            if (memo is not null)
            {
                memo.All = _all;
            }

            // **만들었을 때만 남긴다.** 통에서 꺼낸 것까지 찍으면 업무를
            // 옮길 때마다 같은 줄이 쌓여 정작 처음 읽은 시점이 묻힌다.
            logger.LogInformation("메뉴를 읽었다: 최상위 {Count}개", _all.Count);
            ReportUnresolvedKeys();
        }

        Reapply();
    }

    /// <summary>트리 전체에 링크 주소를 채운다. 외부 링크는 앱 라우트가 아니라 옮길 대상이 아니므로 건드리지 않는다.</summary>
    private MenuNode WithHref(MenuNode node) => node with
    {
        Href = HrefOf(node),
        Children = [.. node.Children.Select(WithHref)],
    };

    /// <summary>이 메뉴가 걸 주소를 정한다. 순서가 곧 이행 상태다: 외부 링크는 그대로, 끼워 넣는 메뉴(EMBEDDED)는 공용 화면으로(고를 화면이 없어 열쇠를 보지 않는다), 열쇠가 있으면 카탈로그에서 푼다(정본 — DB 는 URL 을 모른다), 없거나 모르는 열쇠면 옛 길로 떨어진다(RouteAliases 가 Path 를 옮긴다). 3번이 남아 있으면 DB 백필이 안 끝난 것이고, 다 끝나면 3번과 RouteAliases 를 함께 지운다.</summary>
    private string HrefOf(MenuNode node)
    {
        if (node.IsExternalLink)
        {
            return node.Link ?? node.Path;
        }

        // 열쇠보다 앞이다 — 옛 화면의 열쇠가 DB 에 남아 있어도 그 화면은 지워졌으므로 열쇠를 먼저 보면 준비 중이 뜬다. 유형과 주소만으로 판단한다.
        if (node.IsEmbedded && !string.IsNullOrWhiteSpace(node.IframeSrc))
        {
            return EmbeddedRoute.HrefFor(node.Path);
        }

        return routes.Resolve(node.RouteKey) ?? RouteAliases.Resolve(node.Path);
    }

    /// <summary>열쇠는 붙어 있는데 그런 화면이 실려 있지 않은 메뉴를 로그로 남긴다. 보이는데 눌러도 준비 중이 뜨는 상태이고, 원인은 열쇠를 잘못 적었거나 화면이 지워졌거나 모듈이 안 실린 것이다. 기동을 세우지 않는 까닭은 ReportRouteMismatch 와 같다.</summary>
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
                // 끼워 넣는 메뉴는 화면을 안 고른다. 열쇠가 남아 있어도
                // 읽지 않으므로 「화면이 없다」고 알릴 일이 아니다.
                if (!node.IsCatalog
                    && !node.IsExternalLink
                    && !node.IsEmbedded
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

    /// <summary>이 주소에 이르는 메뉴 줄기를 뿌리부터 돌려준다. 가장 긴 짝을 고른다 — /funeral 과 /funeral/status 가 둘 다 있을 때 앞엣것에 먼저 걸리면 브레드크럼이 한 칸에서 멈춘다.</summary>
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

    /// <summary>들고 있던 원본을 지금 기준으로 다시 거른다. 통이 있으면 앞서 만든 것을 돌려준다 — 값이 같아서가 아니라 참조가 같아야 DxTreeView 가 자료가 안 바뀐 것으로 보고 다시 그리지 않는다. 열쇠가 화면 크기인 것은 그것이 MenuFilter 의 나머지 입력이기 때문이다. 권한표는 열쇠에 넣지 않는다 — 통의 수명이 곧 권한표의 수명이다.</summary>
    private void Reapply()
    {
        VisibleMenus = Filtered();
        MenusChanged?.Invoke();
    }

    private IReadOnlyList<MenuNode> Filtered()
    {
        if (_memo is null)
        {
            return MenuFilter.Filter(_all, _viewport, permissions.CanView);
        }

        if (_memo.Visible.TryGetValue(_viewport, out var remembered))
        {
            return remembered;
        }

        var filtered = MenuFilter.Filter(_all, _viewport, permissions.CanView);
        _memo.Visible[_viewport] = filtered;
        return filtered;
    }

    /// <summary>DB 의 메뉴 경로와 모듈이 가진 라우트를 대조해 로그로 남긴다. 어긋나는 쪽은 둘 다 문제다 — DB 에만 있으면 보이는데 404, 라우트에만 있으면 화면이 있는데 아무도 못 찾는다. 이행하는 동안 어긋나는 것은 정상이라 기동을 세우지 않는다. 이행이 끝나면 이 로그가 비어야 한다.</summary>
    /// <param name="routePaths">모듈들이 가진 @page 경로 전부</param>
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
                // 묶음과 외부 링크는 화면이 없는 것이 정상이다. Path 가 아니라 LinkTarget 으로 대조한다 — 사용자가 실제로 열게 되는 주소가 그것이다.
                if (!node.IsCatalog && !node.IsExternalLink && !string.IsNullOrWhiteSpace(node.LinkTarget))
                {
                    into.Add(node.LinkTarget);
                }
                Collect(node.Children, into);
            }
        }
    }
}
