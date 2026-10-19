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
    [Inject] private HeaderIconManager HeaderIcons { get; set; } = default!;
    [Inject] private IPermissionContext Permissions { get; set; } = default!;

    /// <summary>사용자 판을 접는 손잡이. 이 띠가 여는 것은 모두 그 판 뒤로 들어간다 — 휴대폰에서 판이 헤더 아래를 덮기 때문이다.</summary>
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;

    /// <summary>지금 화면의 DB 메뉴 경로. 즐겨찾기는 이 값으로 담긴다(링크 주소와 섞으면 이미 담은 것이 안 담긴 것으로 보인다). 메뉴에 없는 화면이면 null 이고 별 단추가 꺼진다.</summary>
    [Parameter] public string? MenuPath { get; set; }

    /// <summary>빠른 지시 화면의 열쇠. 주소가 아니라 이것으로 찾는다 — DB 의 path 는 옛 경로가 남아 있을 수 있다.</summary>
    private const string AskRouteKey = "projmng.ai.ask";

    /// <summary><c>route_key</c> 를 아직 안 채운 DB 를 위한 대비책.</summary>
    private const string AskPath = "/projmng/ai/ask";

    /// <summary>휴대폰인가. ⚡ 이 서랍을 여는지 화면으로 가는지를 가른다. 브레드크럼이 받는 값과 같다.</summary>
    [Parameter] public bool IsPhone { get; set; }

    private bool IsFavorite => Favorites.Contains(MenuPath);

    /// <summary>쪽지 쓰기 창이 열려 있나.</summary>
    private bool _writing;

    /// <summary>안 읽은 쪽지 수. 0 이면 숫자를 안 붙인다.</summary>
    private int _unread;

    /// <summary>안 읽은 알림 수. 0 이면 숫자를 안 붙인다.</summary>
    private int _unreadNotifications;

    /// <summary>스스로 다시 세는 시계(1분). 다른 장비에서 읽은 것이 여기로 넘어오는 마지막 길이다. 틀려도 조용한 값이라 더 촘촘히 물을 까닭이 없다.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private Timer? _poll;

    /// <summary>이 탭이 보이고 있나. 안 보이는 동안은 시계가 쉰다 — 돌아오는 순간 브라우저가 알려 주고 그때 한 번 센다.</summary>
    private bool _visible = true;

    /// <summary>바깥 사정을 들어다 주는 브라우저 쪽(<c>unread-sync.js</c>).</summary>
    private IJSObjectReference? _syncModule;

    /// <summary>그쪽이 우리를 부를 손잡이. <b>반드시 버린다</b>(회로마다 하나씩 샌다).</summary>
    private DotNetObjectReference<HeaderTools>? _syncRef;

    /// <summary>✉ 에 얹는 글. 안 읽은 것이 있으면 그 수를 함께 적는다 — 작은 숫자만으로는 무엇을 세는지 알 수 없다.</summary>
    private string NoteTitle => _unread > 0
        ? $"쪽지 쓰기 — 안 읽은 쪽지 {_unread}통"
        : "쪽지 쓰기";

    /// <summary>🔔 에 얹는 글.</summary>
    private string NotificationTitle => _unreadNotifications > 0
        ? $"알림 — 안 읽은 알림 {_unreadNotifications}건"
        : "알림";

    /// <summary>서랍으로 열리나. 휴대폰이거나 알맹이가 없으면 거짓이다.</summary>
    private bool AskDocks => !IsPhone && Ask.CanDock;

    /// <summary>빠른 지시 단추를 그릴 수 있나. 책상 화면에서는 서랍이 준비되었거나 메뉴 권한이 있으면 그리고, 환경설정에서 켜져 있어야 한다.</summary>
    private bool CanShowAsk => (AskMenu is not null || AskDocks) && HeaderIcons.IsIconEnabled("quick_ask");

    /// <summary>쪽지 단추를 그릴 수 있나. 메뉴 권한이 있고 환경설정에서 켜져 있어야 한다.</summary>
    private bool CanShowNote => (MenuLookup.Find(Menus.VisibleMenus, "admin.note.write", "/admin/note/write") != null || Permissions.CanView("/admin/note/write"))
                                && HeaderIcons.IsIconEnabled("note");

    /// <summary>알림 단추를 그릴 수 있나. 메뉴 권한이 있고 환경설정에서 켜져 있어야 한다.</summary>
    private bool CanShowNotification => (MenuLookup.Find(Menus.VisibleMenus, "admin.push.history", "/system/push/history") != null || Permissions.CanView("/system/push/history"))
                                        && HeaderIcons.IsIconEnabled("notification");

    /// <summary>⚡ 에 얹는 글. 누르면 무엇이 일어나는지를 적는다 — 같은 단추가 자리에 따라 서랍을 열기도 화면을 옮기기도 한다.</summary>
    private string AskTitle => AskDocks
        ? $"{(AskMenu?.Title ?? "빠른 지시")} — 옆에서 한 줄 보내기"
        : $"{(AskMenu?.Title ?? "빠른 지시")} — AI 에게 한 줄 보내기";

    /// <summary>⚡ 를 눌렀다. 책상이면 서랍, 휴대폰이면 화면이다.</summary>
    private void OpenAsk()
    {
        // 펴 둔 사용자 판을 먼저 접는다 — 휴대폰에서는 새 화면 위에 판이 덮인 채 도착하기 때문이다.
        UserDrawer.Close();

        if (AskDocks)
        {
            Ask.Toggle();
            return;
        }

        if (AskMenu is { } ask)
        {
            Navigation.NavigateTo(ask.LinkTarget);
        }
        else
        {
            Navigation.NavigateTo(AskPath);
        }
    }

    /// <summary>권한과 화면 크기로 걸러진 목록에서 찾는다(원본 AllMenus 에서 찾으면 볼 수 없는 사람에게도 단추가 나온다).</summary>
    private MenuNode? AskMenu => MenuLookup.Find(Menus.VisibleMenus, AskRouteKey, AskPath);


    protected override void OnInitialized()
    {
        Favorites.Changed += OnFavoritesChanged;
        HeaderIcons.Changed += OnFavoritesChanged;

        // 화면을 옮길 때마다 안 읽은 수를 다시 센다 — 이 부품은 레이아웃에 붙어 있어 듣지 않으면 숫자가 로그인 순간에 붙박인다.
        Navigation.LocationChanged += OnLocationChanged;

        // 메뉴는 로그인 직후·권한 갱신 때 뒤늦게 실린다 — 듣지 않으면 처음 그린 뒤로 단추가 영영 안 나타난다.
        Menus.MenusChanged += OnFavoritesChanged;

        // 서랍을 닫는 길은 셋(⚡ 다시 누르기 · 서랍의 ✕ · 화면 옮기기)인데 뒤의 둘은 여기를 안 거친다 — 듣지 않으면 ⚡ 의 글만 옛 상태에 남는다.
        Ask.Changed += OnFavoritesChanged;

        // 알림함 서랍에서 읽음을 찍으면 종의 숫자를 다시 센다 — 콜백으로 받던 길이 끊겼으므로 듣는다. 안 들으면 모두 읽음 뒤에도 숫자가 남는다.
        NotificationDrawerHandle.Read += CountUnreadAsync;
    }

    private void OnFavoritesChanged() => InvokeAsync(StateHasChanged);

    /// <summary>첫 그림 뒤에 한 번 센다. 프리렌더에서는 세지 않는다 — 두 번 타고 숫자가 떴다 사라진다(DataPage.CanLoad 와 같은 자리).</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await CountUnreadAsync();
            await AttachSyncAsync();
            StartPoll();
        }
    }

    /// <summary>바깥 사정을 듣기 시작한다 — 탭으로 돌아옴 · 망이 붙음 · 이 기기에 푸시가 막 도착함. 여러 장비를 쓰는 사람의 숫자가 어긋나는 자리를 덮는다. 못 걸어도 조용히 지나간다.</summary>
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

    /// <summary>시계를 켠다. 회로 안에서만 켠다 — 프리렌더된 부품은 곧 버려지는데 시계는 그것을 모르고 깨어난다.</summary>
    private void StartPoll()
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        _poll?.Dispose();
        _poll = new Timer(_ => _ = PollAsync(), null, PollInterval, PollInterval);
    }

    /// <summary>시계가 깨어났다. 안 보이는 탭에서는 아무것도 하지 않는다.</summary>
    private async Task PollAsync()
    {
        if (!_visible) return;

        try
        {
            await SyncUnreadAsync();
        }
        catch (Exception)
        {
            // 시계가 부른 것이라 받는 쪽이 없어 예외는 조용히 사라진다 — 숫자는 직전 값으로 두고 다음 차례에 다시 해 본다.
        }
    }

    /// <summary>지금 당장 다시 센다. 브라우저 쪽(unread-sync.js)과 시계가 부른다. 펴 둔 서랍의 목록도 함께 맞춘다 — 숫자만 고치면 이미 없는 알림 카드를 사람이 계속 누른다.</summary>
    [JSInvokable]
    public async Task SyncUnreadAsync()
    {
        await CountUnreadAsync();
        await NotificationDrawerHandle.NotifyChangedAsync();
    }

    /// <summary>이 탭이 보이는지를 브라우저가 알려 온다. 값만 갈아 둔다 — 다시 보이게 되면 세는 일은 따로 부른다.</summary>
    [JSInvokable]
    public Task SetVisibleAsync(bool visible)
    {
        _visible = visible;
        return Task.CompletedTask;
    }

    /// <summary>화면을 옮겼다. async void 로 두지 않는다 — 여기서 새는 예외를 아무도 못 잡아 회로가 끊긴다.</summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        => _ = InvokeAsync(CountUnreadAsync);

    /// <summary>안 읽은 쪽지를 센다. 못 세면 조용히 지나간다 — 숫자 하나 때문에 상단 띠에 오류를 띄우지 않는다.</summary>
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

    /// <summary>🔔 를 눌렀다. 서랍을 펴면서 그 자리에서 숫자를 다시 센다 — 목록과 숫자는 다른 길로 와서 종을 안 세면 「3」이 붙은 빈 목록이 된다. 여는 것을 기다리지 않는다.</summary>

    /// <summary>✉ 를 눌렀다. 휴대폰에서는 사용자 판이 뒤에 깔린 채 남으므로 먼저 접는다.</summary>
    private void OpenNote()
    {
        UserDrawer.Close();
        _writing = true;
    }

    private async Task OpenNotificationsAsync()
    {
        UserDrawer.Close();
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

    /// <summary>같은 주소로 다시 이동해 화면만 새로 만든다. forceLoad 를 켜면 문서를 새로 받아 회로가 끊기므로 켜지 않는다.</summary>
    private void Refresh() =>
        Navigation.NavigateTo(Navigation.Uri, forceLoad: false, replace: true);

    private Task ToggleFullScreenAsync() =>
        Js.InvokeAsync<bool>("jsiniScreen.toggle").AsTask();

    public void Dispose()
    {
        Favorites.Changed -= OnFavoritesChanged;
        HeaderIcons.Changed -= OnFavoritesChanged;
        Menus.MenusChanged -= OnFavoritesChanged;
        Ask.Changed -= OnFavoritesChanged;
        NotificationDrawerHandle.Read -= CountUnreadAsync;
        Navigation.LocationChanged -= OnLocationChanged;

        // **빠뜨리면 화면을 닫아도 1분마다 조회가 계속 나간다.**
        _poll?.Dispose();
        _poll = null;
    }

    /// <summary>브라우저 쪽에 걸어 둔 것까지 거둔다. unread-sync.js 는 한 번 실려 돌려쓰므로 떼지 않으면 죽은 회로의 손잡이가 남는다. 이미 닫혔으면 조용히 넘어간다 — 모든 화면에 실리므로 여기서 새는 예외는 창 닫기만으로 오류가 된다.</summary>
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
