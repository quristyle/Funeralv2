using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Menu;
using JSini.Web.Components.Settings;

namespace JSini.Web.Components.Layout;

public partial class HeaderTools
{
    [Inject] private MenuFavorites Favorites { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private QuickAskReveal Ask { get; set; } = default!;
    [Inject] private NoteClient Notes { get; set; } = default!;
    [Inject] private ThemeSize Size { get; set; } = default!;

    /// <summary>
    /// 지금 화면의 <b>DB 메뉴 경로</b>. 즐겨찾기는 이 값으로 담긴다.
    ///
    /// 링크 주소가 아니다 — 즐겨찾기 표에 쌓인 값이 DB 경로라 섞으면 이미
    /// 담아 둔 것이 안 담긴 것으로 보인다(MenuFavorites 주석 참고).
    /// 메뉴에 없는 화면이면 <c>null</c> 이고, 그때는 별 단추가 꺼진다.
    /// </summary>
    [Parameter] public string? MenuPath { get; set; }

    /// <summary>
    /// 「빠른 지시」 화면의 열쇠. <b>주소가 아니라 이것으로 찾는다</b> —
    /// DB 의 <c>path</c> 는 옛 경로가 남아 있을 수 있고 링크는 여기서 나온다
    /// (<c>MenuNode.RouteKey</c> 머리말).
    /// </summary>
    private const string AskRouteKey = "projmng.ai.ask";

    /// <summary><c>route_key</c> 를 아직 안 채운 DB 를 위한 대비책.</summary>
    private const string AskPath = "/projmng/ai/ask";

    /// <summary>
    /// 휴대폰인가. <b>⚡ 가 서랍을 여는지 화면으로 가는지</b>를 이 값이 가른다
    /// (위 머리말). 레이아웃이 내려 준다 — 브레드크럼이 받는 것과 같은 값이다.
    /// </summary>
    [Parameter] public bool IsPhone { get; set; }

    private bool IsFavorite => Favorites.Contains(MenuPath);

    /// <summary>쪽지 쓰기 창이 열려 있나.</summary>
    private bool _writing;

    /// <summary>안 읽은 쪽지 수. 0 이면 숫자를 안 붙인다.</summary>
    private int _unread;

    /// <summary>
    /// ✉ 에 얹는 글. <b>안 읽은 것이 있으면 그 수를 함께 적는다</b> — 작은
    /// 숫자만으로는 그것이 무엇을 세는 값인지 알 수 없다.
    /// </summary>
    private string NoteTitle => _unread > 0
        ? $"쪽지 쓰기 — 안 읽은 쪽지 {_unread}통"
        : "쪽지 쓰기";

    /// <summary>서랍으로 열리나. 휴대폰이거나 알맹이가 없으면 거짓이다.</summary>
    private bool AskDocks => !IsPhone && Ask.CanDock;

    /// <summary>
    /// ⚡ 에 얹는 글. <b>누르면 무엇이 일어나는지를 적는다</b> — 같은 단추가
    /// 자리에 따라 서랍을 열기도 하고 화면을 옮기기도 해서, 글자까지 같으면
    /// 어느 쪽인지 눌러 봐야 안다.
    /// </summary>
    private string AskTitle(MenuNode ask) => AskDocks
        ? $"{ask.Title} — 옆에서 한 줄 보내기"
        : $"{ask.Title} — AI 에게 한 줄 보내기";

    /// <summary>
    /// ⚡ 를 눌렀다. <b>책상이면 서랍, 휴대폰이면 화면</b>이다(위 머리말).
    /// </summary>
    private void OpenAsk(MenuNode ask)
    {
        if (AskDocks)
        {
            Ask.Toggle();
            return;
        }

        Navigation.NavigateTo(ask.LinkTarget);
    }

    /// <summary>
    /// 권한과 화면 크기로 걸러진 목록에서 찾는다. <b>원본(<c>AllMenus</c>)이 아니다</b> —
    /// 그쪽에서 찾으면 볼 권한이 없는 사람에게도 단추가 나온다.
    /// </summary>
    private MenuNode? AskMenu => FindMenu(Menus.VisibleMenus, AskRouteKey, AskPath);

    /// <summary>「AI 작업 요청」 화면의 열쇠. ⚡ 를 못 쓰는 사람의 노란 번개가 간다.</summary>
    private const string RequestRouteKey = "projmng.ai.request";

    /// <summary><c>route_key</c> 를 아직 안 채운 DB 를 위한 대비책.</summary>
    private const string RequestPath = "/projmng/ai/request";

    /// <summary>
    /// 요청 화면 메뉴. <see cref="AskMenu"/> 와 같은 까닭으로 <b>걸러진 목록</b>에서
    /// 찾는다 — 볼 권한이 없는 사람에게는 노란 번개도 안 뜬다.
    /// </summary>
    private MenuNode? RequestMenu => FindMenu(Menus.VisibleMenus, RequestRouteKey, RequestPath);

    /// <summary>
    /// 노란 번개에 얹는 글. <b>흰 번개가 옆에 있으면 둘을 갈라 적는다</b> —
    /// 그림이 같은 번개라 글자까지 같으면 어느 쪽인지 눌러 봐야 안다.
    /// </summary>
    private string RequestTitle(MenuNode request) => AskMenu is null
        ? $"{request.Title} — 빠른 지시 요청 (관리자가 확인해 실행합니다)"
        : $"{request.Title} — 적어 두는 요청 (지금 돌리지 않는다)";

    private static MenuNode? FindMenu(IReadOnlyList<MenuNode> nodes, string routeKey, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.RouteKey, routeKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            if (FindMenu(node.Children, routeKey, path) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    protected override void OnInitialized()
    {
        Favorites.Changed += OnFavoritesChanged;

        // 화면을 옮길 때마다 안 읽은 수를 다시 센다. **쪽지함에서 읽고 나왔는데
        // 숫자가 그대로면 그것이 고장으로 보인다** — 이 부품은 레이아웃에 붙어
        // 있어 화면이 바뀌어도 다시 만들어지지 않으므로, 안 듣고 있으면 숫자가
        // 로그인한 그 순간의 값에 붙박인다.
        Navigation.LocationChanged += OnLocationChanged;

        // 메뉴는 로그인 직후·권한 갱신 때 뒤늦게 실린다. 안 듣고 있으면
        // **처음 그린 뒤로 단추가 영영 안 나타난다.**
        Menus.MenusChanged += OnFavoritesChanged;

        // 서랍을 닫는 길이 셋이다 — ⚡ 다시 누르기 · 서랍의 ✕ · 화면 옮기기.
        // 뒤의 둘은 여기를 거치지 않아서, 안 듣고 있으면 ⚡ 에 얹히는 글만
        // 옛 상태에 남는다.
        Ask.Changed += OnFavoritesChanged;
    }

    private void OnFavoritesChanged() => InvokeAsync(StateHasChanged);

    /// <summary>
    /// 첫 그림 뒤에 한 번 센다.
    /// </summary>
    /// <remarks>
    /// <b>프리렌더에서는 세지 않는다.</b> 거기서 부르면 첫 진입마다 게이트웨이를
    /// 두 번 타고(정적 SSR 한 번, 회로가 붙고 또 한 번) 숫자가 떴다 사라졌다
    /// 한다 — <c>DataPage.CanLoad</c> 가 막는 것과 같은 자리다.
    /// <c>OnAfterRenderAsync</c> 는 회로 안에서만 돈다.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await CountUnreadAsync();
        }
    }

    /// <summary>
    /// 화면을 옮겼다. <b><c>async void</c> 로 두지 않는다</b> — 그러면 여기서
    /// 새는 예외를 아무도 못 잡아 회로가 통째로 끊어진다.
    /// </summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        => _ = InvokeAsync(CountUnreadAsync);

    /// <summary>
    /// 안 읽은 쪽지를 센다. <b>못 세면 조용히 지나간다</b> — 숫자 하나 때문에
    /// 상단 띠에 오류를 띄우지 않는다. 그때는 직전 값이 그대로 남는다.
    /// </summary>
    private async Task CountUnreadAsync()
    {
        try
        {
            var count = (await Notes.GetUnreadCountAsync())?.Unread ?? 0;

            if (count != _unread)
            {
                _unread = count;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (ApiException)
        {
            // 로그인 전이거나 서비스가 잠깐 없다. 다음 이동에서 다시 센다.
        }
        catch (ObjectDisposedException)
        {
            // 회로가 닫히는 중이다(화면을 옮기는 순간 등).
        }
    }

    /// <summary>보내고 난 뒤. 내가 나에게 보낼 수도 있으므로 다시 센다.</summary>
    private Task OnNoteSentAsync(NoteSendResultDto result) => CountUnreadAsync();

    private async Task ToggleFavoriteAsync()
    {
        if (MenuPath is { Length: > 0 } path)
        {
            await Favorites.ToggleAsync(path);
        }
    }

    /// <summary>
    /// 같은 주소로 다시 이동시켜 화면만 새로 만든다.
    ///
    /// <c>forceLoad</c> 를 켜지 않는다. 켜면 문서를 새로 받아 회로가 끊어지고
    /// 메뉴·권한·탭이 전부 다시 만들어진다 — 그건 F5 와 같다.
    /// </summary>
    private void Refresh() =>
        Navigation.NavigateTo(Navigation.Uri, forceLoad: false, replace: true);

    private Task ToggleFullScreenAsync() =>
        Js.InvokeAsync<bool>("jsiniScreen.toggle").AsTask();

    public void Dispose()
    {
        Favorites.Changed -= OnFavoritesChanged;
        Menus.MenusChanged -= OnFavoritesChanged;
        Ask.Changed -= OnFavoritesChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
