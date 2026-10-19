using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using System.Text.Json;

namespace JSini.Web.Components.Layout;

public partial class TabBar
{
    [Inject] private PortalTabs Tabs { get; set; } = default!;
    [Inject] private TabMenuRequest TabMenu { get; set; } = default!;
    [Inject] private MenuFavorites Favorites { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>아래 띠에 넣을 때 화면 열쇠와 이름을 여기서 꺼낸다. 탭은 주소와 이름만 들고 있어 그것만으로 넣으면 경로가 옮겨 갔을 때 띠 칸이 준비 중으로 떨어진다.</summary>
    [Inject] private IMenuProvider Menus { get; set; } = default!;

    private DxContextMenu? _menu;

    /// <summary>열린 화면 목록 창. 넘칠 때만 그려진다(<c>_over</c>).</summary>
    private DxContextMenu? _allMenu;

    /// <summary>다섯이 다 찼을 때 무엇과 바꿀지 묻는 창.</summary>
    private BottomNavSwapDialog? _swap;

    /// <summary>오른쪽 클릭한 탭. 창이 열려 있는 동안만 값이 있다.</summary>
    private PortalTab? _target;

    /// <summary>적어 둔 고정 탭을 이미 되살렸는가. 회로마다 한 번만 읽는다.</summary>
    private bool _restored;

    /// <summary>휴대폰 아래 띠에 지금 선 칸들. 읽기 전에는 기본 다섯이고 MobileBottomNav 가 그리는 값과 같은 자리에서 읽는다.</summary>
    private List<BottomNavItem> _navItems = [.. BottomNav.Defaults];

    /// <summary>띠를 <b>쓰지 않기로</b> 했는가. 그러면 넣고 빼는 항목을 안 그린다.</summary>
    private bool _navHidden;

    /// <summary>고정 탭을 적어 두는 자리(이 기기에 남는다). 정본은 PortalBoot.PinnedTabsKey 다 — 읽기는 그쪽 한 왕복에서 하고 여기서는 쓰고 지우는 데만 쓴다.</summary>
    private const string PinnedKey = PortalBoot.PinnedTabsKey;

    /* 넘치는 탭 줄은 가로로 구른다(app.css). 넘쳤는지와 굴러간 자리는 브라우저만 안다 — tab-scroll.js 가 재어 OnOverflowChanged 로 되알린다. 안 넘치면 화살표와 열린 화면 목록을 그리지 않는다. */

    /// <summary>탭 줄의 굴림 상자. JS 가 이 요소를 재고 민다.</summary>
    private ElementReference _list;

    private IJSObjectReference? _scroll;
    private DotNetObjectReference<TabBar>? _self;

    /// <summary>JS 를 이 요소에 붙였는가. 탭이 없으면 요소 자체가 없다.</summary>
    private bool _attached;

    /// <summary>탭이 칸을 넘치는가. 양끝 화살표와 목록 단추가 이 값으로 선다.</summary>
    private bool _over;

    /// <summary>그쪽으로 더 갈 데가 있는가. 끝에 닿으면 화살표를 꺼 둔다.</summary>
    private bool _canLeft;

    private bool _canRight;

    /// <summary>마지막으로 보이는 자리까지 끌어온 탭의 주소. 켜진 주소가 이것과 달라졌을 때만 끌어온다 — 그릴 때마다 부르면 사람이 굴려 둔 자리를 되돌린다.</summary>
    private string? _revealed;

    protected override void OnInitialized()
    {
        Tabs.Changed += OnTabsChanged;
        Tabs.PinsChanged += OnPinsChanged;
        Favorites.Changed += OnTabsChanged;

        // 휴대폰에서 헤더의 화면 이름을 누른 것. 이 줄은 감춰져 있지만 창은 이 부품이 들고 있다(TabMenuRequest 머리말).
        TabMenu.Requested += OnTabMenuRequested;

        // 띠를 환경설정에서 고쳐도 같은 회로라 바로 들어온다. 안 들으면 창의 넣기·빼기가 옛 다섯을 보고 이미 넣은 화면에 또 넣기를 띄운다.
        Boot.BottomNavItemsChanged += OnNavItemsChanged;
        Boot.BottomNavHiddenChanged += OnNavHiddenChanged;

        // 아이콘과 열쇠를 메뉴에서 찾으므로 메뉴가 늦게 오면 다시 그린다.
        Menus.MenusChanged += OnTabsChanged;
    }

    private void OnTabsChanged() => InvokeAsync(StateHasChanged);

    /// <summary>오른쪽 클릭한 자리에 창을 연다. 대상을 먼저 세우고 한 번 다시 그린 뒤 연다 — 안 그리면 창이 이전 대상의 항목(고정·담기 여부)을 그린 채 떠서 누르면 다른 탭이 닫힌다.</summary>
    private async Task OpenMenuAsync(MouseEventArgs args, PortalTab tab)
    {
        _target = tab;
        StateHasChanged();

        if (_menu is not null)
        {
            await _menu.ShowAsync(args);
        }
    }

    /// <summary>오른쪽 끝 단추로 여는 길. 대상이 보고 있는 탭이라는 것만 다르고 항목은 오른쪽 클릭과 같다.</summary>
    private async Task OpenMenuForActiveAsync()
    {
        _target = ActiveTab;
        StateHasChanged();

        if (_menu is not null && _target is not null)
        {
            await _menu.ShowAsync(ContextMenuPosition.Bottom);
        }
    }

    /// <summary>휴대폰에서 헤더의 화면 이름을 눌렀다(Breadcrumb). 대상은 보고 있는 탭이고 창만 누른 자리에 뜬다 — 「⋯」 자리는 휴대폰에서 줄째 감춰져 있어 창이 엉뚱한 구석에 뜬다.</summary>
    private void OnTabMenuRequested(MouseEventArgs args) => InvokeAsync(async () =>
    {
        if (ActiveTab is { } tab)
        {
            await OpenMenuAsync(args, tab);
        }
    });

    /// <summary>보고 있는 탭. 못 찾으면 첫 탭이고, 탭이 없으면 <c>null</c> 이다.</summary>
    private PortalTab? ActiveTab => Tabs.ActiveTab;

    /* ── 양끝 화살표와 「열린 화면」 목록 ────────────────────────── */

    /// <summary>브라우저가 재어 보낸 것. 바뀔 때만 올라온다(tab-scroll.js).</summary>
    /// <param name="over">탭이 칸을 넘치는가.</param>
    /// <param name="left">왼쪽으로 더 갈 데가 있는가.</param>
    /// <param name="right">오른쪽으로 더 갈 데가 있는가.</param>
    [JSInvokable]
    public void OnOverflowChanged(bool over, bool left, bool right)
    {
        _over = over;
        _canLeft = left;
        _canRight = right;

        InvokeAsync(StateHasChanged);
    }

    /// <summary>탭 줄을 한 칸 민다(dir 이 -1 이면 앞쪽). 민 뒤의 끝 상태는 되묻지 않는다 — 굴림이 끝나면 브라우저가 scroll 로 알려 주고, 여기서 재면 미끄러지는 중간값을 읽어 끝에 닿지 않았는데 단추가 꺼진다.</summary>
    private async Task StepAsync(int dir)
    {
        if (_scroll is null)
        {
            return;
        }

        try
        {
            await _scroll.InvokeVoidAsync("step", _list, dir);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
            // 회로가 닫히는 중이다. 굴리지 못한 것이 전부다.
        }
    }

    /// <summary>열린 화면을 모두 적은 목록을 연다.</summary>
    private async Task OpenAllMenuAsync()
    {
        if (_allMenu is not null)
        {
            await _allMenu.ShowAsync(ContextMenuPosition.Bottom);
        }
    }

    /// <summary>목록 한 줄의 그림. 보고 있는 탭은 체크, 고정한 탭은 압정, 나머지는 빈 자리다. 빈 자리를 채우는 것은 줄을 맞추기 위해서다 — 비우면 그 줄만 글자가 왼쪽으로 붙는다.</summary>
    private string AllMenuIcon(PortalTab tab) =>
        IsActive(tab) ? "jsini-icon-check"
        : tab.Pinned ? "jsini-icon-pin"
        : "jsini-icon-blank";

    /// <summary>목록에서 고른 탭으로 간다. 탭이 라우팅을 하지 않으므로 주소로 옮기고 결과를 보고 탭이 켜진다. 보이는 자리까지 끌어오는 일도 따라온다.</summary>
    private void Go(PortalTab tab)
    {
        if (!IsActive(tab))
        {
            Navigation.NavigateTo(tab.Href);
        }
    }

    private bool IsActive(PortalTab tab) =>
        string.Equals(tab.Href, Tabs.ActiveHref, StringComparison.OrdinalIgnoreCase);

    /// <summary>이 탭의 DB 메뉴 경로. 즐겨찾기는 이 값으로 담긴다 — 탭이 든 링크 주소(/funeral/room-status)와 DB 경로(/room_status)를 섞으면 이미 담은 것이 안 담긴 것으로 보인다(MenuFavorites 참고).</summary>
    private static string MenuPathOf(PortalTab tab) => RouteAliases.ToMenuPath(tab.Href);

    private bool IsFavorite(PortalTab tab) => Favorites.Contains(MenuPathOf(tab));

    private async Task ToggleFavoriteAsync(PortalTab tab)
    {
        if (MenuPathOf(tab) is { Length: > 0 } path)
        {
            await Favorites.ToggleAsync(path);
        }
    }

    /// <summary>같은 주소로 다시 이동해 화면만 새로 만든다. forceLoad 를 켜지 않는다 — 켜면 문서를 새로 받아 회로가 끊기고 탭이 사라진다.</summary>
    private void Refresh(PortalTab tab) =>
        Navigation.NavigateTo(tab.Href, forceLoad: false, replace: true);

    private Task OpenInNewWindowAsync(PortalTab tab) =>
        Js.InvokeVoidAsync("open", tab.Href, "_blank", "noopener").AsTask();

    /// <summary>이 탭의 전체 주소를 클립보드에 담는다. 남에게 보내거나 다른 기기에서 열려고 쓰므로 PathAndQuery 가 아니라 전체 주소를 담는다 — 경로만 붙이면 받은 쪽이 못 연다. 담았는지 말해 준다 — 복사는 눈에 보이는 결과가 없어 실패하면 주소를 토스트에 적어 손으로 집게 한다.</summary>
    private async Task CopyUrlAsync(PortalTab tab)
    {
        var url = Navigation.ToAbsoluteUri(tab.Href).ToString();

        bool copied;

        try
        {
            copied = await Js.InvokeAsync<bool>("jsiniClipboard.copy", url);
        }
        catch (JSException)
        {
            // theme.js 가 안 실렸거나 브라우저가 거절했다.
            copied = false;
        }

        Toasts.Show(
            copied ? "주소를 복사했습니다." : $"주소를 복사하지 못했습니다 — {url}",
            copied ? NoticeTone.Info : NoticeTone.Error);
    }

    /// <summary>도메인을 뗀 경로(PathAndQuery)를 클립보드에 담는다.</summary>
    private async Task CopyPathAsync(PortalTab tab)
    {
        var uri = Navigation.ToAbsoluteUri(tab.Href);
        var path = uri.PathAndQuery;

        bool copied;

        try
        {
            copied = await Js.InvokeAsync<bool>("jsiniClipboard.copy", path);
        }
        catch (JSException)
        {
            copied = false;
        }

        Toasts.Show(
            copied ? "경로를 복사했습니다." : $"경로를 복사하지 못했습니다 — {path}",
            copied ? NoticeTone.Info : NoticeTone.Error);
    }

    /* 휴대폰 아래 띠에 넣고 뺀다. 칸 수는 MinItems~MaxItems 에서 정하고, 다 찼으면 무엇과 바꿀지 묻고 고른 자리에 그대로 끼운다(BottomNavSwapDialog). 바닥에서는 빼지 않는다. 고친 것은 PortalBoot 가 브라우저에 적고 띠는 같은 회로에서 그 알림을 듣는다. */

    private void OnNavItemsChanged(string? json)
    {
        _navItems = [.. BottomNav.Parse(json)];
        InvokeAsync(StateHasChanged);
    }

    private void OnNavHiddenChanged(bool hidden)
    {
        _navHidden = hidden;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>이 탭이 가리키는 메뉴. 없으면 null — 메뉴에 없는 화면이다.</summary>
    private MenuNode? MenuOf(PortalTab tab) =>
        BottomNav.Find(new BottomNavItem { Path = tab.Href }, Menus.AllMenus);

    /// <summary>이 탭이 띠에 이미 서 있으면 그 자리, 없으면 <c>-1</c>.</summary>
    private int NavIndexOf(PortalTab tab) =>
        BottomNav.IndexOf(_navItems, tab.Href, MenuOf(tab)?.RouteKey);

    private bool InBottomNav(PortalTab tab) => NavIndexOf(tab) >= 0;

    /// <summary>이 탭을 띠에 넣거나 뺀다. 아이콘은 담지 않고 메뉴에서 찾는다 — 관리자가 메뉴 아이콘을 바꾸면 띠도 따라간다. 주소는 탭 주소의 쿼리를 떼고 메뉴의 링크 주소로 적는다 — 쿼리가 붙으면 띠 칸이 그때 보던 조건에 묶인다. 메뉴에 없는 화면이면 탭 주소 그대로다.</summary>
    private async Task ToggleBottomNavAsync(PortalTab tab)
    {
        if (_navHidden)
        {
            return;
        }

        var at = NavIndexOf(tab);

        if (at >= 0)
        {
            // 바닥에서는 빼지 않는다(BottomNav.MinItems). 환경설정 판은 단추를 잠그지만 이쪽은 창을 열어야 보이므로 말로 돌려보낸다 — 조용히 넘기면 눌러도 아무 일이 없다.
            if (_navItems.Count <= BottomNav.MinItems)
            {
                Toasts.Show(
                    $"아래 띠는 칸이 {BottomNav.MinItems}개보다 적어질 수 없습니다. "
                    + "띠를 쓰지 않으려면 환경설정에서 끕니다.",
                    NoticeTone.Warning);
                return;
            }

            var dropped = _navItems[at].Title;

            _navItems.RemoveAt(at);

            await SaveNavAsync();
            Toasts.Show($"「{dropped}」을(를) 아래 띠에서 뺐습니다.");
            return;
        }

        var node = MenuOf(tab);

        var item = new BottomNavItem
        {
            Path = node?.LinkTarget ?? tab.Href,
            RouteKey = node?.RouteKey,
            Title = node?.Title ?? tab.Title,
        };

        if (_navItems.Count < BottomNav.MaxItems)
        {
            _navItems.Add(item);
            await SaveNavAsync();
            Toasts.Show($"「{item.Title}」을(를) 아래 띠에 넣었습니다.");
            return;
        }

        if (_swap is null)
        {
            return;
        }

        // 칸이 다 찼다. 아무거나 밀어내지 않고 어느 칸을 내줄지 묻는다 — 뒤에 붙이면 건드리지 않은 칸의 자리가 바뀐다.
        var slot = await _swap.AskAsync(_navItems, item.Title);

        // 그만두었거나, 묻는 동안 환경설정에서 칸이 줄었다.
        if (slot < 0 || slot >= _navItems.Count)
        {
            return;
        }

        var replaced = _navItems[slot].Title;

        _navItems[slot] = item;
        await SaveNavAsync();

        Toasts.Show($"「{replaced}」 자리에 「{item.Title}」을(를) 놓았습니다.");
    }

    private Task SaveNavAsync() => Boot.SetBottomNavItemsAsync(BottomNav.Serialize(_navItems));

    /// <summary>그쪽에 닫을 것이 하나라도 있는가. 없으면 항목을 꺼 둔다.</summary>
    private bool HasClosableSide(PortalTab tab, bool left)
    {
        var pivot = Tabs.Items.ToList().FindIndex(t =>
            string.Equals(t.Href, tab.Href, StringComparison.OrdinalIgnoreCase));

        if (pivot < 0)
        {
            return false;
        }

        return Tabs.Items
            .Select((t, i) => (t, i))
            .Any(x => !x.t.Pinned && (left ? x.i < pivot : x.i > pivot));
    }

    private void Close(string href) => GoAfter(Tabs.Close(href));

    private void CloseAll() => Navigation.NavigateTo(Tabs.CloseAll());

    /* 고정 탭은 브라우저에 적어 둔다. 탭 목록은 회로에 살아 F5 로 사라지므로 고정은 여기서 되살린다. 서버가 아니라 브라우저에 두는 까닭은 고정이 이 기기에서 지금 하는 일에 대한 표시이기 때문이다(즐겨찾기는 이미 서버에 있다). 프리렌더 중에는 JS 를 부를 수 없어 첫 렌더 뒤에 읽는다. */

    /// <summary>그릴 때마다 굴림 쪽을 먼저 맞춘다. 되살리기는 첫 그림에 한 번뿐이다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await SyncScrollAsync();
        await RestoreAsync(firstRender);
    }

    /// <summary>탭 줄에 JS 를 붙이고, 켜진 탭이 가려져 있으면 끌어온다. 붙이는 일을 firstRender 에 걸지 않는다 — 탭이 없으면 줄이 아예 안 그려져 첫 그림에는 붙일 요소가 없다. 회로가 닫히는 중이면 조용히 넘어간다.</summary>
    private async Task SyncScrollAsync()
    {
        try
        {
            if (Tabs.Items.Count == 0)
            {
                // 줄이 통째로 사라졌다. 붙여 둔 것은 JS 쪽이 다음에 붙을 때 스스로 걷는다.
                _attached = false;
                _revealed = null;
                return;
            }

            _scroll ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/tab-scroll.js");

            if (!_attached)
            {
                _self ??= DotNetObjectReference.Create(this);

                await _scroll.InvokeVoidAsync("attach", _list, _self);
                _attached = true;
            }

            // 켜진 탭이 바뀌었을 때만 끌어온다 — 그릴 때마다 부르면 사람이 굴려 둔 자리를 되돌린다.
            if (!string.Equals(_revealed, Tabs.ActiveHref, StringComparison.OrdinalIgnoreCase))
            {
                // 첫 번째는 미끄러뜨리지 않는다 — 화면이 뜨자마자 탭 줄이 혼자 흘러가면 눈이 움직임을 쫓지 못한다.
                var smooth = _revealed is not null;

                _revealed = Tabs.ActiveHref;

                await _scroll.InvokeVoidAsync("reveal", _list, smooth);
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
            // 최악이라야 화살표가 안 서고 켜진 탭이 가려진 채로 있을 뿐이다.
        }
    }

    /// <summary>브라우저가 들고 있던 것(고정 탭 · 아래 띠)을 되살린다. 회로마다 한 번이다.</summary>
    private async Task RestoreAsync(bool firstRender)
    {
        if (!firstRender || _restored)
        {
            return;
        }

        _restored = true;

        // 저장소를 직접 읽지 않는다 — PortalBoot 가 잠금·공지·테마와 한 왕복으로 읽어 온다.
        var state = await Boot.ReadAsync();

        // 아래 띠의 칸들도 같은 왕복에 얹혀 온다. 우리 때문에 왕복이 늘지 않는다.
        _navHidden = state.BottomNavHidden;
        _navItems = [.. BottomNav.Parse(state.BottomNavItemsJson)];
        StateHasChanged();

        var saved = state.PinnedTabsJson;

        if (string.IsNullOrWhiteSpace(saved))
        {
            return;
        }

        try
        {
            var pinned = JsonSerializer.Deserialize<List<PinnedTab>>(saved);

            if (pinned is { Count: > 0 })
            {
                Tabs.RestorePinned(pinned);
            }
        }
        catch (JsonException)
        {
            // 적어 둔 것이 깨졌으면 지우고 넘어간다 — 고정 몇 개 때문에 포털이 안 열리면 안 된다.
            await Js.InvokeVoidAsync("localStorage.removeItem", PinnedKey);
        }
    }

    private async void OnPinsChanged()
    {
        try
        {
            var pinned = Tabs.PinnedTabs;

            if (pinned.Count == 0)
            {
                await Js.InvokeVoidAsync("localStorage.removeItem", PinnedKey);
                return;
            }

            await Js.InvokeVoidAsync("localStorage.setItem", PinnedKey, JsonSerializer.Serialize(pinned));
        }
        catch (JSException)
        {
            // 저장소를 못 쓰는 브라우저(사생활 보호 모드)가 있다. 고정은 이번 회로 동안 그대로 듣고 다음 새로고침에 풀린다.
        }
    }

    /// <summary>탭을 닫은 결과 옮겨 갈 곳이 생겼으면 옮긴다.</summary>
    private void GoAfter(string? next)
    {
        if (next is not null)
        {
            Navigation.NavigateTo(next);
        }
    }

    public void Dispose()
    {
        // 붙여 둔 관찰자를 여기서 떼지 않는다 — 떠나는 참이라 그 요소를 가리킬 수 없고 JS 를 부를 수 있는 자리도 아니다. 떨어져 나간 요소는 다음에 붙는 쪽이 쓸어 낸다.
        _self?.Dispose();
        _self = null;

        Tabs.Changed -= OnTabsChanged;
        Tabs.PinsChanged -= OnPinsChanged;
        Favorites.Changed -= OnTabsChanged;
        TabMenu.Requested -= OnTabMenuRequested;
        Boot.BottomNavItemsChanged -= OnNavItemsChanged;
        Boot.BottomNavHiddenChanged -= OnNavHiddenChanged;
        Menus.MenusChanged -= OnTabsChanged;
    }
}
