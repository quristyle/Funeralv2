using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

public partial class MobileBottomNav
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ThemeDrawer Theme { get; set; } = default!;
    [Inject] private MenuReveal Reveal { get; set; } = default!;
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;

    /// <summary>
    /// 지금 띠에 선 칸들. 읽기 전에는 <see cref="BottomNav.Defaults"/> 다 —
    /// 비워 두면 첫 그림에서 띠가 빈 채로 한 번 지나간다.
    /// </summary>
    private IReadOnlyList<BottomNavItem> _items = BottomNav.Defaults;

    private bool _loaded;

    protected override void OnInitialized()
    {
        // 환경설정에서 고친 것이 **같은 회로**라 바로 들어온다. 안 듣고 있으면
        // 고친 사람이 화면을 한 번 옮겨야 띠가 바뀐다.
        Boot.BottomNavItemsChanged += OnItemsChanged;

        // 아이콘을 메뉴에서 찾으므로(`BottomNav.IconClass`) 메뉴가 늦게 오면
        // 그때 다시 그려야 한다 — 안 그러면 고른 칸이 임의 아이콘으로 남는다.
        Menus.MenusChanged += OnMenusChanged;

        // 얼굴도 같은 까닭이다. 내 정보는 부트스트랩이 끝나야 들어오므로,
        // 안 듣고 있으면 프로필 칸이 첫 글자(또는 `?`)인 채로 남는다.
        Me.Changed += OnMeChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _loaded)
        {
            return;
        }

        _loaded = true;

        // `PortalBoot` 의 단일 왕복에 얹혀 간다. 우리 때문에 왕복이 늘지 않는다.
        var json = (await Boot.ReadAsync()).BottomNavItemsJson;
        var items = BottomNav.Parse(json);

        if (!SameItems(items, _items))
        {
            _items = items;
            StateHasChanged();
        }
    }

    private void OnItemsChanged(string? json)
    {
        _items = BottomNav.Parse(json);
        InvokeAsync(StateHasChanged);
    }

    private void OnMenusChanged() => InvokeAsync(StateHasChanged);

    private void OnMeChanged() => InvokeAsync(StateHasChanged);

    /// <summary>이 칸이 프로필(내 정보)을 여는 칸인가.</summary>
    private static bool IsProfile(BottomNavItem item) =>
        string.Equals(item.Path, BottomNav.ProfilePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 칸을 눌렀다. <b>무슨 일이 일어나는지는 여기가 정하지 않는다</b> —
    /// 같은 칸 목록을 넓은 화면 헤더도 그리므로(<see cref="HeaderNavTools"/>)
    /// 판정을 <see cref="BottomNav.Open"/> 한 곳에 둔다.
    /// </summary>
    private void Go(BottomNavItem item) =>
        BottomNav.Open(item, Menus.AllMenus, Navigation, Theme, Reveal, UserDrawer);

    /// <summary>
    /// 두 목록이 같은가. 목록끼리는 통째로 비교하면 <b>참조만 본다</b> —
    /// 읽어 온 것이 기본값과 똑같을 때 쓸데없이 한 번 더 그리게 된다.
    ///
    /// <para>
    /// 이름을 <c>Equals</c> 로 두지 않는다 — 인자 둘짜리 <c>Equals</c> 는
    /// <see cref="object.Equals(object, object)"/> 를 가려서 경고가 난다.
    /// </para>
    /// </summary>
    private static bool SameItems(
        IReadOnlyList<BottomNavItem> left, IReadOnlyList<BottomNavItem> right) =>
        ReferenceEquals(left, right)
        || (left.Count == right.Count && left.SequenceEqual(right));

    public void Dispose()
    {
        Boot.BottomNavItemsChanged -= OnItemsChanged;
        Menus.MenusChanged -= OnMenusChanged;
        Me.Changed -= OnMeChanged;
    }
}
