using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

public partial class HeaderNavTools
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ThemeDrawer Theme { get; set; } = default!;
    [Inject] private MenuReveal Reveal { get; set; } = default!;
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private ThemeSize Size { get; set; } = default!;

    /// <summary>
    /// 지금 헤더에 선 칸들. 아래 띠와 <b>같은 목록이다</b> — 읽기 전에는
    /// <see cref="BottomNav.Defaults"/> 라 첫 그림에서 빈 자리가 지나가지 않는다.
    /// </summary>
    private IReadOnlyList<BottomNavItem> _items = BottomNav.Defaults;

    private bool _loaded;

    protected override void OnInitialized()
    {
        // 환경설정에서 고친 것이 **같은 회로**라 바로 들어온다. 안 듣고 있으면
        // 고친 사람이 화면을 한 번 옮겨야 헤더가 바뀐다.
        Boot.BottomNavItemsChanged += OnItemsChanged;

        // 아이콘을 메뉴에서 찾으므로(`BottomNav.IconClass`) 메뉴가 늦게 오면
        // 그때 다시 그려야 한다 — 안 그러면 고른 칸이 임의 아이콘으로 남는다.
        Menus.MenusChanged += OnMenusChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _loaded)
        {
            return;
        }

        _loaded = true;

        // `PortalBoot` 의 단일 왕복에 얹혀 간다. 우리 때문에 왕복이 늘지 않는다.
        var items = BottomNav.Parse((await Boot.ReadAsync()).BottomNavItemsJson);

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

    /// <summary>
    /// 칸을 눌렀다. 띠와 <b>같은 판정을 쓴다</b>(<see cref="BottomNav.Open"/>) —
    /// 갈라 적으면 같은 칸이 자리에 따라 다르게 움직인다.
    /// </summary>
    private void Go(BottomNavItem item) =>
        BottomNav.Open(item, Menus.AllMenus, Navigation, Theme, Reveal, UserDrawer);

    /// <summary>
    /// 두 목록이 같은가. 목록끼리 통째로 비교하면 <b>참조만 본다</b> —
    /// 읽어 온 것이 기본값과 똑같을 때 쓸데없이 한 번 더 그리게 된다
    /// (<c>MobileBottomNav</c> 의 같은 이름과 같은 까닭이다).
    /// </summary>
    private static bool SameItems(
        IReadOnlyList<BottomNavItem> left, IReadOnlyList<BottomNavItem> right) =>
        ReferenceEquals(left, right)
        || (left.Count == right.Count && left.SequenceEqual(right));

    public void Dispose()
    {
        Boot.BottomNavItemsChanged -= OnItemsChanged;
        Menus.MenusChanged -= OnMenusChanged;
    }
}
