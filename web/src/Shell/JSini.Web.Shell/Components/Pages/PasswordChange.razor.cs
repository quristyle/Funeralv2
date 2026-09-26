using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using JSini.Web.Http;
using JSini.Web.Components.Layout;

namespace JSini.Web.Shell.Components.Pages;

public partial class PasswordChange
{
    [Inject] private GatewayClient Gateway { get; set; } = default!;
    [Inject] private ITokenStore Tokens { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<PasswordChange> Log { get; set; } = default!;

    private string? _old;
    private string? _new;
    private string? _confirm;

    /// <summary>바꾸기에 성공했는가. 참이면 폼 대신 재로그인 단추를 보인다.</summary>
    private bool _changed;

    /// <summary>만료되어 끌려온 사람인가. 참이면 빠져나갈 링크를 감춘다.</summary>
    private bool _forced;

    /// <summary>바꾸는 중인가. 두 번 눌러 같은 요청을 두 번 보내지 않게 막는다.</summary>
    private bool _busy;

    /// <summary>바꾸는 중이거나 아직 회로가 안 붙었으면 단추를 잠근다.</summary>
    private bool Blocked => _busy || !RendererInfo.IsInteractive;

    /// <summary>방금 한 일의 결과. 토스트를 안 쓰는 이유는 머리말에 있다.</summary>
    private string? _notice;

    private NoticeTone _tone = NoticeTone.Info;

    private string? _expiryText;
    private NoticeTone _expiryTone = NoticeTone.Info;

    /// <summary>
    /// 토큰을 챙기고 만료 안내를 읽는다. <b>순서가 중요하다</b> —
    /// 토큰을 넘기기 전에 게이트웨이를 부르면 401 이 난다.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        Tokens.Initialize(state.User);

        await LoadExpiryAsync();
    }

    /// <summary>
    /// 「며칠 지났는지 · 며칠 남았는지」를 읽어 안내 줄을 만든다.
    ///
    /// <para>
    /// <c>auth/user/info</c> 는 만료 상태에서도 열려 있는 몇 안 되는 경로다.
    /// 그래도 실패를 삼키는 이유는, 안내를 못 띄우는 것과 비밀번호를 못 바꾸는
    /// 것의 무게가 다르기 때문이다. <b>안내가 없어도 바꾸는 일은 되어야 한다.</b>
    /// </para>
    /// </summary>
    private async Task LoadExpiryAsync()
    {
        PasswordStatus? status;

        try
        {
            status = await Gateway.GetOneAsync<PasswordStatus>("auth/user/info");
        }
        catch (ApiException ex)
        {
            Log.LogWarning(ex, "비밀번호 만료 안내를 읽지 못했습니다.");
            return;
        }

        if (status is null)
        {
            return;
        }

        if (status.PasswordExpired)
        {
            _forced = true;
            _expiryTone = NoticeTone.Error;
            _expiryText = status.PasswordExpiryDays is int days
                ? $"비밀번호를 바꾼 지 {days}일이 지났습니다. 지금 바꿔야 다른 화면을 쓸 수 있습니다."
                : "비밀번호 사용 기간이 지났습니다. 지금 바꿔야 다른 화면을 쓸 수 있습니다.";
        }
        else if (status.PasswordDaysRemaining is int left && left <= 7)
        {
            // 이레는 Vue 때와 같은 값이다. 미리 알려 주지 않으면 어느 날
            // 갑자기 막히는 것으로 보인다.
            _expiryTone = NoticeTone.Warning;
            _expiryText = $"비밀번호 사용 기간이 {left}일 남았습니다.";
        }
    }

    /// <summary>
    /// 비밀번호를 바꾼다. 서버로 가기 전에 세 가지를 먼저 본다.
    /// </summary>
    private async Task SubmitAsync()
    {
        if (string.IsNullOrEmpty(_old) || string.IsNullOrEmpty(_new))
        {
            Warn("지금 비밀번호와 새 비밀번호를 모두 넣으십시오.");
            return;
        }

        if (!string.Equals(_new, _confirm, StringComparison.Ordinal))
        {
            Warn("새 비밀번호와 확인이 다릅니다.");
            return;
        }

        if (string.Equals(_old, _new, StringComparison.Ordinal))
        {
            // 서버도 거부한다. 여기서 한 번 더 보는 이유는, 90일마다 바꾸라면서
            // 같은 값을 받아 주면 정책이 아무 일도 하지 않는 셈이기 때문이다.
            Warn("지금 쓰는 비밀번호와 다른 값으로 넣으십시오.");
            return;
        }

        _notice = null;
        _tone = NoticeTone.Info;
        _busy = true;

        try
        {
            await Gateway.PostAsync(
                "auth/user/change-password",
                new { oldPassword = _old, newPassword = _new });

            _changed = true;
            _old = null;
            _new = null;
            _confirm = null;

            // 만료 안내를 걷는다. 방금 바꿨으니 더 이상 사실이 아니고,
            // 남겨 두면 「바꿨습니다」 바로 위에 「기간이 지났습니다」가
            // 붙어 바뀐 것인지 아닌지 알 수 없게 된다.
            _expiryText = null;
        }
        catch (ApiException ex)
        {
            _notice = $"비밀번호를 바꾸지 못했습니다 — {ex.Message}";
            _tone = NoticeTone.Error;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>서버까지 가지 않고 막은 것. 화면에 남겨 둔다.</summary>
    private void Warn(string text)
    {
        _notice = text;
        _tone = NoticeTone.Warning;
    }

    /// <summary>
    /// 로그아웃 폼을 제출해 로그인 화면으로 보낸다. 왜 그냥 두면 안 되는지는
    /// 머리말의 「바꾼 뒤에는 반드시 다시 로그인시킨다」에 있다.
    /// </summary>
    private Task ReloginAsync() =>
        // 이 줄도 `document.getElementById('…').submit` 이라 아무 일도 하지
        // 않고 있었다 — 이유는 UserMenu.LogoutAsync 주석에 있다.
        Js.InvokeVoidAsync("jsiniForm.submit", "jsini-password-logout").AsTask();

    /// <summary>
    /// <c>auth/user/info</c> 에서 만료 안내에 필요한 칸만 꺼낸다.
    ///
    /// <para>
    /// 포털관리의 <c>UserInfoDto</c> 를 쓰지 않는 이유가 둘이다. 셸은 업무
    /// 모듈의 타입을 이름으로 알지 않고(web/CLAUDE.md), 그쪽 DTO 에는 이 세
    /// 칸이 아예 없다. 쓰는 곳이 셋이 되면 그때 <c>Models</c> 로 올린다.
    /// </para>
    /// </summary>
    private sealed class PasswordStatus
    {
        /// <summary>만료로 보는 기간(일). 설정 <c>Auth:PasswordExpiryDays</c> 값이다.</summary>
        public int? PasswordExpiryDays { get; set; }

        /// <summary>남은 날. 이미 지났으면 음수일 수 있다.</summary>
        public int? PasswordDaysRemaining { get; set; }

        /// <summary>지금 막혀 있는가.</summary>
        public bool PasswordExpired { get; set; }
    }
}
