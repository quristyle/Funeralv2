using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using JSini.Web.Components.Security;

namespace JSini.Web.Components.Layout;

public partial class UserMenu
{
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ScreenLock Lock { get; set; } = default!;
    [Inject] private ThemeDrawer Theme { get; set; } = default!;
    [Inject] private UserMenuDrawer UserDrawer { get; set; } = default!;
    [Inject] private CurrentUser Me { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IPermissionContext Permissions { get; set; } = default!;

    /// <summary>펼친 판의 항목 하나.</summary>
    private sealed record Item(string Label, string Href, string Icon);

    /// <summary>내 정보 묶음. 전부 /admin/profile 의 탭이다 — 그 화면이 ?tab= 를 받으므로 목록에서 곧장 그 자리를 연다.</summary>
    private static readonly Item[] ProfileItems =
    [
        new("내 정보", "/admin/profile?tab=basic", "jsini-icon-user"),
        new("계정 정보 · 접속 기록", "/admin/profile?tab=account", "jsini-icon-eye"),
        new("프로필 사진", "/admin/profile?tab=avatar", "jsini-icon-edit"),
        new("고정탭 관리", "/admin/profile?tab=fixedTabs", "jsini-icon-star"),
        new("보안 설정", "/admin/profile?tab=security", "jsini-icon-lock"),

        // 「알림」 묶음에 혼자 서 있던 줄이다. **이것도 프로필 탭이라**
        // 묶음을 따로 둘 까닭이 없었다 — 그 묶음의 다른 한 줄(알림 설정)은
        // 장례식장 모듈의 화면이라 아래 「개인화」로 내려갔다.
        new("새 메시지 알림", "/admin/profile?tab=notice", "jsini-icon-bell"),

        // 비밀번호만 프로필 밖이다. 바꾼 뒤 재로그인까지 시켜야 해서 셸에
        // 전용 화면이 있다(Profile.razor 머리말).
        new("비밀번호 변경", "/password/change", "jsini-icon-lock"),
    ];

    /// <summary>「개인화」 묶음에 세울 화면 하나. 주소는 적지 않는다 — RouteKey 로 메뉴를 찾고 가는 주소도 이름도 그 마디가 준다(MenuLookup 머리말).</summary>
    /// <param name="RouteKey">DB system_menus.route_key.</param>
    /// <param name="Paths">열쇠를 아직 안 채운 DB 를 위한 대비책. 이행 중에는 옛 경로와 새 경로가 둘 다 쓰일 수 있어 여럿을 받는다.</param>
    /// <param name="Label">메뉴 이름 대신 쓸 글. 거의 비운다 — 메뉴에서 이름을 고쳤을 때 여기만 옛 이름으로 남지 않아야 한다.</param>
    private sealed record Personal(string RouteKey, string[] Paths, string? Label = null);

    /// <summary>「개인화」 묶음 — 사람마다 보이는 것이 다른 화면들이다. 잣대는 하나: 보이는 것이 사람마다 다른가. 내 설정을 고치는 화면과 「내 …」 화면이 든다. 업무 자료를 보는 화면은 권한이 같으면 누구에게나 같아서 뺀다. 모듈이 섞여 있어 셸은 타입으로 알 수 없고 열쇠 글자로 적는다 — 못 찾으면 그 줄이 빠진다.
    /// 헤더의 ⚡ 는 VisibleMenus 에서 찾지만 이 묶음은 AllMenus 에서 찾고 권한 하나만 따로 묻는다 — 사이드바 정리 규칙(hide_in_menu · use_mobile)을 따르면 휴대폰에서 개인화 항목이 통째로 사라지는데 이 판을 가장 많이 쓰는 곳이 거기다.</summary>
    private static readonly Personal[] PersonalRoutes =
    [
        // 개인 환경설정. **테마가 아니다** — 알림(받을 것인가 · 이 기기로 받을
        // 것인가) · 토스트 자리 · 떠다니는 단추 자리를 여기서 고른다
        // (EnvironmentSettingPage 머리말). 테마는 아래 「화면」 묶음이다.
        new("funeral.setting.environment", ["/setting/environment", "/funeral/setting/environment"]),
        new("funeral.setting.work-options", ["/setting/work-options", "/funeral/setting/work-options"]),
        new("funeral.info.my-info", ["/info/my-info", "/funeral/info/my-info"]),

        new("admin.note.box", ["/admin/note/box"]),
        new("admin.push.history", ["/system/push/history", "/admin/push/history"]),
        new("admin.location.my-track", ["/admin/location/my-track"]),

        new("helpdesk.request.my-comments", ["/helpdesk/request/my-comments"]),

        // **이 하나만 이름을 덮어쓴다.** 메뉴 이름이 「내 정보」라 바로 위
        // 「내 정보」 묶음의 첫 줄과 글자가 같아진다 — 한 판 안에 같은 이름이
        // 둘이면 어느 쪽이 무엇인지 눌러 봐야 안다.
        new("cargotrust.me", ["/cargotrust/me"], "운송관리 내 정보"),
        new("cargotrust.vehicles", ["/cargotrust/vehicles"]),
    ];

    /// <summary>
    /// 지금 사람에게 실제로 세울 개인화 항목. 못 찾은 줄과 권한 없는 줄은 빠진다.
    /// </summary>
    private IEnumerable<(MenuNode Node, string Label)> PersonalLinks =>
        PersonalRoutes
            .Select(p => (Node: MenuLookup.Find(Menus.AllMenus, p.RouteKey, p.Paths), p.Label))
            .Where(x => x.Node is not null && Permissions.CanView(x.Node!.Path))
            .Select(x => (x.Node!, x.Label ?? x.Node!.Title));

    private bool _open;

    protected override void OnInitialized()
    {
        Me.Changed += OnMeChanged;
        UserDrawer.OpenRequested += OnDrawerRequested;
        UserDrawer.ToggleRequested += OnDrawerToggleRequested;

        // 권한표·메뉴는 이 부품이 만들어진 뒤에 실린다. 안 듣고 있으면
        // 「개인화」 묶음이 로그인한 그 순간의 빈 목록에 붙박인다.
        Menus.MenusChanged += OnMenusChanged;

        // 화면을 옮기면 판을 접는다 — 판을 펴 둔 채 헤더의 다른 단추를 눌러 옮겨 가면 새 화면 위에 이 판이 덮인 채 도착한다(휴대폰에서는 화면 전체다).
        Navigation.LocationChanged += OnLocationChanged;
    }

    public void Dispose()
    {
        Me.Changed -= OnMeChanged;
        UserDrawer.OpenRequested -= OnDrawerRequested;
        UserDrawer.ToggleRequested -= OnDrawerToggleRequested;
        Menus.MenusChanged -= OnMenusChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }

    private void OnMenusChanged() => InvokeAsync(StateHasChanged);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        InvokeAsync(StateHasChanged);
    }

    private void OnDrawerRequested(bool open)
    {
        if (_open != open)
        {
            _open = open;
            InvokeAsync(StateHasChanged);
        }
    }

    private void OnDrawerToggleRequested()
    {
        _open = !_open;
        InvokeAsync(StateHasChanged);
    }

    private void OnMeChanged() => InvokeAsync(StateHasChanged);

    /// <summary>얼굴 동그라미가 첫 글자를 딸 이름. 아직 못 읽었으면 쿠키 클레임의 이름으로 대신한다 — 헤더가 잠깐 ? 로 보이는 것보다 낫다.</summary>
    private string? FaceName(AuthenticationState state) =>
        Me.IsLoaded ? Me.DisplayName ?? Me.Username : state.User.Identity?.Name;

    private void Close() => _open = false;

    /// <summary>항목을 눌렀을 때 먼저 닫고 옮긴다 — 같은 화면 안의 다른 탭으로 갈 때는 판이 저절로 사라지지 않는다.</summary>
    private void GoTo(string href)
    {
        Close();
        Navigation.NavigateTo(href);
    }

    private void OpenTheme()
    {
        Close();
        Theme.Open();
    }

    private Task LockAsync()
    {
        Close();
        return Lock.LockAsync();
    }

    private Task LogoutAsync()
    {
        Close();
        // 이 줄은 한동안 아무 일도 하지 않았다 — Blazor 가 식별자를 점으로 쪼개 getElementById('jsini-logout-form') 라는 속성을 찾아 던졌다. 인자를 받는 함수로 부르면 쪼개기에 걸릴 것이 없다(theme.js). 예외가 콘솔에만 남아 누르면 반응이 없었다.
        return Js.InvokeVoidAsync("jsiniForm.submit", "jsini-logout-form").AsTask();
    }
}
