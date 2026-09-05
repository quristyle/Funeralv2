using System.Reflection;
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
            var wire = await gateway.GetListAsync<MenuWireDto>("auth/menu/all", cancellationToken);
            _all = [.. wire.Select(w => w.ToNode())];
            logger.LogInformation("메뉴를 읽었다: 최상위 {Count}개", _all.Count);
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

    public void SetViewport(Viewport viewport)
    {
        if (_viewport == viewport)
        {
            return;
        }

        _viewport = viewport;
        Reapply();
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
                if (!node.IsCatalog && !node.IsExternalLink && !string.IsNullOrWhiteSpace(node.Path))
                {
                    into.Add(node.Path);
                }
                Collect(node.Children, into);
            }
        }
    }
}

/// <summary>
/// 이 앱이 가진 <c>@page</c> 경로를 모아 둔다. 기동 때 한 번 만든다.
///
/// 라우트가 컴파일 시점에 고정이라 <b>가능해진</b> 일이다. Vue 에서는 라우트가
/// DB 에서 만들어졌으니 이런 대조 자체가 성립하지 않았다.
///
/// 앱마다 자기 것만 안다 — 업무 앱이 독립 프로세스라 남의 라우트를 볼 방법이 없다.
/// 그래서 대조도 앱 단위다: 장례식장 앱은 <c>/funeral/*</c> 메뉴만 대조하고
/// 나머지는 "내 소관이 아님" 으로 넘긴다.
/// </summary>
public sealed class RouteInventory
{
    private RouteInventory(IReadOnlySet<string> paths)
    {
        Paths = paths;
    }

    /// <summary>
    /// 이 앱의 라우트를 <b>브라우저에 보이는 전체 경로</b>로 담는다
    /// (<c>/projmng/status</c>). 매개변수 자리(<c>{id}</c>)는 그대로 둔다.
    ///
    /// 앱 안의 <c>@page</c> 는 접두사가 없는 상대 경로다(<c>UsePathBase</c> 가
    /// 붙여 준다). 그런데 DB 메뉴와 권한표의 열쇠는 접두사가 붙은 전체 경로라,
    /// 대조하려면 여기서 붙여 두어야 한다.
    /// </summary>
    public IReadOnlySet<string> Paths { get; }

    /// <summary>
    /// 어셈블리를 훑어 <c>@page</c> 를 모으고, 앞에 접두사를 붙인다.
    /// </summary>
    /// <param name="routePrefix">이 앱의 접두사 (<c>/projmng</c>). 셸은 빈 문자열.</param>
    /// <param name="assemblies">라우트를 담고 있는 어셈블리들</param>
    public static RouteInventory Build(string routePrefix, params Assembly[] assemblies)
    {
        var prefix = routePrefix.TrimEnd('/');
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var attribute in type.GetCustomAttributes(
                    typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false))
                {
                    if (attribute is Microsoft.AspNetCore.Components.RouteAttribute route)
                    {
                        paths.Add(Combine(prefix, route.Template));
                    }
                }
            }
        }

        return new RouteInventory(paths);
    }

    /// <summary>
    /// 접두사와 <c>@page</c> 를 이어 붙인다.
    ///
    /// 앱의 첫 화면은 <c>@page "/"</c> 이고 그 전체 경로는 <c>/projmng</c> 다 —
    /// <c>/projmng/</c> 가 아니다. 끝에 <c>/</c> 가 남으면 DB 의 <c>path</c>
    /// (<c>/projmng</c>)와 안 맞아서 "메뉴는 있는데 화면이 없다" 로 잘못 보고된다.
    /// </summary>
    private static string Combine(string prefix, string template)
    {
        if (template is "/" or "")
        {
            return prefix.Length > 0 ? prefix : "/";
        }

        return prefix + (template.StartsWith('/') ? template : "/" + template);
    }
}
