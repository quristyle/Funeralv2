using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
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

    /// <summary>그릴 메뉴들. **이미 걸러진 것**이어야 한다.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<MenuNode> Nodes { get; set; } = [];

    /// <summary>
    /// 지금 보고 있는 화면의 메뉴 경로(<see cref="MenuNode.Path"/>).
    /// 사이드바가 펴질 때마다 이 가지를 펴고 골라 준다(<see cref="ShowActive"/>).
    /// </summary>
    /// <remarks>
    /// 레이아웃이 주소로 찾아 둔 줄기의 끝이다(<c>MainLayout.Track</c>). 여기서
    /// 다시 찾지 않는 이유는 브레드크럼과 <b>같은 줄기를 봐야</b> 하기 때문이다 —
    /// 따로 찾으면 둘이 다른 메뉴를 가리키는 자리가 생긴다.
    /// </remarks>
    [Parameter] public string? ActivePath { get; set; }

    /// <summary>
    /// 사이드바가 펴져 있나. <b>접힘은 레이아웃이 들고 있는 상태다</b> —
    /// 여기서는 「방금 펴졌다」를 알아채는 데만 쓴다.
    /// </summary>
    [Parameter] public bool IsOpen { get; set; } = true;

    private enum Tab { Menu, Favorites }

    private Tab _tab = Tab.Menu;

    /// <summary>메뉴 검색어. 고른 탭의 트리에 그대로 넘어간다.</summary>
    private string _search = string.Empty;

    private DxTreeView? _tree;

    /// <summary>즐겨찾기 트리를 다음 렌더 뒤에 펼쳐야 한다.</summary>
    private bool _expandFavorites;

    /// <summary>
    /// 다음 렌더 뒤에 펴서 골라야 할 메뉴의 경로. 브레드크럼이 넣는다.
    ///
    /// <para>
    /// <b>여기서 곧바로 트리를 만지지 않는다.</b> 탭을 갈아 끼웠으면
    /// (<c>@key</c> 때문에) 메뉴 트리는 <b>다음 렌더에</b> 새로 지어지고,
    /// 그 전의 <see cref="_tree"/> 는 즐겨찾기 트리이거나 <c>null</c> 이다.
    /// </para>
    /// </summary>
    private string? _revealPath;

    /// <summary>
    /// 이번 폄에서 이미 짚어 준 활성 메뉴의 경로. <see cref="ShowActive"/> 가
    /// 쓰고, 사이드바가 접히면 지운다.
    /// </summary>
    private string? _activeShown;

    /// <summary>
    /// 이동이 끝난 뒤 <b>한 번 더</b> 펴 줘야 하는 자리. 「도착할 주소」와
    /// 「펼 메뉴의 경로」 한 쌍이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 브레드크럼의 가운데 칸(자기 화면이 있는 메뉴)은 <b>링크</b>라 누르면
    /// 펴기와 이동이 함께 일어난다. 그 둘이 겹치면 <b>펴 놓은 것이 남지
    /// 않는다</b> — 재어 보니 도착한 뒤의 트리는 접힌 채였다. (골라진 것처럼
    /// 보이기는 하는데, 그건 우리가 고른 것이 아니라 DevExpress 가 주소에
    /// 맞는 줄을 스스로 짚어 준 것이다.)
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>이동이 끝난 자리에서 한 번 더</b> 편다. 그 한 번은 남는다 —
    /// 사이드바에서 메뉴를 눌러 옮겨 다닐 때 펴 둔 가지가 그대로 있는 것과
    /// 같은 이치다.
    /// </para>
    ///
    /// <para>
    /// 그래서 도착한 뒤에 한 번 더 편다. <b>아무 이동에나 다시 펴지 않는다</b> —
    /// 도착 주소가 그 메뉴의 것일 때만이다. 안 그러면 사이드바로 다른 화면에
    /// 간 다음에도 옛 가지가 저 혼자 펴지고 골라진다.
    /// </para>
    /// </remarks>
    private (string Target, string Path)? _reapply;

    /// <summary>
    /// 좁혀 둔 즐겨찾기 트리. <b>렌더할 때마다 다시 좁히면 안 된다.</b>
    ///
    /// 좁히기는 매번 새 목록을 만드는데, DxTreeView 는 <c>Data</c> 가 다른
    /// 것으로 바뀌면 트리를 새로 짓고 <b>펼침 상태를 버린다</b>. 그래서 렌더
    /// 안에서 좁히면 펼쳐 두어도 다음 렌더에 도로 접힌다 — 실제로 밟았다.
    /// 즐겨찾기나 메뉴가 바뀔 때만 다시 좁힌다.
    /// </summary>
    private IReadOnlyList<MenuNode> _favorites = [];

    /// <summary>담아 두었지만 지금 메뉴에 없어 트리에 못 그린 개수.</summary>
    private int _hiddenFavorites;

    /// <summary>
    /// 마지막으로 좁힐 때 쓴 메뉴 목록. <b>참조를 그대로 들고 있는다.</b>
    ///
    /// 화면을 옮길 때마다 레이아웃이 다시 그려지면서 <see cref="Nodes"/> 가
    /// 같은 값으로 다시 들어온다. 그때마다 좁히면 매번 새 목록이 되고, 그러면
    /// 트리가 새로 지어지면서 펼침이 풀린다(<see cref="_favorites"/> 주석 참고).
    /// </summary>
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

        // 즐겨찾기를 보는 동안에는 활성 메뉴를 짚지 않는다(ShowActive).
        // 메뉴 탭으로 돌아왔으면 그 트리는 방금 새로 지어진 것이라 접혀 있다 —
        // 여기서 한 번 다시 짚어 준다.
        if (tab == Tab.Menu)
        {
            _activeShown = null;
            ShowActive();
        }
    }

    /// <summary>
    /// <b>사이드바가 펴질 때마다 지금 화면의 메뉴를 펴고 골라 준다.</b>
    ///
    /// <para>
    /// 휴대폰에서는 메뉴를 누르면 사이드바가 저절로 접히고(<c>MainLayout</c>의
    /// <c>OnLocationChanged</c>), 다시 열면 트리가 <b>접힌 채</b>로 나온다 —
    /// 펼침은 <c>DxTreeView</c> 안에만 있고 그 트리는 이동할 때 다시 지어지기
    /// 때문이다(재어 본 것은 <see cref="_reapply"/> 머리말에 있다). 그러면
    /// 179개짜리 접힌 트리만 남아서 <b>지금 어느 화면에 있는지 사이드바로는
    /// 알 수 없다.</b> 고른 줄이 접힌 묶음 안에 들어 있으니 보이지 않는다.
    /// </para>
    ///
    /// <para>
    /// 그래서 펴질 때 한 번 짚어 준다. 브레드크럼이 하던 일(<see cref="RevealAsync"/>)을
    /// 그대로 쓰므로 <b>펴기·고르기·보이는 자리로 굴리기</b>가 한꺼번에 된다.
    /// </para>
    ///
    /// <para>
    /// <b>한 번만 한다</b>(<see cref="_activeShown"/>). 렌더마다 하면 사용자가
    /// 접어 둔 가지가 다음 렌더에 도로 펴진다 — 즐겨찾기 펼침을 한 번만 하는
    /// 것과 같은 이유다. 접히면 그 표시를 지워, <b>다음에 열 때 다시</b> 짚는다.
    /// </para>
    ///
    /// <para>
    /// <b>브레드크럼의 부탁이 먼저다</b>(<c>??=</c>). 둘은 겹친다 — 브레드크럼을
    /// 누르면 접혀 있던 사이드바를 레이아웃이 펴 주는데, 거기서 이쪽이 덮어쓰면
    /// 「눌렀는데 엉뚱한 가지가 펴진다」가 된다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 즐겨찾기 트리는 <b>펼쳐서 연다.</b>
    ///
    /// 접힌 채로 열면 담아 둔 화면 하나를 누르는 데 묶음을 두 번 펴야 한다.
    /// 매일 여는 화면을 한 번에 누르려고 담는 것이니, 그러면 담을 이유가 없다.
    /// 메뉴 탭은 접힌 채로 둔다 — 그쪽은 179개라 다 펴면 오히려 못 찾는다.
    ///
    /// 한 번만 편다(<see cref="_expandFavorites"/> 를 내린다). 렌더마다 펴면
    /// 사용자가 접어 둔 묶음이 다음 렌더에 다시 펴진다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_expandFavorites && _tree is not null)
        {
            _expandFavorites = false;
            _tree.ExpandAll();
        }

        await RevealAsync();
    }

    /// <summary>
    /// 부탁받은 메뉴까지 펴고 고르고 보이는 자리로 굴린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>트리가 없으면 부탁을 버리지 않고 다음 렌더로 미룬다.</b> 사이드바가
    /// 접혀 있었으면 그 판이 아직 안 그려져 있을 수 있는데(레이아웃이 같은
    /// 부탁을 받아 펴 준다), 거기서 버리면 <b>접힌 채로 누른 첫 번째 부탁만
    /// 조용히 사라진다.</b>
    /// </para>
    ///
    /// <para>
    /// 못 찾는 경로는 그냥 지나간다. 권한 때문에 사이드바에 없는 화면을
    /// 주소로 열면 브레드크럼은 보이지만(원본에서 찾는다) 트리에는 그 가지가
    /// 없다 — 없는 메뉴를 만들어 보여 주지 않는다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 링크 칸을 눌러 이동이 끝났다. <b>그 메뉴에 도착했을 때만</b> 한 번 더 편다.
    /// </summary>
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

    /// <summary>
    /// 브레드크럼이 「이 메뉴를 보여 달라」고 했다. <b>여기서는 표시만 해 둔다</b> —
    /// 실제로 펴는 것은 다음 렌더 뒤다(<see cref="_revealPath"/>).
    /// </summary>
    private void OnRevealRequested(string path) => InvokeAsync(() =>
    {
        // 즐겨찾기를 보고 있었으면 돌아온다. 담아 두지 않은 메뉴는 그쪽
        // 트리에 아예 없어서, 그대로 두면 아무 일도 안 일어난 것처럼 보인다.
        _tab = Tab.Menu;

        // 검색어가 걸려 있으면 그 가지가 걸러져 있을 수 있다. 지우고 편다 —
        // 찾던 것을 마저 보려면 다시 적으면 되지만, 안 지우면 「눌렀는데
        // 안 열린다」가 된다.
        _search = string.Empty;

        _revealPath = path;
        StateHasChanged();
    });

    /// <summary>
    /// 메뉴가 <b>실제로</b> 바뀌었을 때만(권한·화면 크기) 다시 좁힌다.
    /// 같은 목록이 다시 들어오는 것은 화면을 옮길 때마다 일어난다.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_builtFrom, Nodes))
        {
            RebuildFavorites();
        }

        // 사이드바가 방금 펴졌거나 화면이 바뀌었으면 지금 메뉴를 짚어 준다.
        ShowActive();
    }

    /// <summary>
    /// 헤더의 별표로 즐겨찾기가 바뀌었다. 즐겨찾기 탭을 보고 있었다면 트리가
    /// 통째로 다시 지어지므로 펼침도 다시 해야 한다 — 안 하면 방금 담은 화면이
    /// 접힌 묶음 안으로 들어가 보이지 않는다.
    /// </summary>
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
