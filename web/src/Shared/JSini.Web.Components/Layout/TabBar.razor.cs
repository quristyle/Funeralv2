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

    /// <summary>
    /// 아래 띠에 넣을 때 <b>화면 열쇠와 이름</b>을 여기서 꺼낸다. 탭이 들고 있는
    /// 것은 주소와 탭 이름뿐이라, 그것만으로 넣으면 경로가 옮겨 갔을 때 띠의
    /// 칸이 「준비 중」으로 떨어진다(<see cref="BottomNav.Resolve"/>).
    /// </summary>
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

    /// <summary>
    /// 휴대폰 아래 띠에 지금 선 칸들. <b>읽기 전에는 기본 다섯</b>이다 —
    /// <c>MobileBottomNav</c> 가 그리는 것과 같은 값이고, 같은 자리에서
    /// 읽는다(<see cref="PortalBoot.ReadAsync"/> 의 단일 왕복).
    /// </summary>
    private List<BottomNavItem> _navItems = [.. BottomNav.Defaults];

    /// <summary>띠를 <b>쓰지 않기로</b> 했는가. 그러면 넣고 빼는 항목을 안 그린다.</summary>
    private bool _navHidden;

    /// <summary>
    /// 고정 탭을 적어 두는 자리. 브라우저(이 기기)에 남는다.
    ///
    /// <para>
    /// <b>정본은 <see cref="PortalBoot.PinnedTabsKey"/> 다.</b> 읽는 일은 그쪽이
    /// 다른 값들과 함께 한 왕복으로 하고, 여기서는 쓰고 지우는 데만 쓴다.
    /// </para>
    /// </summary>
    private const string PinnedKey = PortalBoot.PinnedTabsKey;

    /* ── 넘치는 탭 줄 ───────────────────────────────────────────

       탭 줄은 줄바꿈 대신 가로로 구른다(`app.css` 의 `.jsini-tabs__list`).
       그래서 칸을 넘기면 뒤에 연 탭이 화면 밖으로 밀려 나가고, 굴림 막대도
       감춰 두어서 **거기에 더 있다는 것조차 안 보였다.**

       넘쳤는지와 지금 어디까지 굴러갔는지는 **브라우저만 안다.** `js/tab-scroll.js`
       가 재어 `OnOverflowChanged` 로 되알려 주고, 그 값으로 양끝 화살표와
       「열린 화면」 목록을 세운다. 안 넘치면 셋 다 안 그린다. */

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

    /// <summary>
    /// 마지막으로 <b>보이는 자리까지 끌어온</b> 탭의 주소. 켜진 주소가 이것과
    /// 달라졌을 때만 끌어온다 — 그리는 때마다 부르면 사람이 손으로 굴려 둔
    /// 자리를 매번 되돌려 놓는다.
    /// </summary>
    private string? _revealed;

    protected override void OnInitialized()
    {
        Tabs.Changed += OnTabsChanged;
        Tabs.PinsChanged += OnPinsChanged;
        Favorites.Changed += OnTabsChanged;

        // 휴대폰에서 헤더의 화면 이름을 누른 것. 이 줄은 그때 감춰져 있지만
        // 창은 이 부품이 들고 있다(TabMenuRequest 머리말).
        TabMenu.Requested += OnTabMenuRequested;

        // 띠를 환경설정에서 고쳐도 **같은 회로**라 바로 들어온다. 안 듣고
        // 있으면 이 창의 「넣기/빼기」가 옛 다섯을 보고 판정해서, 이미 넣어 둔
        // 화면에 또 「넣기」가 뜬다.
        Boot.BottomNavItemsChanged += OnNavItemsChanged;
        Boot.BottomNavHiddenChanged += OnNavHiddenChanged;

        // 아이콘과 열쇠를 메뉴에서 찾으므로 메뉴가 늦게 오면 다시 그린다.
        Menus.MenusChanged += OnTabsChanged;
    }

    private void OnTabsChanged() => InvokeAsync(StateHasChanged);

    /// <summary>
    /// 오른쪽 클릭한 자리에 창을 연다.
    ///
    /// <para>
    /// 대상을 먼저 세우고 <b>한 번 다시 그린 뒤</b> 연다. 안 그러면 창이 이전
    /// 대상의 항목(고정/담기 여부)을 그린 채로 뜬다 — 이름만 틀린 것이 아니라
    /// 누르면 다른 탭이 닫힌다.
    /// </para>
    /// </summary>
    private async Task OpenMenuAsync(MouseEventArgs args, PortalTab tab)
    {
        _target = tab;
        StateHasChanged();

        if (_menu is not null)
        {
            await _menu.ShowAsync(args);
        }
    }

    /// <summary>
    /// 오른쪽 끝 단추로 여는 길. <b>대상이 「보고 있는 탭」이라는 것만 다르고</b>
    /// 항목은 오른쪽 클릭과 똑같다.
    /// </summary>
    private async Task OpenMenuForActiveAsync()
    {
        _target = ActiveTab;
        StateHasChanged();

        if (_menu is not null && _target is not null)
        {
            await _menu.ShowAsync(ContextMenuPosition.Bottom);
        }
    }

    /// <summary>
    /// 휴대폰에서 헤더에 남는 <b>화면 이름</b>을 눌렀다(<c>Breadcrumb</c>).
    /// 대상은 「⋯」 과 같은 **보고 있는 탭**이고, 창만 <b>누른 자리</b>에 뜬다.
    ///
    /// <para>
    /// 「⋯」 처럼 <c>ContextMenuPosition.Bottom</c> 으로 열지 않는다 — 그 자리는
    /// 이 줄 오른쪽 끝의 단추(<c>#jsini-tabs-more</c>)인데 휴대폰에서는 줄째
    /// 감춰져 있어서(<c>display: none</c>) 창이 엉뚱한 구석에 뜬다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 브라우저가 재어 보낸 것. <b>바뀔 때만</b> 올라온다(<c>tab-scroll.js</c>).
    /// </summary>
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

    /// <summary>
    /// 탭 줄을 한 칸 민다. <c>dir</c> 이 -1 이면 앞쪽, 1 이면 뒤쪽이다.
    /// </summary>
    /// <remarks>
    /// 민 뒤의 상태(끝에 닿았나)는 <b>되묻지 않는다</b> — 굴림이 끝나면 브라우저가
    /// <c>scroll</c> 로 알려 주고, 그때 화살표가 꺼진다. 여기서 재면 미끄러지는
    /// 중간값을 읽어 끝에 닿지도 않았는데 단추가 꺼진다.
    /// </remarks>
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

    /// <summary>
    /// 목록 한 줄의 그림. 보고 있는 탭은 체크, 고정한 탭은 압정, 나머지는 빈 자리다.
    ///
    /// <para>
    /// 빈 자리를 <c>jsini-icon-blank</c> 로 채우는 것은 **줄을 맞추기 위해서**다.
    /// 비워 두면 그 줄만 글자가 왼쪽으로 붙어 목록이 들쭉날쭉해진다.
    /// </para>
    /// </summary>
    private string AllMenuIcon(PortalTab tab) =>
        IsActive(tab) ? "jsini-icon-check"
        : tab.Pinned ? "jsini-icon-pin"
        : "jsini-icon-blank";

    /// <summary>
    /// 목록에서 고른 탭으로 간다. <b>탭이 라우팅을 하지 않는다</b>(이 파일 머리말) —
    /// 주소로 옮기고, 옮긴 결과를 보고 탭이 켜진다. 그러면 켜진 탭을 보이는
    /// 자리까지 끌어오는 일(<see cref="SyncScrollAsync"/>)도 그대로 따라온다.
    /// </summary>
    private void Go(PortalTab tab)
    {
        if (!IsActive(tab))
        {
            Navigation.NavigateTo(tab.Href);
        }
    }

    private bool IsActive(PortalTab tab) =>
        string.Equals(tab.Href, Tabs.ActiveHref, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 이 탭의 <b>DB 메뉴 경로</b>. 즐겨찾기는 이 값으로 담긴다.
    ///
    /// <para>
    /// 탭이 들고 있는 것은 링크 주소(<c>/funeral/room-status</c>)인데 즐겨찾기
    /// 표에 쌓인 것은 DB 경로(<c>/room_status</c>)다. 섞으면 이미 담아 둔 것이
    /// 안 담긴 것으로 보인다(<c>MenuFavorites</c> 주석 참고).
    /// </para>
    /// </summary>
    private static string MenuPathOf(PortalTab tab) => RouteAliases.ToMenuPath(tab.Href);

    private bool IsFavorite(PortalTab tab) => Favorites.Contains(MenuPathOf(tab));

    private async Task ToggleFavoriteAsync(PortalTab tab)
    {
        if (MenuPathOf(tab) is { Length: > 0 } path)
        {
            await Favorites.ToggleAsync(path);
        }
    }

    /// <summary>
    /// 같은 주소로 다시 이동시켜 화면만 새로 만든다. <c>forceLoad</c> 를 켜지
    /// 않는다 — 켜면 문서를 새로 받아 회로가 끊기고 탭이 통째로 사라진다.
    /// </summary>
    private void Refresh(PortalTab tab) =>
        Navigation.NavigateTo(tab.Href, forceLoad: false, replace: true);

    private Task OpenInNewWindowAsync(PortalTab tab) =>
        Js.InvokeVoidAsync("open", tab.Href, "_blank", "noopener").AsTask();

    /// <summary>
    /// 이 탭의 주소를 클립보드에 담는다. 남에게 보내거나 다른 기기에서 열려고
    /// 쓰는 기능이라 <b>전체 주소</b>(<c>https://portal.jsini.co.kr/...</c>)로
    /// 담는다 — <c>/funeral/room-status</c> 만 붙여 넣으면 받은 쪽이 못 연다.
    ///
    /// <para>
    /// 담았는지 <b>말해 준다.</b> 복사는 눈에 보이는 결과가 없어서, 조용히
    /// 실패하면 붙여넣을 때에야 안 된 것을 안다. 실패하면 주소를 토스트에
    /// 같이 적어 손으로 집어 갈 수 있게 한다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 도메인을 제외한 경로(PathAndQuery)를 클립보드에 담는다.
    /// </summary>
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

    /* ── 휴대폰 아래 띠에 넣고 빼기 ──────────────────────────────

       칸 수는 사람이 정한다(`BottomNav.MinItems`~`MaxItems`). 다 찼으면
       **무엇과 바꿀지 묻고** 고른 자리에 그대로 끼운다 — 까닭은
       `BottomNavSwapDialog` 머리말. 거꾸로 **바닥에서는 빼지 않는다**(바로 아래).

       고친 것은 `PortalBoot` 가 브라우저에 적고, 띠(`MobileBottomNav`)는
       같은 회로에서 그 알림을 듣고 있어서 **그 자리에서 바뀐다.** */

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

    /// <summary>
    /// 이 탭이 가리키는 메뉴. 없으면 <c>null</c> 이다(메뉴에 없는 화면).
    /// </summary>
    private MenuNode? MenuOf(PortalTab tab) =>
        BottomNav.Find(new BottomNavItem { Path = tab.Href }, Menus.AllMenus);

    /// <summary>이 탭이 띠에 이미 서 있으면 그 자리, 없으면 <c>-1</c>.</summary>
    private int NavIndexOf(PortalTab tab) =>
        BottomNav.IndexOf(_navItems, tab.Href, MenuOf(tab)?.RouteKey);

    private bool InBottomNav(PortalTab tab) => NavIndexOf(tab) >= 0;

    /// <summary>
    /// 이 탭을 띠에 넣거나 뺀다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>아이콘은 담지 않는다</b> — 비워 두면 그릴 때마다 메뉴에서 찾으므로
    /// (<see cref="BottomNav.IconClass"/>) 관리자가 메뉴 아이콘을 바꾸면 띠도
    /// 따라간다. 환경설정에서 넣을 때와 같은 규칙이다.
    /// </para>
    /// <para>
    /// 주소는 <b>메뉴의 링크 주소</b>로 적는다 — 탭의 주소에는 쿼리가 붙어
    /// 있을 수 있고, 그대로 담으면 띠의 칸이 「그때 보던 조건」에 묶인다.
    /// 메뉴에 없는 화면이면 탭의 주소 그대로다.
    /// </para>
    /// </remarks>
    private async Task ToggleBottomNavAsync(PortalTab tab)
    {
        if (_navHidden)
        {
            return;
        }

        var at = NavIndexOf(tab);

        if (at >= 0)
        {
            // **바닥에서는 빼지 않는다**(`BottomNav.MinItems`). 환경설정 판은
            // 그 자리에서 단추를 잠그는데 이쪽 항목은 창을 열어야 보이므로
            // 잠근 낯을 미리 보여 줄 수가 없다 — 그래서 **말로 돌려보낸다.**
            // 조용히 넘기면 「눌렀는데 아무 일도 없다」가 되고, 그 증상은
            // 띠를 쓰는 사람에게 창 자체가 고장난 것으로 읽힌다.
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

        // 칸이 다 찼다. **아무거나 밀어내지 않는다** — 어느 칸을 내줄지 묻고
        // 고른 자리에 그대로 끼운다(뒤에 붙이면 건드리지도 않은 칸의 자리가 바뀐다).
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

    /* ── 고정 탭은 브라우저에 적어 둔다 ──────────────────────────

       탭 목록은 회로 하나(= 브라우저 탭 하나)에 사는 값이라 F5 를 누르면
       회로와 함께 사라진다. 고정은 「이 화면은 늘 열어 둔다」는 뜻이라 그렇게
       사라지면 기능이 없는 것과 같다 — 실제로 「고정해 두고 새로고침하면
       고정이 풀려 있다」로 나타났다.

       [왜 서버가 아니라 브라우저인가]

       고정은 **이 기기에서 지금 하는 일**에 대한 표시다. 사무실에서 고정해
       둔 탭이 집 노트북에서도 열리면 그건 즐겨찾기지 고정이 아니다.
       즐겨찾기는 이미 서버에 있다(`MenuFavorites`).

       [회로가 붙은 뒤에 읽는다]

       프리렌더 중에는 JS 를 부를 수 없다. 그래서 첫 렌더 뒤에 읽고, 읽은
       것을 되살리면 탭 줄이 한 번 더 그려진다.
    */

    /// <summary>
    /// 그릴 때마다 <b>굴림 쪽을 먼저 맞춘다.</b> 되살리기는 첫 그림에 한 번뿐이다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await SyncScrollAsync();
        await RestoreAsync(firstRender);
    }

    /// <summary>
    /// 탭 줄에 JS 를 붙이고, 켜진 탭이 가려져 있으면 끌어온다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 붙이는 일을 <c>firstRender</c> 에 걸지 않는다. 탭이 하나도 없으면
    /// <b>줄 자체가 안 그려져</b>(<c>@if (Tabs.Items.Count &gt; 0)</c>) 첫 그림에는
    /// 붙일 요소가 없다. 요소가 선 그림에서 붙는다.
    /// </para>
    /// <para>
    /// 회로가 닫히는 중이면 조용히 넘어간다. 화살표 셋 때문에 화면을 떠나는 것이
    /// 오류가 되어서는 안 된다(<c>CommGrd.SyncScrollAsync</c> 와 같은 판단).
    /// </para>
    /// </remarks>
    private async Task SyncScrollAsync()
    {
        try
        {
            if (Tabs.Items.Count == 0)
            {
                // 줄이 통째로 사라졌다. 붙여 둔 것은 JS 쪽이 다음에 붙을 때
                // 스스로 걷는다(떨어져 나간 요소를 쓸어 낸다).
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

            // 켜진 탭이 바뀌었을 때만 끌어온다. 그릴 때마다 부르면 사람이 손으로
            // 굴려 둔 자리를 매번 되돌려 놓는다.
            if (!string.Equals(_revealed, Tabs.ActiveHref, StringComparison.OrdinalIgnoreCase))
            {
                // 첫 번째는 미끄러뜨리지 않는다 — 화면이 뜨자마자 탭 줄이
                // 혼자 흘러가면 무엇이 움직인 것인지 눈이 못 쫓는다.
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

    /// <summary>
    /// 브라우저가 들고 있던 것(고정 탭 · 아래 띠)을 되살린다. 회로마다 한 번이다.
    /// </summary>
    private async Task RestoreAsync(bool firstRender)
    {
        if (!firstRender || _restored)
        {
            return;
        }

        _restored = true;

        // 저장소를 직접 읽지 않는다. `PortalBoot` 가 잠금 표시·공지 표시·테마와
        // **한 왕복으로** 읽어 온다 — 이유는 그 클래스 머리말에 있다.
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
            // 적어 둔 것이 깨졌으면 지우고 넘어간다. 고정 몇 개 때문에
            // 포털이 안 열리면 안 된다.
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
            // 저장소를 못 쓰는 브라우저(사생활 보호 모드)가 있다. 고정은 이번
            // 회로 동안 그대로 듣고, 다음 새로고침에 풀릴 뿐이다.
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
        // 붙여 둔 관찰자를 여기서 떼지 않는다 — 떠나는 참이라 그 요소를 가리킬
        // 수 없고, JS 를 부를 수 있는 자리도 아니다(회로가 닫히는 중일 수 있다).
        // 떨어져 나간 요소는 다음에 붙는 쪽이 쓸어 낸다(`tab-scroll.js` 의 attach).
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
