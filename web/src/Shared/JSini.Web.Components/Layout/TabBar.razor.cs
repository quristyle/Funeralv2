using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using DevExpress.Blazor;
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

    private DxContextMenu? _menu;

    /// <summary>오른쪽 클릭한 탭. 창이 열려 있는 동안만 값이 있다.</summary>
    private PortalTab? _target;

    /// <summary>적어 둔 고정 탭을 이미 되살렸는가. 회로마다 한 번만 읽는다.</summary>
    private bool _restored;

    /// <summary>
    /// 고정 탭을 적어 두는 자리. 브라우저(이 기기)에 남는다.
    ///
    /// <para>
    /// <b>정본은 <see cref="PortalBoot.PinnedTabsKey"/> 다.</b> 읽는 일은 그쪽이
    /// 다른 값들과 함께 한 왕복으로 하고, 여기서는 쓰고 지우는 데만 쓴다.
    /// </para>
    /// </summary>
    private const string PinnedKey = PortalBoot.PinnedTabsKey;

    protected override void OnInitialized()
    {
        Tabs.Changed += OnTabsChanged;
        Tabs.PinsChanged += OnPinsChanged;
        Favorites.Changed += OnTabsChanged;

        // 휴대폰에서 헤더의 화면 이름을 누른 것. 이 줄은 그때 감춰져 있지만
        // 창은 이 부품이 들고 있다(TabMenuRequest 머리말).
        TabMenu.Requested += OnTabMenuRequested;
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
    private PortalTab? ActiveTab =>
        Tabs.Items.FirstOrDefault(IsActive) ?? Tabs.Items.FirstOrDefault();

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _restored)
        {
            return;
        }

        _restored = true;

        // 저장소를 직접 읽지 않는다. `PortalBoot` 가 잠금 표시·공지 표시·테마와
        // **한 왕복으로** 읽어 온다 — 이유는 그 클래스 머리말에 있다.
        var saved = (await Boot.ReadAsync()).PinnedTabsJson;

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
        Tabs.Changed -= OnTabsChanged;
        Tabs.PinsChanged -= OnPinsChanged;
        Favorites.Changed -= OnTabsChanged;
        TabMenu.Requested -= OnTabMenuRequested;
    }
}
