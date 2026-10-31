using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Components.Layout;

public partial class SidebarMenu
{
    [Inject] private MenuFavorites Favorites { get; set; } = default!;
    [Inject] private MenuReveal Reveal { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>그릴 메뉴들. **이미 걸러진 것**이어야 한다.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<MenuNode> Nodes { get; set; } = [];

    /// <summary>지금 보고 있는 화면의 메뉴 경로(MenuNode.Path). 사이드바가 펴질 때마다 이 가지를 펴고 골라 준다. 레이아웃이 주소로 찾아 둔 줄기의 끝이라 브레드크럼과 같은 줄기를 본다 — 따로 찾으면 둘이 다른 메뉴를 가리킨다.</summary>
    [Parameter] public string? ActivePath { get; set; }

    /// <summary>사이드바가 펴져 있나. 접힘은 레이아웃이 들고 있고 여기서는 방금 펴졌는지를 알아채는 데만 쓴다.</summary>
    [Parameter] public bool IsOpen { get; set; } = true;

    /// <summary>메뉴 정보를 서버에서 다시 받는다. 받았으면 true. 레이아웃이 준다 — 메뉴를 그리는 서비스와 같은 것을 다시 채워야 사이드바에 닿는다.</summary>
    [Parameter] public Func<Task<bool>>? Refresh { get; set; }

    private enum Tab { Menu, Favorites }

    private DxContextMenu? _tabMenu;

    /// <summary>다시 받는 중. 연달아 눌러 왕복이 겹치지 않게 막는다.</summary>
    private bool _refreshing;

    private Tab _tab = Tab.Menu;

    /// <summary>메뉴 검색어. 고른 탭의 트리에 그대로 넘어간다.</summary>
    private string _search = string.Empty;

    private DxTreeView? _tree;

    /// <summary>즐겨찾기 트리를 다음 렌더 뒤에 펼쳐야 한다.</summary>
    private bool _expandFavorites;

    /// <summary>다음 렌더 뒤에 펴서 골라야 할 메뉴의 경로. 브레드크럼이 넣는다. 여기서 곧바로 트리를 만지지 않는다 — 탭을 갈았으면(@key) 메뉴 트리는 다음 렌더에 새로 지어지고 그 전의 트리는 즐겨찾기 트리거나 null 이다.</summary>
    private string? _revealPath;

    /// <summary>이번 폄에서 이미 짚어 준 활성 메뉴의 경로. ShowActive 가 쓰고, 사이드바가 접히면 지운다.</summary>
    private string? _activeShown;

    /// <summary>이동이 끝난 뒤 한 번 더 펴 줘야 하는 자리. 도착할 주소와 펼 메뉴의 경로 한 쌍이다. 링크를 누르면 펴기와 이동이 겹쳐 펴 놓은 것이 도착 뒤에 남지 않으므로 이동이 끝난 자리에서 한 번 더 편다. 아무 이동에나 다시 펴지는 않고 도착 주소가 그 메뉴의 것일 때만 한다 — 아니면 옛 가지가 저 혼자 펴진다.</summary>
    private (string Target, string Path)? _reapply;

    /// <summary>좁혀 둔 즐겨찾기 트리. 렌더마다 다시 좁히면 안 된다 — DxTreeView 는 Data 가 다른 것으로 바뀌면 펼침을 버린다. 즐겨찾기나 메뉴가 바뀔 때만 다시 좁힌다.</summary>
    private IReadOnlyList<MenuNode> _favorites = [];

    /// <summary>담아 두었지만 지금 메뉴에 없어 트리에 못 그린 개수.</summary>
    private int _hiddenFavorites;

    /// <summary>마지막으로 좁힐 때 쓴 메뉴 목록. 참조를 그대로 든다 — 화면을 옮길 때마다 같은 값이 다시 들어와도 좁히면 트리가 새로 지어져 펼침이 풀린다.</summary>
    private IReadOnlyList<MenuNode>? _builtFrom;

    private void RebuildFavorites()
    {
        _builtFrom = Nodes;
        _favorites = FavoriteTree.Prune(Nodes, Favorites.Contains);
        _hiddenFavorites =
            Favorites.Items.Count - FavoriteTree.CountScreens(_favorites, Favorites.Contains);
    }

    private void Select(Tab tab)
    {
        _tab = tab;
        _expandFavorites = tab == Tab.Favorites;

        // 즐겨찾기를 보는 동안에는 활성 메뉴를 짚지 않는다. 메뉴 탭으로 돌아왔으면 그 트리는 방금 새로 지어져 접혀 있으므로 여기서 한 번 다시 짚는다.
        if (tab == Tab.Menu)
        {
            _activeShown = null;
            ShowActive();
        }
    }

    private async Task OpenTabMenuAsync(MouseEventArgs args)
    {
        if (_tabMenu is not null)
        {
            await _tabMenu.ShowAsync(args);
        }
    }

    private async Task RefreshMenusAsync()
    {
        if (Refresh is null || _refreshing)
        {
            return;
        }

        _refreshing = true;

        try
        {
            var fresh = await Refresh();

            // 메뉴가 바뀌면 지금 화면의 가지가 접힌 새 트리가 서므로 다시 짚는다.
            _activeShown = null;
            ShowActive();

            Toasts.Show(fresh ? "메뉴를 새로 가져왔습니다." : "메뉴를 새로 가져오지 못했습니다.",
                fresh ? NoticeTone.Info : NoticeTone.Warning);
        }
        finally
        {
            _refreshing = false;
            StateHasChanged();
        }
    }

    /// <summary>사이드바가 펴질 때마다 지금 화면의 메뉴를 펴고 골라 준다. 휴대폰에서 메뉴를 누르면 사이드바가 접히고 다시 열면 트리가 접힌 채로 나온다 — 펼침은 DxTreeView 안에만 있고 이동할 때 트리가 다시 지어지기 때문이다. 그대로 두면 지금 어느 화면인지 사이드바로 알 수 없다. 한 번만 한다(_activeShown) — 렌더마다 하면 사용자가 접어 둔 가지가 되살아난다. 브레드크럼의 부탁이 먼저다(??=) — 눌러서 펴진 사이드바에 이쪽이 덮어쓰면 엉뚱한 가지가 펴진다.</summary>
    private void ShowActive()
    {
        if (!IsOpen)
        {
            // 접혔다. 표시만 지운다 — 다음에 펼 때 다시 짚는다.
            _activeShown = null;
            return;
        }

        if (_tab != Tab.Menu || ActivePath is not { Length: > 0 } path || _activeShown == path)
        {
            return;
        }

        _activeShown = path;
        _revealPath ??= path;
    }

    /// <summary>즐겨찾기 트리는 펼쳐서 연다. 접힌 채로 열면 담은 화면 하나를 누르는 데 묶음을 두 번 펴야 해서 담을 이유가 없다. 메뉴 탭은 179개라 다 펴면 오히려 못 찾으므로 접힌 채 둔다. 한 번만 편다 — 렌더마다 펴면 접어 둔 묶음이 되살아난다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_expandFavorites && _tree is not null)
        {
            _expandFavorites = false;
            _tree.ExpandAll();
        }

        await RevealAsync();
    }

    /// <summary>부탁받은 메뉴까지 펴고 고르고 보이는 자리로 굴린다. 트리가 없으면 부탁을 버리지 않고 다음 렌더로 미룬다 — 접힌 사이드바는 판이 아직 안 그려져 있을 수 있어 거기서 버리면 첫 부탁이 조용히 사라진다. 못 찾는 경로는 지나간다 — 권한 때문에 트리에 없는 화면은 없는 메뉴를 만들어 보여 주지 않는다.</summary>
    private async Task RevealAsync()
    {
        if (_revealPath is not { Length: > 0 } path || _tab != Tab.Menu || _tree is null)
        {
            return;
        }

        _revealPath = null;

        bool Match(ITreeViewNodeInfo node) =>
            (node.DataItem as MenuNode)?.Path == path;

        if (_tree.GetNodeInfo(Match) is null)
        {
            return;
        }

        _tree.ExpandToNode(Match);

        // 묶음을 누른 뜻은 그 안을 보겠다는 것이다. 잎이면 아무 일도 없다.
        _tree.SetNodeExpanded(Match, true);
        _tree.SelectNode(Match);

        // 링크를 누른 것이면 이동이 뒤따라 온다. 그 이동이 펼침을 푸므로
        // 도착한 뒤에 한 번 더 편다(_reapply 머리말).
        var target = (_tree.GetNodeInfo(Match)?.DataItem as MenuNode)?.NavigateUrl;

        _reapply = target is { Length: > 0 } && !IsCurrent(target)
            ? (target, path)
            : null;

        try
        {
            await Js.InvokeVoidAsync("jsiniSidebar.reveal");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException
                                      or ObjectDisposedException or TaskCanceledException)
        {
            // 회로가 이미 끊겼거나 브라우저가 안 받는다. 펴고 고르는 것은
            // 이미 끝났으므로 **굴려 주지 못했을 뿐이다** — 다시 시도할 일이 아니다.
        }
    }

    protected override void OnInitialized()
    {
        Favorites.Changed += OnFavoritesChanged;
        Reveal.Requested += OnRevealRequested;
        Navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>이 주소가 지금 보고 있는 화면인가.</summary>
    private bool IsCurrent(string href) =>
        string.Equals(Relative(), Trim(href), StringComparison.OrdinalIgnoreCase);

    /// <summary>브라우저에 떠 있는 경로. 질의 문자열과 조각은 뗀다.</summary>
    private string Relative() =>
        Trim("/" + Navigation.ToBaseRelativePath(Navigation.Uri));

    private static string Trim(string href)
    {
        var cut = href.IndexOfAny(['?', '#']);
        var path = cut >= 0 ? href[..cut] : href;

        return "/" + path.Trim('/');
    }

    /// <summary>링크 칸을 눌러 이동이 끝났다. 그 메뉴에 도착했을 때만 한 번 더 편다.</summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_reapply is not { } pending)
        {
            return;
        }

        // 한 번만 본다. 다른 데로 갔으면 그대로 잊는다.
        _reapply = null;

        if (!IsCurrent(pending.Target))
        {
            return;
        }

        _revealPath = pending.Path;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>브레드크럼이 이 메뉴를 보여 달라고 했다. 여기서는 표시만 해 두고 펴는 것은 다음 렌더 뒤에 한다(_revealPath).</summary>
    private void OnRevealRequested(string path) => InvokeAsync(() =>
    {
        // 즐겨찾기를 보고 있었으면 돌아온다. 담아 두지 않은 메뉴는 그쪽
        // 트리에 아예 없어서, 그대로 두면 아무 일도 안 일어난 것처럼 보인다.
        _tab = Tab.Menu;

        // 검색어가 걸려 있으면 그 가지가 걸러져 있을 수 있어 지우고 편다 — 안 지우면 눌렀는데 안 열린다.
        _search = string.Empty;

        _revealPath = path;
        StateHasChanged();
    });

    /// <summary>메뉴가 실제로 바뀌었을 때만(권한 · 화면 크기) 다시 좁힌다. 같은 목록이 다시 들어오는 것은 화면을 옮길 때마다 일어난다.</summary>
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_builtFrom, Nodes))
        {
            RebuildFavorites();
        }

        // 사이드바가 방금 펴졌거나 화면이 바뀌었으면 지금 메뉴를 짚어 준다.
        ShowActive();
    }

    /// <summary>헤더의 별표로 즐겨찾기가 바뀌었다. 즐겨찾기 탭을 보고 있었다면 트리가 통째로 다시 지어지므로 펼침도 다시 해야 한다 — 아니면 방금 담은 화면이 접힌 묶음 안에 들어가 보이지 않는다.</summary>
    private void OnFavoritesChanged() => InvokeAsync(() =>
    {
        RebuildFavorites();
        _expandFavorites = _tab == Tab.Favorites;
        StateHasChanged();
    });

    public void Dispose()
    {
        Favorites.Changed -= OnFavoritesChanged;
        Reveal.Requested -= OnRevealRequested;
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
