using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using JSini.Web.Models;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Security;

/// <summary>
/// 화면 하나에 대한 판정. 막혔으면 <b>왜</b>까지 들고 있다.
/// </summary>
/// <param name="Allowed">열어도 되는가.</param>
/// <param name="Reason">막은 까닭(<see cref="BlockedAccessReason"/>). 통과면 <c>null</c>.</param>
/// <param name="MenuPath">권한을 따진 메뉴 경로. 메뉴를 못 찾았으면 <c>null</c>.</param>
/// <param name="RouteKey">화면이 신고한 열쇠.</param>
/// <param name="ScreenType">라우터가 끼우려던 화면 클래스 이름.</param>
public sealed record ScreenVerdict(
    bool Allowed,
    string? Reason = null,
    string? MenuPath = null,
    string? RouteKey = null,
    string? ScreenType = null)
{
    /// <summary>통과. 따질 것이 없을 때도 이것이다.</summary>
    public static readonly ScreenVerdict Pass = new(true);
}

/// <summary>
/// 화면을 열어도 되는가를 정하는 단 한 곳. 사이드바 거르기·탭 줄·브레드크럼·라우터 앞 문(ScreenGate)이 모두 여기에 묻는다. 판정이 두 곳이면 「목록에는 없는데 주소로는 열린다」로 어긋난다.
/// 기본은 차단이다. 가리킬 메뉴를 못 찾으면 막고(<see cref="BlockedAccessReason.Unmanaged"/>) 기록에 남긴다. 상세 화면은 <see cref="ScreenAccessAttribute"/> 로 부모 메뉴를 가리킨다.
/// 권한표·메뉴가 아직 안 왔으면 막지 않는다. 그 사이를 권한 없음으로 다루면 차단 안내가 스치고 기록에 쌓여 진짜 막힌 줄이 묻힌다. 실제 통제는 서버가 한다.
/// scoped 다. 사람마다, 회로마다 권한과 화면이 다르다.
/// </summary>
public sealed class ScreenAccess : IDisposable
{
    private readonly IPermissionContext permissions;
    private readonly IMenuProvider menus;
    private readonly ScreenAccessCatalog catalog;
    private readonly ILogger<ScreenAccess> logger;

    /// <summary>지금 보고 있는 화면. 권한표가 늦게 왔을 때 다시 따지려고 들고 있다.</summary>
    private Type? _pageType;
    private string _href = string.Empty;

    public ScreenAccess(
        IPermissionContext permissions,
        IMenuProvider menus,
        ScreenAccessCatalog catalog,
        ILogger<ScreenAccess> logger)
    {
        this.permissions = permissions;
        this.menus = menus;
        this.catalog = catalog;
        this.logger = logger;

        // 이 줄이 없으면 기본 차단이 사실상 안 돈다. 권한표가 채워진 뒤 다시 따지는 자리다(PortalBootstrap.ApplyAll 이 권한표를 먼저 넣는다).
        menus.MenusChanged += Refresh;
    }

    /// <summary>지금 열려 있는 화면의 판정. ScreenGate 가 넣고 레이아웃이 읽는다. 레이아웃은 주소만 알아 매개변수 있는 상세 화면을 스스로 알아볼 수 없어 이 값을 쓴다.</summary>
    public ScreenVerdict Current { get; private set; } = ScreenVerdict.Pass;

    /// <summary>판정이 바뀌었다. 문이 본문을 다시 그린다.</summary>
    public event Action? Changed;

    /// <summary>
    /// 이 화면으로 들어왔다. 판정을 갱신하고 그 값을 돌려준다.
    /// </summary>
    public ScreenVerdict Enter(Type? pageType, string href)
    {
        _pageType = pageType;
        _href = href;

        return Apply(Judge(pageType, href));
    }

    /// <summary>들고 있던 화면을 지금 기준으로 다시 따진다.</summary>
    private void Refresh()
    {
        if (_pageType is not null)
        {
            Apply(Judge(_pageType, _href));
        }
    }

    private ScreenVerdict Apply(ScreenVerdict verdict)
    {
        if (verdict != Current)
        {
            Current = verdict;
            Changed?.Invoke();
        }

        return verdict;
    }

    public void Dispose() => menus.MenusChanged -= Refresh;

    /// <summary>
    /// 이 화면을 열 수 있는가. <b>아무것도 바꾸지 않는다.</b>
    /// </summary>
    public ScreenVerdict Judge(Type? pageType, string href)
    {
        // 라우터가 화면을 못 찾았다. 그건 차단이 아니라 404 고, `NotFound` 가 받는다.
        if (pageType is null)
        {
            return ScreenVerdict.Pass;
        }

        var rule = catalog.For(pageType);

        // 라우트를 안 들고 있는 컴포넌트. 라우터가 끼울 리가 없지만,
        // 모르는 것을 막으면 「왜 안 열리지」가 되므로 통과시킨다.
        if (rule is null)
        {
            return ScreenVerdict.Pass;
        }

        if (rule.Kind == ScreenAccessKind.Open)
        {
            return ScreenVerdict.Pass;
        }

        // 권한표·메뉴가 아직 안 왔다. 막지 않는다(머리말).
        if (!permissions.IsLoaded || menus.AllMenus.Count == 0)
        {
            return ScreenVerdict.Pass;
        }

        var node = Resolve(rule, href);

        if (node is null)
        {
            logger.LogDebug(
                "가리킬 메뉴가 없어 막는다: {Href} ({Screen})", href, rule.Name);

            return new ScreenVerdict(
                Allowed: false,
                Reason: BlockedAccessReason.Unmanaged,
                MenuPath: null,
                RouteKey: rule.RouteKey,
                ScreenType: rule.Name);
        }

        if (CanOpen(node))
        {
            return ScreenVerdict.Pass;
        }

        return new ScreenVerdict(
            Allowed: false,
            Reason: BlockedAccessReason.NoPermission,
            MenuPath: node.Path,
            RouteKey: rule.RouteKey ?? node.RouteKey,
            ScreenType: rule.Name);
    }

    /// <summary>이 메뉴의 화면을 열 수 있는가. 묶음과 외부 링크는 따지지 않는다(묶음은 자기 화면이 없고, 외부 링크는 권한표에 줄이 없다).</summary>
    public bool CanOpen(MenuNode node)
    {
        if (!permissions.IsLoaded || node.IsCatalog || node.IsExternalLink
            || string.IsNullOrEmpty(node.Path))
        {
            return true;
        }

        return permissions.CanView(node.Path);
    }

    /// <summary>이 화면의 권한을 들고 있는 메뉴를 찾는다. ① 주소 ② 화면의 열쇠 ③ [ScreenAccess] 가 가리킨 부모 순서로 본다. 셋 다 없으면 null.</summary>
    private MenuNode? Resolve(ScreenAccessRule rule, string href)
    {
        if (menus.Trail(href).LastOrDefault() is { } byHref)
        {
            return byHref;
        }

        return ByRouteKey(rule.RouteKey) ?? ByRouteKey(rule.InheritsFrom);
    }

    /// <summary>열쇠로 메뉴를 찾는다. 트리 전체를 훑는다 — 180줄 남짓이라 가볍다.</summary>
    private MenuNode? ByRouteKey(string? routeKey)
    {
        if (string.IsNullOrWhiteSpace(routeKey))
        {
            return null;
        }

        return Find(menus.AllMenus, routeKey);

        static MenuNode? Find(IReadOnlyList<MenuNode> nodes, string key)
        {
            foreach (var node in nodes)
            {
                if (string.Equals(node.RouteKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }

                if (Find(node.Children, key) is { } hit)
                {
                    return hit;
                }
            }

            return null;
        }
    }
}
