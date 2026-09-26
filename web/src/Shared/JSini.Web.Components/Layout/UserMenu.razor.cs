using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
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

        // 비밀번호만 프로필 밖이다. 바꾼 뒤 재로그인까지 시켜야 해서 셸에
        // 전용 화면이 있다(Profile.razor 머리말).
        new("비밀번호 변경", "/password/change", "jsini-icon-lock"),
    ];

    private static readonly Item[] NoticeItems =
    [
        // **장례식장 모듈의 화면이다.** 포털관리에 같은 판을 여는 「알림 설정」이
        // 따로 있었는데, 사이드바 「설정」 묶음에 거의 같은 화면 둘이 나란히
        // 걸려 있어 환경설정 하나로 합쳤다(그 화면 머리말).
        new("알림 설정 (푸시 · 이메일)", "/funeral/setting/environment", "jsini-icon-bell"),
        new("새 메시지 알림", "/admin/profile?tab=notice", "jsini-icon-bell"),
    ];

    private bool _open;

    protected override void OnInitialized()
    {
        Me.Changed += OnMeChanged;
        UserDrawer.OpenRequested += OnDrawerRequested;
        UserDrawer.ToggleRequested += OnDrawerToggleRequested;
    }

    public void Dispose()
    {
        Me.Changed -= OnMeChanged;
        UserDrawer.OpenRequested -= OnDrawerRequested;
        UserDrawer.ToggleRequested -= OnDrawerToggleRequested;
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
