using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Components.Security;
using JSini.Web.Components.Menu;

namespace JSini.Web.Components.Layout;

public partial class MainLayout
{
    [Inject] private PageTransition Transition { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IPermissionContext Permissions { get; set; } = default!;
    [Inject] private ITokenStore Tokens { get; set; } = default!;
    [Inject] private ScreenLock Lock { get; set; } = default!;
    [Inject] private MenuFavorites Favorites { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private PortalBootstrap Bootstrap { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private PortalTabs Tabs { get; set; } = default!;
    [Inject] private KeepAliveScreens KeepAlive { get; set; } = default!;
    [Inject] private MenuReveal Reveal { get; set; } = default!;

    /// <summary>사용자 판 접기 손잡이. ☰ 로 사이드바를 펼 때 함께 접는다 — 휴대폰에서 그 판이 헤더 아래를 덮어 안 접으면 메뉴가 가린다.</summary>
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;
    [Inject] private MenuUsageRecorder MenuUsage { get; set; } = default!;

    /// <summary>화면을 열어도 되는지 정하는 단 한 곳. 메뉴 하나에 대한 판정만 한다 — 매개변수가 든 상세 화면은 라우터 앞의 ScreenGate 가 맡는다.</summary>
    [Inject] private Security.ScreenAccess Access { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<MainLayout> Log { get; set; } = default!;
    [Inject] private Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    /// <summary>셸의 클래스. 접힘은 --collapsed, 휴대폰에서 펴진 것은 --nav-open. 첫 그림은 화면 크기를 모르므로 이 표시로 좁은 화면의 깜빡임을 막는다.</summary>
    private string ShellCss
    {
        get
        {
            var css = _sidebarOpen
                ? (_isPhone ? "jsini-shell jsini-shell--nav-open" : "jsini-shell")
                : "jsini-shell jsini-shell--collapsed";

            // 떠다니는 메뉴 단추를 감춘 사람에게 붙는다. 감추기만 하고 헤더의 ☰ 는 되살리지 않는다 — 로고가 메뉴를 여는 길이다.
            if (_fabHidden)
            {
                css += " jsini-shell--fab-hidden";
            }

            // 요청 등록 단추를 감춘 사람에게 붙는다. 메뉴 단추와 따로 둔다. 권한이 없으면 여기 오지 않는다.
            if (_hdFabHidden)
            {
                css += " jsini-shell--hdfab-hidden";
            }

            // 요청 등록 단추를 넓은 화면에도 띄우기로 한 사람에게 붙는다. 감춘 사람에게는 이 표시가 있어도 안 보인다(app.css 가 감추기를 뒤에 둔다).
            if (_hdFabDesktop)
            {
                css += " jsini-shell--hdfab-desktop";
            }

            // 아래 띠를 쓰지 않기로 한 사람에게 붙는다. 여백과 단추 자리 올림을 CSS 가 들고 있어 이 표시로만 걷을 수 있다.
            if (_bottomNavHidden)
            {
                return css + " jsini-shell--bottomnav-hidden";
            }

            // 띠는 쓰되 이름만 감춘 사람에게 붙는다. 띠가 얇아져 본문이 16px 을 돌려받는다 — 값은 --jsini-bottomnav-h 한 토큰에 모은다.
            return _bottomNavLabelsHidden ? css + " jsini-shell--bottomnav-nolabel" : css;
        }
    }

    private bool _sidebarOpen = true;
    private bool _isPhone;
    private bool _isTablet;

    /// <summary>사이드바 폭(px). 기본 330px — DevExpress 데모 값이고 --jsini-sidebar-width 와 같아야 휴대폰 갈래와 어긋나지 않는다. 기기에 남긴다 — 알맞은 폭이 화면 크기에 딸린다.</summary>
    private int _sidebarWidth = 330;

    /// <summary>끌어서 이 폭 이하로 좁히면 붙잡지 않고 접는다(px). 판의 MinSize(90px)보다 커야 이 갈래가 걸린다 — 지금은 10px 차이뿐이라 부품이 90px 에 붙잡아 접힘으로 간다.</summary>
    private const int SidebarSnapPx = 100;

    /// <summary>끌어 접은 순간의 이전 폭(px). 다시 펼 때 이 값으로 되돌린다 — 좁은 폭을 그대로 두면 글자가 잘린 사이드바가 나온다. 접힌 동안 _sidebarWidth 는 끌어 놓은 좁은 값이다.</summary>
    private int? _widthBeforeSnap;

    /// <summary>저장해 둔 폭을 읽었는가. 한 번만 읽는다 — 두 번째부터는 방금 끌어 놓은 값을 옛 값으로 되돌린다.</summary>
    private bool _widthRestored;

    /// <summary>휴대폰에서 메뉴를 편 순간의 뒤로 가기 횟수(theme.js jsiniHistory). 메뉴를 펼 때마다 다시 적어야 지난번 삼킨 뒤로 가기가 이번 이동을 막지 않는다. null 이면 가로채지 않는다.</summary>
    private long? _popsAtOpen;

    /// <summary>지금 화면의 메뉴 줄기. 브레드크럼이 쓴다.</summary>
    private IReadOnlyList<MenuNode> _trail = [];

    /// <summary>옮기기 전 갈고리의 등록증. 놓으면 갈고리가 풀린다.</summary>
    private IDisposable? _navigating;

    /// <summary>지난번에 받은 본문. 라우터가 화면을 갈았는지 이것으로 본다.</summary>
    private RenderFragment? _body;

    /// <summary>이번 그림에서 본문이 갈렸는가. <c>OnAfterRenderAsync</c> 가 쓰고 끈다.</summary>
    private bool _bodyChanged;

    /// <summary>지금 화면의 DB 메뉴 경로. 즐겨찾기 단추가 쓴다.</summary>
    private string? _menuPath;

    /// <summary>잠금 덮개에 띄우는 이름. 누구 화면인지 알려 준다.</summary>
    private string? _userName;

    /// <summary>모바일 화면의 떠다니는 메뉴 단추(FAB) 위치.</summary>
    private string _fabPosition = "bottom-left";

    /// <summary>그 단추를 감췄는가. 감춰도 헤더의 ☰ 는 되살리지 않는다 — 휴대폰에서 메뉴를 여는 길은 로고가 맡는다.</summary>
    private bool _fabHidden = true;

    /// <summary>헬프데스크 요청 등록 단추의 위치. 기본은 메뉴 단추의 반대 귀퉁이다.</summary>
    private string _hdFabPosition = "bottom-right";

    /// <summary>그 단추를 감춰 두었는가. 기본은 <c>true</c> — 권한이 있으면 보인다.</summary>
    private bool _hdFabHidden = true;

    /// <summary>요청 등록 단추를 넓은 화면에도 띄우기로 했는가. 기본은 false — 휴대폰에서만 뜬다.</summary>
    private bool _hdFabDesktop;

    /// <summary>요청 등록 단추가 여는 화면. 권한을 묻는 열쇠도 이 경로다.</summary>
    private const string HelpDeskNewPath = "/helpdesk/request/new";

    /// <summary>요청 등록 단추를 그려도 되는가 — 그 화면을 열 수 있는 사람만. 권한표를 받기 전에는 그리지 않는다. 이미 그 화면이면 그리지 않는다 — 눌러도 아무 일이 없는 단추가 되기 때문이다.</summary>
    private bool HelpDeskFabAllowed =>
        Permissions.IsLoaded && Permissions.CanView(HelpDeskNewPath) && !OnHelpDeskNewPage;

    /// <summary>지금 보고 있는 것이 요청 등록 화면인가.</summary>
    private bool OnHelpDeskNewPage =>
        string.Equals(CurrentHref(), HelpDeskNewPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>AI 에이전트 대화 경로. 옛 경로(/ai/chat)와 정규화된 경로(/site/ai/chat) 둘 다 받는다.</summary>
    private static readonly string[] AiChatPaths = ["/site/ai/chat", "/ai/chat"];

    /// <summary>지금 보고 있는 것이 AI 에이전트 대화 화면인가.</summary>
    private bool OnAiChatPage =>
        AiChatPaths.Any(p => string.Equals(CurrentHref(), p, StringComparison.OrdinalIgnoreCase));

    /// <summary>요청 등록 단추를 한 칸 밀어야 하는가. 메뉴 단추와 같은 귀퉁이에 둘 다 설 때만 — 메뉴 단추를 감췄으면 밀지 않는다.</summary>
    private bool HelpDeskFabStacked => !_fabHidden && _hdFabPosition == _fabPosition;

    /// <summary>오른쪽 아래 귀퉁이를 가리키는 값. 네 자리 중 하나다.</summary>
    private const string BottomRight = "bottom-right";

    /// <summary>위로 가기 단추에 붙일 밀어 올리는 표시. 보이는 떠다니는 단추 수만큼 한 칸씩 민다(감춘 것·권한 없는 것은 세지 않는다). 넓은 화면에서 요청 등록 단추를 띄운 사람에게는 한 칸 비켜 준다. 첫 그림에서는 밀지 않는다.</summary>
    private string ScrollTopFabCss
    {
        get
        {
            if (!_isPhone)
            {
                return HelpDeskFabAllowed && _hdFabDesktop && !_hdFabHidden
                    && _hdFabPosition == BottomRight
                    ? "jsini-shell__fab--stacked"
                    : string.Empty;
            }

            var taken = 0;

            if (!_fabHidden && _fabPosition == BottomRight)
            {
                taken++;
            }

            if (HelpDeskFabAllowed && !_hdFabHidden && _hdFabPosition == BottomRight)
            {
                taken++;
            }

            return taken switch
            {
                >= 2 => "jsini-shell__fab--stacked2",
                1 => "jsini-shell__fab--stacked",
                _ => string.Empty,
            };
        }
    }

    /// <summary>휴대폰 아래 띠를 쓰지 않기로 했는가. 기본은 false — 읽기 전 값이 기본이라 깜빡이지 않는다.</summary>
    private bool _bottomNavHidden;

    /// <summary>띠 칸에서 이름을 감추기로 했는가. 기본은 false. 읽기 전 기본값이 있어 띠가 떴다 두꺼워지지 않는다.</summary>
    private bool _bottomNavLabelsHidden;

    /// <summary>칸들을 넓은 화면 헤더에 세우지 않기로 했는가. 기본은 false — 읽기 전 값이 기본이라 단추가 떴다 사라지지 않는다.</summary>
    private bool _bottomNavDesktopHidden;

    private string _toastPosition = "bottom-right";
    private HorizontalAlignment _toastHorizontal = HorizontalAlignment.Right;
    private VerticalEdge _toastVertical = VerticalEdge.Bottom;

    protected override void OnInitialized()
    {
        if (HttpContextAccessor.HttpContext != null)
        {
            var ua = HttpContextAccessor.HttpContext.Request.Headers.UserAgent.ToString();
            if (ua.Contains("Mobi", StringComparison.OrdinalIgnoreCase))
            {
                _sidebarOpen = false;
            }
        }

        // 탭 줄이 볼 수 없는 화면을 세우지 않게, 되살릴 때와 걷을 때 같은 판정(PortalTabs.CanShow)을 쓴다. 서비스는 회로마다 하나라 치울 때 비우지 않는다.
        Tabs.CanShow = CanShowHref;

        // 살려 둔 화면은 옛 레이아웃과 함께 사라졌다. 비우지 않으면 숨은 것까지 한꺼번에 다시 만들어져 조회가 몰린다.
        KeepAlive.Reset();
        Tabs.Changed += OnTabsChanged;

        Menus.MenusChanged += OnMenusChanged;
        Navigation.LocationChanged += OnLocationChanged;
        _navigating = Navigation.RegisterLocationChangingHandler(OnNavigating);

        // 브레드크럼을 누르면 사이드바가 접혀 있을 때 여기서 편다 — 접힘은 이 레이아웃이 든 상태라 트리 쪽에서는 못 한다.
        Reveal.Requested += OnMenuRevealRequested;

        // 헤더의 권한 그룹은 이 값으로 그려진다. 내 정보는 부트스트랩이 끝나야 실리므로 듣지 않으면 다음 렌더까지 비어 있다.
        Me.Changed += OnMeChanged;

        // 환경설정 등에서 모바일 메뉴 단추 위치가 바뀌면 즉시 화면에 반영한다.
        Boot.FabPositionChanged += OnFabPositionChanged;
        Boot.FabHiddenChanged += OnFabHiddenChanged;
        Boot.HelpDeskFabPositionChanged += OnHelpDeskFabPositionChanged;
        Boot.HelpDeskFabHiddenChanged += OnHelpDeskFabHiddenChanged;
        Boot.HelpDeskFabDesktopChanged += OnHelpDeskFabDesktopChanged;
        Boot.BottomNavHiddenChanged += OnBottomNavHiddenChanged;
        Boot.BottomNavLabelsHiddenChanged += OnBottomNavLabelsHiddenChanged;
        Boot.BottomNavDesktopHiddenChanged += OnBottomNavDesktopHiddenChanged;
        Boot.ToastPositionChanged += OnToastPositionChanged;
    }

    
    private void OnToastPositionChanged(string position)
    {
        _toastPosition = position;
        UpdateToastAlignment();
        InvokeAsync(StateHasChanged);
    }
    
    private void UpdateToastAlignment()
    {
        _toastHorizontal = _toastPosition switch {
            "top-left" or "bottom-left" => HorizontalAlignment.Left,
            "top-center" or "bottom-center" or "center"
                or "top-center-1-5" or "top-center-1-4" or "top-center-1-3"
                or "bottom-center-2-3" or "bottom-center-1-4" or "bottom-center-1-5" => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Right,
        };
        _toastVertical = _toastPosition.StartsWith("top") ? VerticalEdge.Top : VerticalEdge.Bottom;
    }

    private void OnFabPositionChanged(string position)
    {
        _fabPosition = position;
        InvokeAsync(StateHasChanged);
    }

    private void OnFabHiddenChanged(bool hidden)
    {
        _fabHidden = hidden;
        InvokeAsync(StateHasChanged);
    }

    private void OnHelpDeskFabPositionChanged(string position)
    {
        _hdFabPosition = position;
        InvokeAsync(StateHasChanged);
    }

    private void OnHelpDeskFabHiddenChanged(bool hidden)
    {
        _hdFabHidden = hidden;
        InvokeAsync(StateHasChanged);
    }

    private void OnHelpDeskFabDesktopChanged(bool shown)
    {
        _hdFabDesktop = shown;
        InvokeAsync(StateHasChanged);
    }

    private void OnBottomNavHiddenChanged(bool hidden)
    {
        _bottomNavHidden = hidden;
        InvokeAsync(StateHasChanged);
    }

    private void OnBottomNavLabelsHiddenChanged(bool hidden)
    {
        _bottomNavLabelsHidden = hidden;
        InvokeAsync(StateHasChanged);
    }

    private void OnBottomNavDesktopHiddenChanged(bool hidden)
    {
        _bottomNavDesktopHidden = hidden;
        InvokeAsync(StateHasChanged);
    }

    private void OnMeChanged() => InvokeAsync(StateHasChanged);

    /// <summary>브레드크럼이 「이 메뉴를 사이드바에서 보여 달라」고 할 때 판을 편다(접지 않는다). 아래 띠의 「메뉴」 칸은 햄버거와 같은 여닫이여야 한다 — 펴기만 하면 덮인 판에서 다시 눌러도 아무 일이 없다.</summary>
    private void OnMenuRevealRequested(string path) => InvokeAsync(async () =>
    {
        if (_sidebarOpen)
        {
            if (string.Equals(path, BottomNav.MenuPath, StringComparison.OrdinalIgnoreCase))
            {
                CloseSidebar();
                StateHasChanged();
            }

            return;
        }

        await OpenSidebarAsync();
        StateHasChanged();
    });
    /// <summary>토큰이 먼저다 — 게이트웨이를 부르려면 있어야 하고 AuthenticationStateProvider 는 Razor 컴포넌트 안에서만 물을 수 있다. 권한표·메뉴·즐겨찾기·내 정보는 PortalBootstrap 이 한 번의 왕복으로 받는다.</summary>
    /// <summary>이 프로세스에서 레이아웃이 몇 번 만들어졌나. 아래 로그가 쓴다.</summary>
    private static int _created;

    protected override async Task OnInitializedAsync()
    {
        // 레이아웃이 업무를 넘나들 때 실제로 다시 만들어지는지 보는 로그다 — 쪽을 새로 열 때만 찍히면 유지되는 것이다. 판정이 끝나면 이 로그를 지운다.
        Log.LogInformation(
            "포털 레이아웃을 새로 만들었다 ({Count}번째, 주소 {Uri}). "
            + "업무를 옮길 때마다 이 숫자가 오르면 감사 문서의 C-4 가 사실이다.",
            Interlocked.Increment(ref _created),
            Navigation.Uri);

        var state = await AuthState.GetAuthenticationStateAsync();
        Tokens.Initialize(state.User);
        _userName = state.User.Identity?.Name;

        // 워터마크 이름은 JS 를 부르지 않고 PortalBoot 의 같은 왕복에 태운다. Bootstrap.LoadAsync 보다 앞이어야 한다 — 뒤면 자식이 이름 없는 왕복을 낸다. 늦으면 PortalBoot 가 혼자 한 번 건다.
        Boot.UseWatermark(_userName);

        await Bootstrap.LoadAsync(state.User);

        // 부트스트랩이 관리자 설정을 실어 오면 여기서 워터마크를 걷는다. 모르는 동안은 깔아 두는 쪽이 맞다 — 빠지는 쪽이 사고다.
        await ApplyWatermarkAsync();

        // 관리자가 바꿨을 수 있어 30초마다 내 설정을 되묻는다 — 관리자가 켜면 다음 로그인을 기다리지 않고 나타나야 한다.
        StartWatermarkWatch();

        Track();
    }

    /// <summary>브라우저에 남아 있던 것(잠금 · 고정 탭 · 사이드바 폭 · 테마)을 회로가 붙은 뒤 되살린다 — 프리렌더 중에는 JS 를 부를 수 없다. 워터마크는 여기서 걸지 않고 이름을 PortalBoot 에 넘겨 같은 왕복에 태운다(찍힌 사진에서 누구 화면인지 드러내려는 것이다).</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 표시는 본문이 갈린 그림에서만 알린다 — 주소가 바뀌면 그림이 두 번 그려지는데 앞엣것에는 새 화면이 없어 곧바로 끝나 버린다. 이 콜백은 자식보다 나중에 불리므로 새 화면의 조회가 이미 잡혀 있다.
        if (_bodyChanged)
        {
            _bodyChanged = false;
            Transition.Rendered();
        }

        if (!firstRender)
        {
            return;
        }

        await RestoreSidebarWidthAsync();
        await RestoreFabPositionAsync();
        await Lock.RestoreAsync();
    }

    /// <summary>끌어 둔 사이드바 폭을 되살린다. PortalBoot 의 같은 왕복에 실려 오므로 왕복은 따로 없다. 첫 그림은 기본 폭(330px)이고 회로가 붙은 뒤 저장된 폭으로 한 번 바뀐다.</summary>
    private async Task RestoreSidebarWidthAsync()
    {
        if (_widthRestored)
        {
            return;
        }

        _widthRestored = true;

        if ((await Boot.ReadAsync()).SidebarWidthPx is not int px || px == _sidebarWidth)
        {
            return;
        }

        _sidebarWidth = px;
        StateHasChanged();
    }

    /// <summary>저장해 둔 모바일 메뉴 단추 위치를 되살린다. PortalBoot 의 단일 왕복으로 읽는다.</summary>
    private async Task RestoreFabPositionAsync()
    {
        var state = await Boot.ReadAsync();
        var pos = state.FabPosition;
        var changed = false;

        if (state.FabHidden != _fabHidden)
        {
            _fabHidden = state.FabHidden;
            changed = true;
        }

        if (state.HelpDeskFabHidden != _hdFabHidden)
        {
            _hdFabHidden = state.HelpDeskFabHidden;
            changed = true;
        }

        if (state.HelpDeskFabPosition != _hdFabPosition)
        {
            _hdFabPosition = state.HelpDeskFabPosition;
            changed = true;
        }

        if (state.HelpDeskFabDesktop != _hdFabDesktop)
        {
            _hdFabDesktop = state.HelpDeskFabDesktop;
            changed = true;
        }

        if (state.BottomNavHidden != _bottomNavHidden)
        {
            _bottomNavHidden = state.BottomNavHidden;
            changed = true;
        }

        if (state.BottomNavLabelsHidden != _bottomNavLabelsHidden)
        {
            _bottomNavLabelsHidden = state.BottomNavLabelsHidden;
            changed = true;
        }

        if (state.BottomNavDesktopHidden != _bottomNavDesktopHidden)
        {
            _bottomNavDesktopHidden = state.BottomNavDesktopHidden;
            changed = true;
        }
        
        var tpos = state.ToastPosition;
        if (tpos != _toastPosition)
        {
            _toastPosition = tpos;
            UpdateToastAlignment();
            changed = true;
        }

        if (pos != _fabPosition)
        {
            _fabPosition = pos;
            changed = true;
        }
        
        if (changed)
        {
            StateHasChanged();
        }
    }

    /// <summary>새 화면이 끼워졌다. 본문이 갈렸는지 보고 화면 크기 판정(ApplyViewport)을 한 번 더 한다. 경계값 767·1023 은 셸 CSS 와 같아야 한다.</summary>
    protected override void OnParametersSet()
    {
        // 본문이 갈렸는가 — 라우터가 새 화면을 끼울 때만 참조가 바뀐다.
        if (!ReferenceEquals(_body, Body))
        {
            _body = Body;
            _bodyChanged = true;
            ShowKeptScreen();
        }

        ApplyViewport();
    }

    /// <summary>
    /// 휴대폰 경계(≤767px)를 넘었다. 값만 받아 <see cref="ApplyViewport"/> 에 넘긴다.
    /// </summary>
    private void OnPhoneChanged(bool active)
    {
        _isPhone = active;
        ApplyViewport();
    }

    /// <summary>태블릿 경계(≤1023px)를 넘었다.</summary>
    private void OnTabletChanged(bool active)
    {
        _isTablet = active;
        ApplyViewport();
    }

    /// <summary>지금 화면 크기에 맞춰 메뉴를 거르고, 휴대폰이면 사이드바를 접는다. 부르는 자리는 둘이다 — 화면이 갈릴 때(OnParametersSet)와 화면 크기가 갈릴 때. 뒤쪽이 없으면 휴대폰으로 처음 들어온 사람이 메뉴를 한 번 누르기 전까지 덮개를 걷지 못한다.</summary>
    private void ApplyViewport()
    {
        var viewport = _isPhone ? Viewport.Phone
            : _isTablet ? Viewport.Tablet
            : Viewport.Desktop;

        Menus.SetViewport(viewport);

        // 휴대폰에서는 사이드바가 본문을 덮으므로 접힌 채로 시작한다.
        if (_isPhone && _sidebarOpen)
        {
            CloseSidebar();
        }
    }

    /// <summary>헤더를 눌렀다. 휴대폰에서 메뉴가 펴져 있으면 먼저 닫는다 — 누른 단추가 하던 일은 막지 않고 함께 일어난다. 넓은 화면에서는 아무 일도 하지 않는다 — 사이드바는 본문과 겹치지 않는 늘 자리를 차지하는 판이라 접으면 사용자가 맞춰 둔 폭이 사라진다.</summary>
    private void OnHeaderClick()
    {
        if (_isPhone && _sidebarOpen)
        {
            CloseSidebar();
        }
    }

    /// <summary>로고를 누르면 휴대폰에서는 메뉴를 여닫는다(넓은 화면에서는 기본대로 / 로 간다). 떠다니는 단추를 감춘 사람에게는 이것이 유일하게 여는 길이라 감출 때 헤더의 ☰ 를 되돌리지 않는다(_fabHidden).</summary>
    private Task OnBrandClick(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        if (_isPhone)
        {
            return ToggleSidebarAsync();
        }
        return Task.CompletedTask;
    }

    private Task ToggleSidebarAsync()
    {
        if (!_sidebarOpen)
        {
            return OpenSidebarAsync();
        }

        CloseSidebar();
        return Task.CompletedTask;
    }

    /// <summary>사이드바를 편다. 휴대폰이면 지금까지의 뒤로 가기 횟수를 적는다 — 이다음 이동이 뒤로 가기인지는 그 수가 늘었는지로 가린다. 펴는 길이 둘(☰ · 브레드크럼)이라 여기 한 곳으로 모은다 — 한쪽에서 적기를 빠뜨리면 그쪽으로 연 메뉴만 뒤로 가기로 안 닫힌다.</summary>
    private async Task OpenSidebarAsync()
    {
        UserDrawer.Close();

        _sidebarOpen = true;
        RestoreSnappedWidth();

        if (!_isPhone)
        {
            return;
        }

        try
        {
            _popsAtOpen = await Js.InvokeAsync<long>("jsiniHistory.pops");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException
                                       or TaskCanceledException)
        {
            // theme.js 가 안 실렸거나 회로가 끊겼다. 이번 폄에서는 뒤로 가기를
            // 가로채지 않는다 — 뒤로 가기가 예전처럼 화면을 옮긴다.
            _popsAtOpen = null;
            Log.LogDebug(ex, "뒤로 가기 횟수를 읽지 못했다.");
        }
    }

    /// <summary>사이드바를 접는다. 적어 둔 뒤로 가기 횟수도 함께 버린다 — 남기면 다음에 편 메뉴에서 첫 이동이 뒤로 가기로 읽힌다.</summary>
    private void CloseSidebar()
    {
        _sidebarOpen = false;
        _popsAtOpen = null;
    }

    /// <summary>떠다니는 요청 등록 단추를 눌렀다. 메뉴가 펴져 있으면 먼저 접는다 — 덮개보다 위에 떠서 메뉴를 펴 둔 채로도 눌리는데, 접지 않고 옮기면 새 화면 위에 사이드바가 덮인 채 도착한다. 이미 그 화면이면 아무것도 하지 않는다 — 같은 주소로 다시 옮기면 적던 글이 비워진다.</summary>
    private void OpenHelpDeskRequest()
    {
        if (_sidebarOpen)
        {
            CloseSidebar();
        }

        if (OnHelpDeskNewPage)
        {
            return;
        }

        Navigation.NavigateTo(HelpDeskNewPath);
    }

    /// <summary>스플리터의 접기 화살표로 접었다 폈다. 헤더의 ☰ 와 같은 상태를 나눠 쓴다 — 따로 두면 화살표로 접은 뒤 ☰ 가 아무 일도 안 하는 것처럼 보인다. 휴대폰에서는 이 이벤트가 오지 않는다(구분선을 감춘다) — 여기서는 접을 때 뒤로 가기 횟수를 버리기만 한다.</summary>
    private void OnSidebarCollapsedChanged(bool collapsed)
    {
        if (collapsed)
        {
            CloseSidebar();
            return;
        }

        _sidebarOpen = true;
        RestoreSnappedWidth();
    }

    /// <summary>끌어서 접었다면(SidebarSnapPx) 그때의 폭으로 되돌린다. 다시 펴는 모든 길에서 부른다 — 접을 때 미리 되돌리면 Size 속성이 접기 전과 같아져 Blazor 가 바뀌었다고 전하지 않아 다시 편 사이드바가 끌어 놓았던 폭 그대로 나온다.</summary>
    private void RestoreSnappedWidth()
    {
        if (_widthBeforeSnap is int px)
        {
            _sidebarWidth = px;
            _widthBeforeSnap = null;
        }
    }

    /// <summary>끌어서 폭을 바꿨다. 브라우저에 적는다. 넘어오는 값은 "312px" 처럼 단위가 붙은 글자라 숫자만 떠서 담는다 — 단위째 담으면 다음에 px 를 또 붙여 312pxpx 가 된다. 접힌 상태는 담지 않는다 — 접으면 0 이 와서 다음에 폭 0 인 사이드바가 나온다.</summary>
    private async Task OnSidebarResizedAsync(string? size)
    {
        if (!Pixels(size, out var px))
        {
            return;
        }

        // 쓸 수 없을 만큼 좁혔으면 붙잡지 않고 접는다. 폭은 접기 직전 것을
        // 기억해 두었다가 다시 펼 때 되돌리고(`RestoreSnappedWidth`),
        // **저장소에는 담지 않는다** — 다음에 들어와도 쓰던 폭이라야 한다.
        if (px <= SidebarSnapPx)
        {
            _widthBeforeSnap ??= _sidebarWidth;
            _sidebarWidth = px;
            CloseSidebar();
            return;
        }

        _widthBeforeSnap = null;
        _sidebarWidth = px;

        try
        {
            await Js.InvokeVoidAsync("localStorage.setItem", PortalBoot.SidebarWidthKey,
                px.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // 저장소를 막아 둔 브라우저이거나 회로가 이미 끊겼다. 폭은 이번
            // 화면에서만 유지된다 — 그 이상 할 일이 없다.
            Log.LogDebug(ex, "사이드바 폭을 저장하지 못했다.");
        }
    }

    /// <summary>"312px" 에서 312 를 뜬다. 0 이하나 못 읽는 값은 걸러 접힘(0)과 이상한 값을 함께 막는다.</summary>
    private static bool Pixels(string? size, out int px)
    {
        px = 0;

        if (size is null)
        {
            return false;
        }

        var digits = size.AsSpan().TrimEnd();
        var end = 0;

        while (end < digits.Length && (char.IsAsciiDigit(digits[end]) || digits[end] == '.'))
        {
            end++;
        }

        return double.TryParse(digits[..end], System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out var value)
               && value >= 1
               && (px = (int)Math.Round(value)) > 0;
    }

    /// <summary>사이드바 탭의 오른쪽 클릭 창에서 「메뉴 새로 고침」을 눌렀다. 통을 거치지 않고 메뉴·권한표·즐겨찾기를 다시 받는다 — 바뀐 것은 MenusChanged 와 Favorites.Changed 로 퍼진다.</summary>
    private async Task<bool> RefreshMenusAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        var fresh = await Bootstrap.RefreshAsync(state.User);

        Track();
        return fresh;
    }

    private void OnMenusChanged()
    {
        // 메뉴가 늦게 도착하면 그때 브레드크럼과 탭 이름이 채워진다.
        Track();
        InvokeAsync(StateHasChanged);
    }

    /// <summary>옮기기 전에 표시를 켠다(Vue 의 router.beforeEach 자리). LocationChanged 로는 늦다 — 라우터가 먼저 새 화면을 만들어 그 화면이 조회를 잡을 때 표시가 아직 안 켜진 상태라 곧바로 걷힌다. 이 갈고리는 딱 한 가지 이동만 막는다: 휴대폰에서 메뉴가 펴져 있을 때의 뒤로 가기는 한 번 삼키고 메뉴부터 닫는다. 뒤로 가기인지는 WentBackAsync 가 가린다 — 이 자리 값만으로는 코드가 낸 이동과 뒤로 가기가 똑같이 생겨 가를 수 없다. 묻는 것은 휴대폰에서 메뉴가 펴져 있을 때뿐이다.</summary>
    private async ValueTask OnNavigating(LocationChangingContext context)
    {
        if (_isPhone && _sidebarOpen && await WentBackAsync())
        {
            context.PreventNavigation();
            CloseSidebar();
            await InvokeAsync(StateHasChanged);
            return;
        }

        Transition.Begin();
    }

    /// <summary>지금 이동이 뒤로 가기인가 — 메뉴를 편 뒤 뒤로 가기 횟수가 늘었으면 그렇다. 못 물었으면 false(막지 않는 쪽으로 진다).</summary>
    private async Task<bool> WentBackAsync()
    {
        if (_popsAtOpen is not { } marked)
        {
            return false;
        }

        try
        {
            return await Js.InvokeAsync<long>("jsiniHistory.pops") > marked;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException
                                       or TaskCanceledException)
        {
            Log.LogDebug(ex, "뒤로 가기 횟수를 읽지 못했다.");
            return false;
        }
    }

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        Track();

        // 휴대폰에서는 메뉴를 누르면 사이드바가 본문을 덮은 채로 남는다.
        if (_isPhone)
        {
            CloseSidebar();
        }

        InvokeAsync(StateHasChanged);
    }

    /// <summary>지금 화면이 메뉴에 있는데 볼 권한이 없다. 본문 대신 안내를 그린다.</summary>
    private bool _denied;

    /// <summary>지금 주소를 보고 탭·브레드크럼·즐겨찾기 열쇠를 맞춘다. 볼 권한이 없는 화면이면 탭을 열지 않고 본문을 막는다 — 권한은 거르기 전 메뉴 목록(Trail)을 훑을 때도 봐야 한다.</summary>
    private void Track()
    {
        var href = CurrentHref();

        _trail = Menus.Trail(href);
        _menuPath = _trail.Count > 0 ? _trail[^1].Path : null;
        _denied = _trail.Count > 0 && !MayOpen(_trail[^1]);

        if (_denied)
        {
            // 브레드크럼도 비운다 — 볼 수 없는 메뉴의 이름과 자리를 알려 줄 까닭이 없다.
            _trail = [];
            _menuPath = null;
        }

        // 로그인·오류 화면과 메뉴에 없는 화면은 탭으로 만들지 않는다 — 진단 화면이 탭을 채우면 업무 탭이 밀려난다.
        if (_trail.Count > 0 && !_denied)
        {
            Tabs.Open(href, _trail[^1].Title);

            // 사용기록은 여기서 적는다 — 여기까지 왔으면 메뉴에 있고 볼 권한도 있는 화면이다. 프리렌더에서는 적지 않는다: 프리렌더와 회로는 기록기가 따로라 한 화면이 두 줄로 남는다.
            if (RendererInfo.IsInteractive)
            {
                MenuUsage.Record(_trail[^1], href);
            }
        }

        // 권한표·메뉴는 늦게 오므로 이미 되살아난 고정 탭이 있을 수 있다.
        Tabs.Prune();
    }

    /// <summary>라우터가 화면을 갈았다. 「화면 유지」 메뉴면 살려 둔 것을 꺼낸다. Track 보다 먼저 불린다(라우터가 LocationChanged 를 먼저 듣는다) — 거기서 가르면 첫 그림에 @Body 로 한 벌이 만들어졌다가 버려져 조회가 두 번 돈다.</summary>
    private void ShowKeptScreen()
    {
        var href = CurrentHref();
        var trail = Menus.Trail(href);
        var keep = trail.Count > 0 && trail[^1].KeepAlive && MayOpen(trail[^1]);

        KeepAlive.Show(href, keep, typeof(MainLayout));
    }

    /// <summary>본문 하나의 클래스. 안 보이는 본문은 자리에 세워 둔 채 감춘다 — display: none 이면 구른 자리와 표의 치수가 날아간다.</summary>
    private static string ContentCss(bool shown) =>
        shown ? "jsini-shell__content" : "jsini-shell__content jsini-shell__content--parked";

    /// <summary>탭이 닫혔으면 살려 둔 화면도 놓아 준다 — 놓아야 Dispose 가 불려 타이머가 멈춘다.</summary>
    private void OnTabsChanged()
    {
        if (KeepAlive.Retain(Tabs.Items.Select(t => t.Href)))
        {
            InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>이 메뉴의 화면을 열 수 있는가. 판정은 ScreenAccess.CanOpen 에 맡긴다. 권한표를 받기 전에는 막지 않는다 — 막으면 새로고침마다 「권한이 없습니다」가 한 번 스친다.</summary>
    private bool MayOpen(MenuNode node) => Access.CanOpen(node);

    /// <summary>탭 줄에 둬도 되는 주소인가. 메뉴에 없는 화면은 막지 않는다(<see cref="Track"/> 머리말).</summary>
    private bool CanShowHref(string href)
    {
        var trail = Menus.Trail(href);
        return trail.Count == 0 || MayOpen(trail[^1]);
    }

    /// <summary>브라우저에 떠 있는 경로. 질의 문자열과 조각은 뗀다.</summary>
    private string CurrentHref()
    {
        var relative = Navigation.ToBaseRelativePath(Navigation.Uri);
        var cut = relative.IndexOfAny(['?', '#']);

        if (cut >= 0)
        {
            relative = relative[..cut];
        }

        return "/" + relative.TrimStart('/');
    }

    /// <summary>내 설정을 되묻는 시계. 관리자가 워터마크를 켜고 끄는 것을 옮긴다. 레이아웃이 새로 생기므로 Dispose 에서 끄고, 새 레이아웃이 자기 것을 켠다 — 시계가 쌓이지 않는다.</summary>
    private Timer? _watermarkWatch;

    /// <summary>되묻는 간격 30초. 짧으면 게이트웨이 호출이 늘고 길면 관리자가 켠 뒤 늦게 나온다. 내 정보 조회 하나에 실려 이름·사진·권한 그룹도 함께 새로워진다.</summary>
    private static readonly TimeSpan WatermarkWatchInterval = TimeSpan.FromSeconds(30);

    private void StartWatermarkWatch()
    {
        _watermarkWatch?.Dispose();

        _watermarkWatch = new Timer(
            // 회로 밖 스레드에서 온다 — InvokeAsync 로 회로에 들어가야 JS 호출과 렌더가 안전하다.
            _ => _ = InvokeAsync(RefreshWatermarkAsync),
            null,
            WatermarkWatchInterval,
            WatermarkWatchInterval);
    }

    private async Task RefreshWatermarkAsync()
    {
        // 실패는 삼킨다 — 게이트웨이가 잠깐 안 되는 것과 워터마크가 틀린 것은 무게가 다르다. 못 물으면 지금 상태를 그대로 둔다.
        await Me.ReloadAsync();
        await ApplyWatermarkAsync();
    }

    /// <summary>관리자가 정한 값대로 깔거나 걷는다.</summary>
    private Task ApplyWatermarkAsync() =>
        Boot.ApplyWatermarkAsync(Me.Watermark ? _userName : null);

    public void Dispose()
    {
        Menus.MenusChanged -= OnMenusChanged;
        Navigation.LocationChanged -= OnLocationChanged;
        Tabs.Changed -= OnTabsChanged;
        Reveal.Requested -= OnMenuRevealRequested;
        _navigating?.Dispose();
        Me.Changed -= OnMeChanged;
        Boot.FabPositionChanged -= OnFabPositionChanged;
        Boot.FabHiddenChanged -= OnFabHiddenChanged;
        Boot.HelpDeskFabPositionChanged -= OnHelpDeskFabPositionChanged;
        Boot.HelpDeskFabHiddenChanged -= OnHelpDeskFabHiddenChanged;
        Boot.HelpDeskFabDesktopChanged -= OnHelpDeskFabDesktopChanged;
        Boot.BottomNavHiddenChanged -= OnBottomNavHiddenChanged;
        Boot.BottomNavLabelsHiddenChanged -= OnBottomNavLabelsHiddenChanged;
        Boot.BottomNavDesktopHiddenChanged -= OnBottomNavDesktopHiddenChanged;
        Boot.ToastPositionChanged -= OnToastPositionChanged;
        _watermarkWatch?.Dispose();
    }
}
