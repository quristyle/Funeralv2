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

    /// <summary>
    /// 내 정보 묶음. 전부 <c>/admin/profile</c> 의 탭이다 — 그 화면이
    /// <c>?tab=</c> 를 받으므로 목록에서 곧장 그 자리를 열 수 있다.
    /// </summary>
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

    /// <summary>
    /// 「개인화」 묶음에 세울 화면 하나. <b>주소를 적지 않는다</b> —
    /// <see cref="RouteKey"/> 로 메뉴를 찾고, 가는 주소도 이름도 그 마디가 준다
    /// (<see cref="MenuLookup"/> 머리말).
    /// </summary>
    /// <param name="RouteKey">DB <c>system_menus.route_key</c>.</param>
    /// <param name="Paths">
    /// 열쇠를 아직 안 채운 DB 를 위한 대비책. 이행 중에는 옛 경로와 새 경로가
    /// 둘 다 쓰일 수 있어 여럿을 받는다.
    /// </param>
    /// <param name="Label">
    /// 메뉴 이름 대신 쓸 글. <b>거의 비운다</b> — 메뉴에서 이름을 고쳤을 때
    /// 여기만 옛 이름으로 남지 않아야 한다.
    /// </param>
    private sealed record Personal(string RouteKey, string[] Paths, string? Label = null);

    /// <summary>
    /// 「개인화」 묶음 — <b>사람마다 값이 다른 화면</b>들이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 전체 메뉴를 훑어 고른 잣대는 하나다: <b>보이는 것이 사람마다 다른가.</b>
    /// 「내 …」 라고 부르는 화면과 제 설정을 고치는 화면이 여기 든다. 업무
    /// 자료를 보는 화면은 권한이 같으면 누구에게나 같은 것이 보이므로 뺐다.
    /// </para>
    ///
    /// <para>
    /// <b>모듈이 섞여 있다.</b> 장례식장(환경설정 · 업무 설정 · 나의정보) ·
    /// 포털관리(쪽지함 · 내 알림함 · 내 이동경로) · 헬프데스크(내 댓글) ·
    /// 운송관리(내 정보 · 나의 차량등록). 셸은 업무 모듈을 타입으로 알지
    /// 못하므로(web/CLAUDE.md) 열쇠 글자로 적는 수밖에 없다 — 그래도 주소를
    /// 박아 두는 것보다는 낫다. 못 찾으면 그 줄이 그냥 빠진다.
    /// </para>
    ///
    /// <para>
    /// [<b>걸러진 목록이 아니라 원본에서 찾고, 권한은 따로 묻는다</b>]
    /// </para>
    ///
    /// <para>
    /// 헤더의 ⚡ 는 <c>VisibleMenus</c> 에서 찾는다 — 사이드바로 가는 단추라
    /// 목록에서 빠지면 함께 사라지는 것이 맞다. 이 묶음은 다르다. 거르기는
    /// <b>사이드바를 정리하는 규칙</b>(<c>hide_in_menu</c> · <c>use_mobile</c>)
    /// 이라, 그것을 그대로 따르면 <b>휴대폰에서 개인화 항목이 통째로 사라진다</b> —
    /// 이 판을 가장 많이 쓰는 자리가 거기다. 그래서 <c>AllMenus</c> 에서 찾고
    /// 묻는 것은 <b>열람 권한 하나</b>다(<c>AiChatDrawer.ChatMenu</c> 와 같은 선).
    /// </para>
    /// </remarks>
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

        // **화면을 옮기면 판을 접는다.** 항목으로 가는 길은 `GoTo` 가 이미
        // 접지만, 판을 펴 둔 채로 **헤더의 다른 단추**를 눌러 옮겨 가는 길이
        // 있다(휴대폰의 ⚡ 는 화면으로 간다). 그때 안 접으면 새 화면 위에
        // 이 판이 그대로 덮인 채 도착한다 — 휴대폰에서는 그것이 화면 전체다.
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

    /// <summary>사진이 없을 때 동그라미에 넣을 글자 한 자.</summary>
    private string Initial(AuthenticationState state)
    {
        if (Me.IsLoaded)
        {
            return Me.Initial;
        }

        // 아직 못 읽었으면 쿠키 클레임의 이름으로 대신한다. 헤더가 잠깐
        // 「?」로 보이는 것보다 낫다.
        var name = state.User.Identity?.Name;
        return string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
    }

    private void Close() => _open = false;

    /// <summary>
    /// 항목을 눌렀을 때. <b>먼저 닫고 옮긴다</b> — 같은 화면 안의 다른 탭으로
    /// 갈 때는 판이 저절로 사라지지 않는다.
    /// </summary>
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
        // **한동안 이 줄이 조용히 아무 일도 하지 않았다.** 아래처럼 적어
        // 두었는데 Blazor 는 식별자를 `.` 으로 쪼개므로
        // `getElementById('jsini-logout-form')` 라는 이름의 속성을 찾고 던진다.
        //
        //     Js.InvokeVoidAsync("document.getElementById('jsini-logout-form').submit")
        //
        // 예외가 브라우저 콘솔에만 남아 **누르면 반응이 없는 상태**로 있었다.
        // 인자를 받는 함수로 부르면 그 쪼개기에 걸릴 것이 없다(theme.js).
        return Js.InvokeVoidAsync("jsiniForm.submit", "jsini-logout-form").AsTask();
    }
}
