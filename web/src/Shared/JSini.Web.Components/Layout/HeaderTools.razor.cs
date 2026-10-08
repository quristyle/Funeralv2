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
    [Inject] private NotificationClient Notifications { get; set; } = default!;
    [Inject] private NotificationDrawer NotificationDrawerHandle { get; set; } = default!;
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

    /// <summary>안 읽은 알림 수. 0 이면 숫자를 안 붙인다.</summary>
    private int _unreadNotifications;

    /// <summary>
    /// 스스로 다시 세는 시계. <b>다른 장비에서 읽은 것이 여기로 넘어오는
    /// 마지막 길</b>이다 — 아무 손짓도 없는 화면에서도 숫자가 따라온다.
    /// </summary>
    /// <remarks>
    /// 1분이다. 안 읽은 수는 <b>틀려도 조용한 값</b>이라 더 촘촘히 물을 까닭이
    /// 없고(급한 소식은 푸시가 이미 울린다), 열어 둔 탭마다 게이트웨이를
    /// 두드리는 일이라 더 성기게 두면 「읽었는데 그대로다」가 길어진다.
    /// </remarks>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private Timer? _poll;

    /// <summary>
    /// 이 탭이 <b>보이고 있나</b>. 안 보이는 동안은 시계가 쉰다 —
    /// 돌아오는 순간 브라우저가 알려 주고 그때 한 번 세면 된다.
    /// </summary>
    private bool _visible = true;

    /// <summary>바깥 사정을 들어다 주는 브라우저 쪽(<c>unread-sync.js</c>).</summary>
    private IJSObjectReference? _syncModule;

    /// <summary>그쪽이 우리를 부를 손잡이. <b>반드시 버린다</b>(회로마다 하나씩 샌다).</summary>
    private DotNetObjectReference<HeaderTools>? _syncRef;

    /// <summary>
    /// ✉ 에 얹는 글. <b>안 읽은 것이 있으면 그 수를 함께 적는다</b> — 작은
    /// 숫자만으로는 그것이 무엇을 세는 값인지 알 수 없다.
    /// </summary>
    private string NoteTitle => _unread > 0
        ? $"쪽지 쓰기 — 안 읽은 쪽지 {_unread}통"
        : "쪽지 쓰기";

    /// <summary>
    /// 🔔 에 얹는 글.
    /// </summary>
    private string NotificationTitle => _unreadNotifications > 0
        ? $"알림 — 안 읽은 알림 {_unreadNotifications}건"
        : "알림";

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
    private MenuNode? AskMenu => MenuLookup.Find(Menus.VisibleMenus, AskRouteKey, AskPath);

    /// <summary>「AI 작업 요청」 화면의 열쇠. ⚡ 를 못 쓰는 사람의 노란 번개가 간다.</summary>
    private const string RequestRouteKey = "projmng.ai.request";

    /// <summary><c>route_key</c> 를 아직 안 채운 DB 를 위한 대비책.</summary>
    private const string RequestPath = "/projmng/ai/request";

    /// <summary>
    /// 요청 화면 메뉴. <see cref="AskMenu"/> 와 같은 까닭으로 <b>걸러진 목록</b>에서
    /// 찾는다 — 볼 권한이 없는 사람에게는 노란 번개도 안 뜬다.
    /// </summary>
    private MenuNode? RequestMenu => MenuLookup.Find(Menus.VisibleMenus, RequestRouteKey, RequestPath);

    /// <summary>
    /// 노란 번개에 얹는 글. <b>흰 번개가 옆에 있으면 둘을 갈라 적는다</b> —
    /// 그림이 같은 번개라 글자까지 같으면 어느 쪽인지 눌러 봐야 안다.
    /// </summary>
    private string RequestTitle(MenuNode request) => AskMenu is null
        ? $"{request.Title} — 빠른 지시 요청 (관리자가 확인해 실행합니다)"
        : $"{request.Title} — 적어 두는 요청 (지금 돌리지 않는다)";

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

        // 알림함 서랍에서 읽음을 찍으면 **종의 빨간 숫자를 다시 세야 한다.**
        // 판이 헤더 바깥으로 나가면서 콜백으로 직접 받던 길이 끊겼다
        // (`NotificationDrawer` 머리말). 안 듣고 있으면 모두 읽음을 눌러도
        // 숫자가 그대로 남아, 읽었는데 안 읽은 것처럼 보인다.
        NotificationDrawerHandle.Read += CountUnreadAsync;
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
            await AttachSyncAsync();
            StartPoll();
        }
    }

    /// <summary>
    /// 바깥 사정을 듣기 시작한다 — <b>탭으로 돌아왔다 · 망이 붙었다 ·
    /// 이 기기에 푸시가 막 도착했다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 셋이 <b>여러 장비를 쓰는 사람의 숫자가 어긋나는 자리</b>를 덮는다.
    /// 휴대폰에서 알림을 읽고 책상 화면으로 돌아오면 그 순간 다시 세고,
    /// 새 알림이 와서 휴대폰이 울리면 열어 둔 화면의 숫자도 함께 오른다.
    /// </para>
    /// <para>
    /// <b>못 걸어도 조용히 지나간다.</b> 서비스워커가 없는 브라우저도 있고
    /// 회로가 닫히는 중일 수도 있다 — 그때는 시계와 화면 이동이 그대로
    /// 남으므로 숫자가 안 맞는 시간이 길어질 뿐 고장은 아니다.
    /// </para>
    /// </remarks>
    private async Task AttachSyncAsync()
    {
        try
        {
            _syncModule ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/unread-sync.js");
            _syncRef ??= DotNetObjectReference.Create(this);

            await _syncModule.InvokeVoidAsync("attachUnreadSync", _syncRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// 시계를 켠다. <b>회로 안에서만 켠다</b> — 프리렌더된 부품은 HTML 을
    /// 만들고 곧 버려지는데 시계는 그것을 모르고 1분 뒤에 깨어난다
    /// (<c>AutoRefreshPage.StartAutoRefresh</c> 와 같은 자리다).
    /// </summary>
    private void StartPoll()
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        _poll?.Dispose();
        _poll = new Timer(_ => _ = PollAsync(), null, PollInterval, PollInterval);
    }

    /// <summary>
    /// 시계가 깨어났다. <b>안 보이는 탭에서는 아무것도 하지 않는다</b> —
    /// 돌아오는 순간 브라우저가 알려 주고 그때 센다.
    /// </summary>
    private async Task PollAsync()
    {
        if (!_visible) return;

        try
        {
            await SyncUnreadAsync();
        }
        catch (Exception)
        {
            // **스스로 도는 일이라 아무에게도 말하지 않는다.** 여기서 새는
            // 예외는 아무도 안 받아(시계가 부른 것이라 기다리는 쪽이 없다)
            // 조용히 사라지는데, 그렇다고 두면 다음 차례까지 무슨 일이
            // 있었는지 알 길이 없다 — 숫자는 직전 값 그대로 두고 다음
            // 차례에 다시 해 본다(<c>AutoRefreshPage.TickAsync</c> 와 같다).
        }
    }

    /// <summary>
    /// <b>지금 당장 다시 센다.</b> 브라우저 쪽(<c>unread-sync.js</c>)과
    /// 시계가 부른다.
    /// </summary>
    /// <remarks>
    /// 숫자만 고치지 않고 <b>펴 둔 서랍의 목록까지 함께 맞춘다</b> — 다른
    /// 장비에서 읽은 알림이 숫자에서는 빠졌는데 눈앞의 카드로는 남아 있으면,
    /// 눌러 봐야 아무 일도 안 일어나는 줄을 사람이 계속 누르게 된다.
    /// </remarks>
    [JSInvokable]
    public async Task SyncUnreadAsync()
    {
        await CountUnreadAsync();
        await NotificationDrawerHandle.NotifyChangedAsync();
    }

    /// <summary>
    /// 이 탭이 보이는지를 브라우저가 알려 온다. <b>값만 갈아 둔다</b> —
    /// 다시 보이게 됐을 때 세는 일은 그쪽이 따로 부른다.
    /// </summary>
    [JSInvokable]
    public Task SetVisibleAsync(bool visible)
    {
        _visible = visible;
        return Task.CompletedTask;
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
            var noteCount = (await Notes.GetUnreadCountAsync())?.Unread ?? 0;
            var notiCount = (await Notifications.GetUnreadCountAsync())?.Unread ?? 0;

            if (noteCount != _unread || notiCount != _unreadNotifications)
            {
                _unread = noteCount;
                _unreadNotifications = notiCount;
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

    /// <summary>
    /// 🔔 를 눌렀다. 서랍을 펴면서 <b>그 자리에서 숫자를 다시 센다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 목록과 숫자는 <b>서로 다른 길로 온다</b>(<c>/inbox</c> ·
    /// <c>/inbox/unread-count</c>). 서랍은 펼 때마다 목록을 새로 읽는데 종은
    /// 안 세고 있어서, 다른 장비에서 읽고 온 사람이 종을 누르면
    /// <b>「3」이 붙은 채로 빈 목록</b>을 봤다 — 열어 확인했는데도 숫자가
    /// 그대로라 그것이 고장으로 읽힌다.
    /// </para>
    /// <para>
    /// <b>여는 것을 기다리지 않는다.</b> 세는 동안 서랍이 안 열리면 누른
    /// 보람이 없다 — 먼저 펴고 숫자는 뒤따라 맞춘다.
    /// </para>
    /// </remarks>
    private async Task OpenNotificationsAsync()
    {
        NotificationDrawerHandle.Open();
        await CountUnreadAsync();
    }

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
        NotificationDrawerHandle.Read -= CountUnreadAsync;
        Navigation.LocationChanged -= OnLocationChanged;

        // **빠뜨리면 화면을 닫아도 1분마다 조회가 계속 나간다.**
        _poll?.Dispose();
        _poll = null;
    }

    /// <summary>
    /// 브라우저 쪽에 걸어 둔 것까지 거둔다.
    /// </summary>
    /// <remarks>
    /// <b>떼는 일이 특히 중요하다.</b> <c>unread-sync.js</c> 는 브라우저가 한 번만
    /// 싣고 계속 돌려쓰므로, 안 떼면 죽은 회로의 손잡이가 문서에 매달린 채
    /// 남는다. 회로가 이미 닫혔으면 조용히 넘어간다 — 이 부품은 모든 화면에
    /// 실려 있어서, 여기서 예외가 새면 <b>사람이 창을 닫는 것만으로</b> 오류가 난다.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        Dispose();

        try
        {
            if (_syncModule is not null)
            {
                await _syncModule.InvokeVoidAsync("detachUnreadSync");
                await _syncModule.DisposeAsync();
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
        }
        finally
        {
            _syncModule = null;
            _syncRef?.Dispose();
            _syncRef = null;
        }
    }
}
