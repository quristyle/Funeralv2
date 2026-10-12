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
/// <b>화면을 열어도 되는가를 정하는 단 한 곳.</b>
/// </summary>
/// <remarks>
/// <para>
/// 사이드바 거르기(<c>MenuFilter</c>) · 탭 줄 · 브레드크럼 · 라우터 바로 앞의
/// 문(<c>ScreenGate</c>)이 모두 여기에 묻는다. 판정이 두 군데가 되면 반드시
/// 어긋나고, 어긋난 자리가 「목록에는 없는데 주소로는 열린다」가 된다 —
/// <see cref="IPermissionContext"/> 머리말이 말하는 그 규칙을 화면 단위로 넓힌 것이다.
/// </para>
///
/// <para>
/// [기본이 차단이다 — 2026-10-13 에 뒤집었다]
/// </para>
///
/// <para>
/// 그 전에는 <b>메뉴에 줄이 있는 화면만</b> 따졌다. 메뉴에 없는 화면(목록에서
/// 줄을 골라 들어가는 상세 화면·진단 화면)은 따질 열쇠가 없어 그냥 지나쳤고,
/// 그래서 계정 관리 권한이 없는 사람이 <c>/admin/system/account/{남의
/// 아이디}/app</c> 을 <b>주소로 쳐서 열 수 있었다.</b> 지금은 반대다 —
/// 가리킬 메뉴를 못 찾으면 막고(<see cref="BlockedAccessReason.Unmanaged"/>)
/// 그 사실을 기록에 남긴다. 상세 화면은
/// <see cref="ScreenAccessAttribute">[ScreenAccess]</see> 로 부모 메뉴를 가리킨다.
/// </para>
///
/// <para>
/// [아직 모르는 동안에는 막지 않는다]
/// </para>
///
/// <para>
/// 권한표와 메뉴는 부트스트랩이 늦게 채운다. 그 사이를 「권한 없음」으로 다루면
/// 새로고침할 때마다 차단 안내가 한 번 스치고, 그 한 번이 차단 기록에도 쌓여
/// <b>진짜 막힌 줄이 묻힌다.</b> 실제 통제는 서버가 계속 하므로 이쪽이 안전 범위
/// 안이다(<see cref="IPermissionContext.IsLoaded"/> 와 같은 판단이다).
/// </para>
///
/// <para>
/// scoped 다 — 사람마다 권한이 다르고, 지금 보고 있는 화면도 회로마다 다르다.
/// </para>
/// </remarks>
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

        // **이 줄이 없으면 기본 차단이 사실상 안 돈다.**
        //
        // 첫 그림은 권한표가 오기 전이라 무엇이든 통과다(머리말). 부트스트랩이
        // 권한표와 메뉴를 채우는 것이 그 뒤인데, 그때 아무도 다시 묻지 않으면
        // 그 통과가 그대로 굳는다. 메뉴가 다시 걸러졌다는 소식이 곧 「권한표가
        // 채워졌다」는 소식이라(`PortalBootstrap.ApplyAll` 이 권한표를 먼저
        // 넣는다) 그 자리에서 다시 따진다.
        menus.MenusChanged += Refresh;
    }

    /// <summary>
    /// 지금 열려 있는 화면의 판정. <c>ScreenGate</c> 가 넣고 레이아웃이 읽는다.
    /// </summary>
    /// <remarks>
    /// <b>레이아웃이 스스로 다시 따지지 않게 하려는 값이다.</b> 레이아웃이 아는
    /// 것은 주소뿐이라 매개변수가 든 상세 화면을 제 힘으로 알아볼 수 없고,
    /// 알아보려면 라우터가 이미 한 짝맞추기를 한 벌 더 두어야 한다.
    /// </remarks>
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

    /// <summary>
    /// 이 메뉴의 화면을 열 수 있는가. 사이드바를 거르는 규칙과 같다.
    /// </summary>
    /// <remarks>
    /// <b>묶음과 외부 링크는 따지지 않는다.</b> 묶음에는 자기 화면이 없고(길잡이
    /// 화면이 그리는 것은 링크뿐인데 그 링크들은 각자 다시 걸러진다), 외부 링크는
    /// 앱 라우트가 아니라 권한표에 줄이 없다 — 막으면 틀리는 방향이
    /// 「멀쩡한 메뉴가 안 열리는」 쪽이다.
    /// </remarks>
    public bool CanOpen(MenuNode node)
    {
        if (!permissions.IsLoaded || node.IsCatalog || node.IsExternalLink
            || string.IsNullOrEmpty(node.Path))
        {
            return true;
        }

        return permissions.CanView(node.Path);
    }

    /// <summary>
    /// 이 화면의 권한을 들고 있는 메뉴를 찾는다. 셋째까지 빈손이면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ① <b>주소로</b> — 사이드바가 건 링크와 같은 값이면 그 메뉴다. 메뉴에
    /// 올라 있는 화면 대부분이 여기서 끝난다.
    /// </para>
    /// <para>
    /// ② <b>자기 열쇠로</b> — 주소가 메뉴의 <c>path</c> 와 어긋나 있어도
    /// (<c>RouteAliases</c> 가 아직 안 걷힌 자리) 열쇠는 맞는다.
    /// </para>
    /// <para>
    /// ③ <b>물려받기로</b> — 메뉴에 올릴 수 없는 상세 화면이 부모를 가리킨 것.
    /// </para>
    /// </remarks>
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
