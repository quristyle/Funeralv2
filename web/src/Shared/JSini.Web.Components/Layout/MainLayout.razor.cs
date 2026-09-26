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
    [Inject] private MenuReveal Reveal { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<MainLayout> Log { get; set; } = default!;

    /// <summary>
    /// 셸의 클래스. 접힘은 <c>--collapsed</c>, <b>휴대폰에서 펴진 것은
    /// <c>--nav-open</c></b> 이다.
    ///
    /// <para>
    /// <b>펴짐을 따로 표시하는 이유</b>는 첫 그림 때문이다. 프리렌더는
    /// 화면 크기를 모르므로 <c>_isPhone</c> 이 거짓이고 사이드바는 펴진
    /// 채로 나간다 — 휴대폰이면 그 판이 본문을 덮은 그림이 회로가 붙을
    /// 때까지 떠 있다. 좁은 화면에서는 이 표시가 붙었을 때만 사이드바를
    /// 그리게 해 두면(app.css) 그 깜빡임이 없어진다.
    /// </para>
    /// </summary>
    private string ShellCss
    {
        get
        {
            var css = _sidebarOpen
                ? (_isPhone ? "jsini-shell jsini-shell--nav-open" : "jsini-shell")
                : "jsini-shell jsini-shell--collapsed";

            // 떠다니는 단추를 감춘 사람에게 붙는 표시. 감추는 일만 하고
            // **헤더의 ☰ 를 되돌리지는 않는다** — 휴대폰에서 메뉴를 여는 일은
            // 로고가 이미 한다(`OnBrandClick`, app.css 의 같은 이름).
            if (_fabHidden)
            {
                css += " jsini-shell--fab-hidden";
            }

            // 아래 띠를 쓰지 않기로 한 사람에게 붙는 표시. 띠 자체는 아래에서
            // 안 그리지만, **본문 아래 여백과 단추 자리 올림**은 CSS 가 들고
            // 있어서 이 표시로만 걷을 수 있다(app.css 의 같은 이름).
            return _bottomNavHidden ? css + " jsini-shell--bottomnav-hidden" : css;
        }
    }

    private bool _sidebarOpen = true;
    private bool _isPhone;
    private bool _isTablet;

    /// <summary>
    /// 사이드바 폭(px). <b>기본값은 DevExpress 데모의 330px 이다</b>
    /// (`--jsini-sidebar-width` 와 같은 값이라야 휴대폰 갈래와 어긋나지 않는다).
    ///
    /// <para>
    /// 끌어 놓으면 이 값이 바뀌고 브라우저에 남는다. <b>사용자가 아니라
    /// 기기에 남기는 이유</b>는 알맞은 폭이 화면 크기에 딸린 것이기 때문이다 —
    /// 노트북에서 좁혀 둔 폭이 사무실 큰 모니터에 따라오면 그때는 그것이
    /// 불편이 된다.
    /// </para>
    /// </summary>
    private int _sidebarWidth = 330;

    /// <summary>
    /// 저장해 둔 폭을 이미 읽었나. <b>한 번만 읽는다</b> — 읽기는
    /// <see cref="PortalBoot"/> 의 공용 왕복에 실려 오고, 두 번째부터는
    /// 사용자가 방금 끌어 놓은 값을 옛 값으로 되돌리게 된다.
    /// </summary>
    private bool _widthRestored;

    /// <summary>
    /// 휴대폰에서 메뉴를 편 그 순간의 <b>뒤로 가기 횟수</b>(theme.js
    /// <c>jsiniHistory</c>). 이동할 때 다시 물어 늘었으면 뒤로 가기다.
    ///
    /// <para>
    /// 메뉴를 펼 때마다 다시 적는다. 그래야 <b>지난번에 삼킨 뒤로 가기가
    /// 이번 이동을 막지 않는다</b> — 한 번 적고 말면 그 뒤의 첫 메뉴 선택이
    /// 뒤로 가기로 읽혀 화면이 안 열린다.
    /// </para>
    ///
    /// <para>
    /// <c>null</c> 이면 가로채지 않는다 — 메뉴가 접혀 있거나, 횟수를 못 읽었을
    /// 때다. 못 읽었으면 뒤로 가기가 예전처럼 화면을 옮긴다(조용히 진다).
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 그 단추를 감춰 두었는가. 감춰도 <b>헤더의 ☰ 는 되살리지 않는다</b> —
    /// 휴대폰에서 메뉴를 여는 일은 로고가 한다(<see cref="OnBrandClick"/>).
    /// </summary>
    private bool _fabHidden;

    /// <summary>
    /// 휴대폰 아래 띠를 <b>쓰지 않기로</b> 했는가. 기본은 <c>false</c> — 쓴다.
    /// 읽어 오기 전에도 그 값이라 한 번도 안 고친 사람에게 띠가 깜빡이지 않는다.
    /// </summary>
    private bool _bottomNavHidden;

    private string _toastPosition = "bottom-right";
    private HorizontalAlignment _toastHorizontal = HorizontalAlignment.Right;
    private VerticalEdge _toastVertical = VerticalEdge.Bottom;

    protected override void OnInitialized()
    {
        // 탭 줄이 볼 수 없는 화면을 세우지 않게 한다 — 고정 탭을 되살릴 때와
        // 이미 열린 탭을 걷을 때 같은 판정을 쓴다(`PortalTabs.CanShow`).
        // 레이아웃은 업무를 옮길 때마다 새로 생기지만 서비스는 회로마다 하나라
        // 누가 채워도 같은 판정이다. 그래서 치울 때 비우지 않는다.
        Tabs.CanShow = CanShowHref;

        Menus.MenusChanged += OnMenusChanged;
        Navigation.LocationChanged += OnLocationChanged;
        _navigating = Navigation.RegisterLocationChangingHandler(OnNavigating);

        // 브레드크럼을 눌렀다. 사이드바가 접혀 있으면 **여기서 편다** —
        // 트리 쪽에서는 못 한다. 접힘은 이 레이아웃이 들고 있는 상태다.
        Reveal.Requested += OnMenuRevealRequested;

        // 헤더의 권한 그룹이 이 값으로 그려진다. 내 정보는 부트스트랩이
        // 끝나야 실리므로, 안 듣고 있으면 **그다음 렌더가 올 때까지 비어
        // 있다** — 메뉴를 한 번 누르기 전에는 로고 옆이 빈 채로 남는다.
        Me.Changed += OnMeChanged;

        // 환경설정 등에서 모바일 메뉴 단추 위치가 바뀌면 즉시 화면에 반영한다.
        Boot.FabPositionChanged += OnFabPositionChanged;
        Boot.FabHiddenChanged += OnFabHiddenChanged;
        Boot.BottomNavHiddenChanged += OnBottomNavHiddenChanged;
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
            "top-center" or "bottom-center" => HorizontalAlignment.Center,
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

    private void OnBottomNavHiddenChanged(bool hidden)
    {
        _bottomNavHidden = hidden;
        InvokeAsync(StateHasChanged);
    }

    private void OnMeChanged() => InvokeAsync(StateHasChanged);

    /// <summary>
    /// 브레드크럼이 「이 메뉴를 사이드바에서 보여 달라」고 했다.
    /// <b>이쪽이 할 일은 판을 펴는 것 하나다</b> — 어느 가지를 펼지는
    /// <c>SidebarMenu</c> 가 같은 부탁을 받아 처리한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 휴대폰에서도 편다. 그때 사이드바는 본문을 덮지만, 「메뉴를 보여
    /// 달라」고 누른 참이라 그것이 바라는 것이다 — 화면을 옮기면
    /// <c>OnLocationChanged</c> 가 다시 접는다.
    /// </para>
    /// <para>
    /// [아래 띠의 「메뉴」 칸만 다시 누르면 접는다]
    /// </para>
    /// <para>
    /// 그 칸이 보내는 것은 메뉴 경로가 아니라 <see cref="BottomNav.MenuPath"/>
    /// 라는 표시다(<c>MobileBottomNav.Go</c>). 그것은 <b>햄버거 단추와 같은
    /// 물건</b>이라 여닫이여야 한다 — 떠다니는 단추·로고가 이미 그렇게
    /// 동작하는데(<see cref="ToggleSidebarAsync"/>) 이 칸만 펴기만 하면,
    /// 판이 덮여 있는 채로 <b>방금 누른 그 자리를 다시 눌러도 아무 일이
    /// 없다.</b> 띠는 사이드바 위(z-index 1030)에 남아 있어서 눌리기는 한다.
    /// </para>
    /// <para>
    /// 브레드크럼이 보내는 것은 언제나 실제 메뉴 경로라 여기 걸리지 않는다
    /// (표시는 <c>#</c> 로 시작해 메뉴 경로와 절대 겹치지 않는다).
    /// <b>그쪽은 접으면 안 된다</b> — 「이 메뉴를 보여 달라」에 판을 닫는 것은
    /// 부탁의 반대다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// **토큰이 먼저다.** 게이트웨이를 부르려면 있어야 하고, 이 값은
    /// `AuthenticationStateProvider` 에서만 나오는데 그건 **Razor 컴포넌트
    /// 안에서만** 물을 수 있다. HTTP 핸들러에서 물으면 예외로 죽는다.
    ///
    /// <para>
    /// 그다음 권한표 · 메뉴 · 즐겨찾기 · 내정보를 <b>한 번의 왕복</b>으로
    /// 받는다(<see cref="PortalBootstrap"/>). 넷을 순차로 부르던 자리였고,
    /// 이 레이아웃은 <b>업무 모듈을 넘나들 때마다 새로 만들어지므로</b>
    /// 그 왕복이 화면 전환 횟수만큼 났다.
    /// </para>
    ///
    /// <para>
    /// 넷 사이의 순서 제약(권한표 → 메뉴 → 즐겨찾기 → 얼굴)은 없어진 것이
    /// 아니라 <see cref="PortalBootstrap"/> 안으로 옮겨 갔다. 부트스트랩이
    /// 안 되는 환경에서는 거기서 옛 방식으로 떨어진다.
    /// </para>
    /// </summary>
    /// <summary>
    /// 이 프로세스에서 레이아웃이 몇 번 만들어졌나. <b>아래 로그가 쓴다.</b>
    /// </summary>
    private static int _created;

    protected override async Task OnInitializedAsync()
    {
        // ── 이 줄이 C-4 를 판정한다 ───────────────────────────
        //
        // `web/CLAUDE.md` 와 성능 감사 문서가 「업무를 넘나들 때마다 레이아웃이
        // 통째로 다시 만들어진다」고 적어 두었고, 부트스트랩 통·참조자료 통·
        // PortalBoot 가 다 그 전제로 만들어졌다. 그런데 코드를 읽으면 그렇게
        // 될 이유가 안 보인다.
        //
        //   · Routes.razor 는 **기본** Router · AuthorizeRouteView 를 쓴다.
        //     Piral 의 MfRouter · MfRouteView 는 우리 렌더 트리에 없다.
        //   · 업무 모듈은 `@layout` 을 선언하지 않는다 — 전부 DefaultLayout 이라
        //     LayoutView 가 보는 Layout 타입이 바뀌지 않는다.
        //   · MapMicrofrontends<App> 은 MapRazorComponents 래퍼이고 App 을
        //     열쇠 붙은 부품으로 감싸지 않는다.
        //   · UseMicrofrontendContainers 는 DI 공장을 Autofac 으로 바꾸는 것이라
        //     렌더 트리를 건드리지 않는다.
        //
        // 그러면 레이아웃 인스턴스는 유지되어야 하고, 위 세 통은 **없어도 되는**
        // 것이 된다. 반대로 정말 다시 만들어진다면 원인이 아직 안 밝혀진 것이다.
        //
        // 읽는 방법: 로그인해서 사이드바로 업무를 두세 번 옮기고 이 로그를 본다.
        //   · 쪽을 새로 열 때만 찍힌다        → 유지된다. 뿌리 문제는 없다.
        //   · 업무를 옮길 때마다 숫자가 오른다 → 정말 다시 만들어진다.
        //
        // **판정이 끝나면 이 로그를 지운다.** 남겨 두면 평상시 로그가 된다.
        Log.LogInformation(
            "포털 레이아웃을 새로 만들었다 ({Count}번째, 주소 {Uri}). "
            + "업무를 옮길 때마다 이 숫자가 오르면 감사 문서의 C-4 가 사실이다.",
            Interlocked.Increment(ref _created),
            Navigation.Uri);

        var state = await AuthState.GetAuthenticationStateAsync();
        Tokens.Initialize(state.User);
        _userName = state.User.Identity?.Name;

        // 워터마크에 쓸 이름을 여기서 넘긴다. **JS 를 부르지 않는다** —
        // `PortalBoot` 가 잠금 표시·공지 표시·고정 탭·테마를 읽는
        // **같은 왕복에** 태워 보낸다. 워터마크가 무엇이고 왜 있는지는
        // OnAfterRenderAsync 머리말에 있다.
        //
        // **아래 `Bootstrap.LoadAsync` 보다 앞이어야 한다.** 그것은 게이트웨이를
        // 부르므로 반드시 한 번 양보하고, 그 순간 Blazor 가 중간 그림을 그리면서
        // 자식 부품이 생긴다. Blazor 는 `OnAfterRenderAsync` 를 **자식부터**
        // 부르므로, 뒤에 두면 브라우저 상태를 처음 읽는 사람이 `TabBar` 나
        // `NoticeAutoPopup` 이 되고 그 왕복에는 이름이 안 실린다.
        //
        // 그렇게 되어도 워터마크가 빠지지는 않는다 — `PortalBoot` 가 늦게 온
        // 이름을 혼자 한 번 걸어 준다. 다만 왕복이 하나 늘어난다.
        Boot.UseWatermark(_userName);

        await Bootstrap.LoadAsync(state.User);

        // 부트스트랩이 관리자가 정한 설정을 실어 온다. 그때 비로소 「이 사람은
        // 워터마크를 끈다」를 알 수 있어서, 위에서 한 번 깔아 둔 것을 여기서
        // 걷는다. **순서를 뒤집지 않는다** — 모르는 동안은 깔아 두는 쪽이
        // 맞다(빠지는 쪽이 사고다).
        await ApplyWatermarkAsync();

        // 관리자가 방금 바꿨을 수 있다. 30초마다 내 설정을 되묻는다 —
        // 워터마크는 **관리자가 켜면 그 사람 화면에 나타나야** 하는 것이라
        // 다음 로그인까지 기다릴 수 없다.
        StartWatermarkWatch();

        Track();
    }

    /// <summary>
    /// 브라우저에 남아 있던 것을 되살린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 잠금 표시는 브라우저에 남는다(localStorage — <c>ScreenLock</c> 머리말).
    /// 새로고침해도, <b>PWA 앱을 껐다 켜도</b> 덮개가 다시 덮이게 하려면 회로가
    /// 붙은 <b>뒤에</b> 읽어야 한다 — 프리렌더 중에는 JS 를 부를 수 없다.
    /// </para>
    ///
    /// <para>
    /// <b>워터마크를 여기서 걸지 않는다.</b> 로그인 아이디를 화면 위에 옅게
    /// 깔아 <b>찍힌 사진에서 누구 화면인지 드러나게</b> 하는 것이고(촬영을
    /// 막으려는 것이 아니다 — 막을 수 없다), 옛 포털도 같은 이유로 켜 두었다.
    /// 여전히 걸리지만 <b>왕복을 따로 내지 않는다</b> — 이름을
    /// <see cref="OnInitializedAsync"/> 에서 <see cref="PortalBoot"/> 에 넘겨
    /// 두고, 그것이 잠금 표시·공지 표시·고정 탭·테마를 읽는 <b>같은 왕복</b>에
    /// 태워 보낸다.
    /// </para>
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // **본문이 갈린 그림에서만 알린다** (`_bodyChanged`).
        //
        // 아무 그림에서나 알리면 표시가 그 자리에서 걷힌다. 주소가 바뀌면
        // 그림이 **두 번** 그려지기 때문이다 — 탭·브레드크럼을 맞추려고
        // 여기서 한 번(`OnLocationChanged`), 라우터가 새 화면을 끼우며 한 번.
        // 앞엣것에는 새 화면이 아직 없어서 잡은 조회도 없고, 그래서 곧바로
        // 끝나 버린다. 실제로 그렇게 만들어 놓고 재 보니 켜졌다 꺼지는 데
        // 35ms 였다 — 사람 눈에는 아무 일도 안 일어난 것과 같다.
        //
        // 뒤엣것에서는 이 콜백이 **자식보다 나중**에 불리므로(Blazor 가 자식을
        // 먼저 부른다) 새 화면의 조회가 이미 잡혀 있다.
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

    /// <summary>
    /// 지난번에 끌어 둔 사이드바 폭을 되살린다.
    ///
    /// <para>
    /// <b>왕복을 따로 내지 않는다</b> — <see cref="PortalBoot"/> 가 잠금 표시·
    /// 공지 표시·고정 탭·테마를 읽는 그 한 번에 실려 온다(열쇠를 그쪽 목록에
    /// 한 줄 더한 것이 전부다).
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>첫 그림은 기본 폭(330px)이고</b> 회로가 붙은 뒤 저장된 폭으로
    /// 한 번 바뀐다. 고정 탭도 같은 자리에서 같은 방식으로 되살아난다.
    /// 첫 그림부터 맞추려면 값이 쿠키에 있어야 하는데(테마 크기가 그렇다)
    /// 그건 theme.js 가 굽는 것들과 한 묶음으로 다룰 일이다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 저장해 둔 모바일 메뉴 단추 위치를 되살린다.
    /// <see cref="PortalBoot"/> 의 단일 왕복으로 읽어 오며, 변경 사항이 있으면 화면을 갱신한다.
    /// </summary>
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

        if (state.BottomNavHidden != _bottomNavHidden)
        {
            _bottomNavHidden = state.BottomNavHidden;
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

    /// <summary>
    /// 새 화면이 끼워졌다. 본문이 갈렸는지 보고, 화면 크기에 맞춘 일
    /// (<see cref="ApplyViewport"/>)을 한 번 더 한다.
    ///
    /// Vue 에서는 `matchMedia` 를 직접 구독했다. Blazor Server 는 브라우저 상태를
    /// 모르므로 DevExpress 의 DxLayoutBreakpoint 가 알려 주는 것을 받아 옮긴다.
    /// 경계값(767 / 1023)은 Vue 때와 같아야 한다 — 사용자가 같은 기기에서
    /// 다른 메뉴를 보면 안 된다.
    /// </summary>
    protected override void OnParametersSet()
    {
        // 본문이 갈렸는가. 라우터가 새 화면을 끼울 때만 참조가 바뀐다 —
        // 까닭은 `OnAfterRenderAsync` 에 적어 두었다.
        if (!ReferenceEquals(_body, Body))
        {
            _body = Body;
            _bodyChanged = true;
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

    /// <summary>
    /// 지금 화면 크기에 맞춰 <b>메뉴를 거르고, 휴대폰이면 사이드바를 접는다.</b>
    ///
    /// <para>
    /// 부르는 자리가 둘이다 — 화면이 갈릴 때(<c>OnParametersSet</c>)와
    /// <b>화면 크기가 갈릴 때</b>(위 두 갈고리). 뒤쪽이 없으면 휴대폰으로
    /// 처음 들어온 사람이 메뉴를 한 번 누르기 전까지 덮개를 걷지 못한다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 헤더를 눌렀다. <b>휴대폰에서 메뉴가 펴져 있으면 그것부터 닫는다.</b>
    /// 누른 단추가 하던 일은 막지 않는다 — 함께 일어난다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 넓은 화면에서는 아무 일도 하지 않는다. 그때 사이드바는 덮개가 아니라
    /// <b>늘 자리를 차지하는 판</b>이라 본문과 겹치지 않고, 헤더를 눌렀다고
    /// 접히면 폭을 끌어 맞춰 둔 사람의 자리가 사라진다.
    /// </para>
    /// </remarks>
    private void OnHeaderClick()
    {
        if (_isPhone && _sidebarOpen)
        {
            CloseSidebar();
        }
    }

    /// <summary>
    /// 로고를 누르면 <b>휴대폰에서는 메뉴를 여닫는다</b>(넓은 화면에서는 하던
    /// 대로 <c>/</c> 로 간다 — 기본 이동을 막는 것도 휴대폰일 때뿐이다).
    /// </summary>
    /// <remarks>
    /// <b>떠다니는 단추를 감춘 사람에게는 이것이 유일하게 여는 길이다.</b>
    /// 그래서 감출 때 헤더의 ☰ 를 되돌리지 않는다(<c>_fabHidden</c>).
    /// </remarks>
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

    /// <summary>
    /// 사이드바를 편다. <b>휴대폰이면 지금까지의 뒤로 가기 횟수를 적어 둔다</b> —
    /// 이다음 이동이 뒤로 가기인지는 그 수가 늘었는지로 가린다
    /// (<see cref="WentBackAsync"/>).
    /// </summary>
    /// <remarks>
    /// 펴는 길이 둘(☰ · 브레드크럼)이라 여기 한 곳으로 모은다. 한쪽에서
    /// 적기를 빠뜨리면 <b>그쪽으로 연 메뉴만</b> 뒤로 가기로 안 닫히고,
    /// 그런 어긋남은 화면만 봐서는 까닭을 알 수 없다.
    /// </remarks>
    private async Task OpenSidebarAsync()
    {
        _sidebarOpen = true;

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

    /// <summary>
    /// 사이드바를 접는다. <b>적어 둔 뒤로 가기 횟수도 함께 버린다</b> —
    /// 남겨 두면 다음에 편 메뉴에서 첫 이동이 뒤로 가기로 읽힌다.
    /// </summary>
    private void CloseSidebar()
    {
        _sidebarOpen = false;
        _popsAtOpen = null;
    }

    /// <summary>
    /// 스플리터의 접기 화살표(구분선 가운데)로 접었다 폈다.
    ///
    /// <para>
    /// 헤더의 ☰ 와 <b>같은 상태를 나눠 쓴다.</b> 따로 두면 화살표로 접고
    /// ☰ 를 누를 때 아무 일도 안 일어나는 것처럼 보인다 — 부품은 접혀 있다고
    /// 알고 우리는 펴져 있다고 알기 때문이다.
    /// </para>
    /// </summary>
    /// <remarks>
    /// 휴대폰에서는 이 이벤트가 오지 않는다 — 구분선을 감춰 두어(app.css)
    /// 끌 자리도 화살표도 없다. 그래서 여기서는 뒤로 가기 횟수를 적지 않고,
    /// 접을 때 버리기만 한다.
    /// </remarks>
    private void OnSidebarCollapsedChanged(bool collapsed)
    {
        if (collapsed)
        {
            CloseSidebar();
            return;
        }

        _sidebarOpen = true;
    }

    /// <summary>
    /// 끌어서 폭을 바꿨다. 브라우저에 적어 둔다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 넘어오는 값은 <c>"312px"</c> 처럼 <b>단위가 붙은 글자</b>다. 숫자만
    /// 떠서 담는다 — 저장한 값을 다음에 읽어 다시 <c>px</c> 를 붙이므로,
    /// 단위째 담아 두면 <c>"312pxpx"</c> 가 되는 자리가 생긴다.
    /// </para>
    ///
    /// <para>
    /// <b>접힌 상태는 담지 않는다.</b> 접으면 부품이 0 을 알려 주는데 그것을
    /// 담으면 다음에 열 때 폭 0 인 사이드바가 나온다 — 접힘은 접힘으로
    /// 기억할 일이고(지금은 기억하지 않는다) 폭과 섞지 않는다.
    /// </para>
    /// </remarks>
    private async Task OnSidebarResizedAsync(string? size)
    {
        if (!Pixels(size, out var px))
        {
            return;
        }

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

    /// <summary>
    /// <c>"312px"</c> 에서 312 를 뜬다. 0 이하나 못 읽는 값은 거른다 —
    /// 접힘(0)과 이상한 값을 여기서 함께 막는다.
    /// </summary>
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

    private void OnMenusChanged()
    {
        // 메뉴가 늦게 도착하면 그때 브레드크럼과 탭 이름이 채워진다.
        Track();
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// 옮기기 <b>전에</b> 표시를 켠다. Vue 의 <c>router.beforeEach</c> 자리다.
    ///
    /// <para>
    /// <b><c>LocationChanged</c> 로는 늦는다.</b> 그 이벤트의 구독자 중
    /// <c>Router</c> 가 우리보다 먼저다(앱이 뜰 때 붙으므로). 라우터는 자기
    /// 차례에 새 화면을 <b>그 자리에서</b> 만들어 버리고, 그 화면이 조회를
    /// 잡으려 할 때 표시는 아직 켜지지도 않은 상태다. 잡히지 않은 조회는
    /// 세지 않으므로(<see cref="PageTransition.Claim"/>) 표시는 곧바로 걷힌다.
    /// 재 보니 켜졌다 꺼지는 데 1ms 였다.
    /// </para>
    ///
    /// <para>
    /// 이 갈고리는 라우터가 움직이기 전에 불린다. <b>딱 한 가지 이동만
    /// 막는다</b> — 휴대폰에서 메뉴가 펴져 있을 때의 뒤로 가기다(아래).
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>휴대폰에서 메뉴가 펴져 있으면 뒤로 가기를 한 번 삼킨다.</b>
    /// 덮개를 걷는 것이 먼저고, 한 번 더 누르면 그때 옮겨 간다.
    /// </para>
    ///
    /// <para>
    /// 뒤로 가기인지는 <see cref="WentBackAsync"/> 가 가린다. <b>이 자리의
    /// 값만으로는 못 가린다</b> — 뒤로 가기와 코드가 낸 이동이 여기서는
    /// 똑같이 생겼다(<c>IsNavigationIntercepted</c> 가 둘 다 거짓이다).
    /// 링크 클릭과만 갈리므로, 그것으로 가르면 <b>헤더의 알림·사용자 메뉴가
    /// 메뉴 펴진 동안 안 먹는다</b> — 그 둘이 코드로 옮기는 자리다.
    /// </para>
    ///
    /// <para>
    /// 묻는 것은 <b>휴대폰에서 메뉴가 펴져 있을 때뿐</b>이다. 나머지 이동은
    /// 예전처럼 아무것도 기다리지 않는다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 지금 일어난 이동이 <b>뒤로 가기인가.</b> 메뉴를 편 뒤로 뒤로 가기
    /// 횟수가 늘었으면 그렇다.
    /// </summary>
    /// <remarks>
    /// 못 물었으면 <c>false</c> 다 — 막지 않는 쪽으로 진다. 메뉴가 안 닫히는
    /// 것과 눌러도 화면이 안 열리는 것은 무게가 다르다.
    /// </remarks>
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

    /// <summary>
    /// 지금 주소를 보고 탭·브레드크럼·즐겨찾기 열쇠를 맞춘다.
    /// <b>볼 권한이 없는 화면이면 탭을 열지 않고 본문을 막는다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// [2026-09-24 — 권한 없는 화면이 열려 있었다]
    /// </para>
    /// <para>
    /// 권한은 사이드바를 거를 때만 봤다(<c>MenuFilter</c>). 그런데 <see cref="MenuProvider.Trail"/>
    /// 은 <b>거르기 전</b> 메뉴를 훑으므로, 주소로 들어오면 권한이 없어도 메뉴를 찾아
    /// 탭을 열고 화면을 그렸다. 브라우저에 적어 둔 고정 탭이 그 길로 매번 되살아났다
    /// (조직도 권한이 없는 사용자에게 「조직도」 고정 탭).
    /// </para>
    /// <para>
    /// 메뉴에 없는 화면(탭으로 여는 상세 화면 · 진단 화면)은 권한표에 줄이 없으므로
    /// 여기서 막지 않는다. 그런 화면은 <c>[Authorize]</c> 와 서버가 지킨다.
    /// </para>
    /// </remarks>
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

        // 로그인·오류 화면은 탭으로 만들지 않는다. 메뉴에 없는 화면도 마찬가지다 —
        // 진단 화면과 "준비 중" 안내가 탭을 채우면 정작 업무 탭이 밀려난다.
        if (_trail.Count > 0 && !_denied)
        {
            Tabs.Open(href, _trail[^1].Title);
        }

        // 권한표·메뉴는 늦게 오므로 이미 되살아난 고정 탭이 있을 수 있다.
        Tabs.Prune();
    }

    /// <summary>
    /// 이 메뉴의 화면을 열 수 있는가. 사이드바를 거르는 규칙(<c>MenuFilter.IsOwnScreen</c>)과 같다.
    /// </summary>
    /// <remarks>
    /// <b>권한표를 받기 전에는 막지 않는다</b>(<see cref="IPermissionContext.IsLoaded"/>) —
    /// 막으면 새로고침할 때마다 「권한이 없습니다」가 한 번 스친다. 받고 나면 다시 따진다
    /// (<see cref="OnMenusChanged"/>).
    /// </remarks>
    private bool MayOpen(MenuNode node)
    {
        if (!Permissions.IsLoaded || node.IsCatalog || node.IsExternalLink || string.IsNullOrEmpty(node.Path))
        {
            return true;
        }

        return Permissions.CanView(node.Path);
    }

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

    /// <summary>
    /// 내 설정을 되묻는 시계. 관리자가 워터마크를 켜고 끄는 것을 이것이 옮긴다.
    ///
    /// <para>
    /// <b>업무를 옮길 때 레이아웃이 새로 만들어진다</b>(이 파일 머리말). 그때
    /// 옛 레이아웃은 <see cref="Dispose"/> 로 이 시계를 끄고 새 레이아웃이
    /// 자기 것을 켠다 — 시계가 쌓이지 않는다.
    /// </para>
    /// </summary>
    private Timer? _watermarkWatch;

    /// <summary>
    /// 되묻는 간격.
    ///
    /// <para>
    /// 30초다. 더 짧게 두면 탭마다 게이트웨이 호출이 늘고, 더 길게 두면
    /// 관리자가 켠 뒤 「왜 안 나오지」 하고 다시 눌러 보게 된다. 이 왕복은
    /// 헤더가 이미 쓰는 내 정보 조회 하나뿐이라(이름·사진·권한 그룹도 함께
    /// 새로워진다) 따로 통로를 두지 않았다.
    /// </para>
    /// </summary>
    private static readonly TimeSpan WatermarkWatchInterval = TimeSpan.FromSeconds(30);

    private void StartWatermarkWatch()
    {
        _watermarkWatch?.Dispose();

        _watermarkWatch = new Timer(
            // 회로 밖 스레드에서 온다. `InvokeAsync` 로 회로에 들어가야
            // JS 호출과 렌더가 안전하다.
            _ => _ = InvokeAsync(RefreshWatermarkAsync),
            null,
            WatermarkWatchInterval,
            WatermarkWatchInterval);
    }

    private async Task RefreshWatermarkAsync()
    {
        // 실패는 삼킨다. 게이트웨이가 잠깐 안 되는 것과 워터마크가 틀린 것의
        // 무게가 다르다 — 못 물으면 지금 상태를 그대로 둔다.
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
        Reveal.Requested -= OnMenuRevealRequested;
        _navigating?.Dispose();
        Me.Changed -= OnMeChanged;
        Boot.FabPositionChanged -= OnFabPositionChanged;
        Boot.FabHiddenChanged -= OnFabHiddenChanged;
        Boot.BottomNavHiddenChanged -= OnBottomNavHiddenChanged;
        Boot.ToastPositionChanged -= OnToastPositionChanged;
        _watermarkWatch?.Dispose();
    }
}
