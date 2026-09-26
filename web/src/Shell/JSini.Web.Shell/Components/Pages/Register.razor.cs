using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.Shell.Security;

namespace JSini.Web.Shell.Components.Pages;

public partial class Register
{
    [Inject] private AccountClient Accounts { get; set; } = default!;
    [Inject] private LoginService Auth { get; set; } = default!;

    private readonly SignupInput _input = new();

    /// <summary>신청을 보냈는가. 참이면 폼 대신 안내만 보인다.</summary>
    private bool _sent;

    /// <summary>그릴 소셜 단추들. 서버가 알려 준 것만 들어 있다.</summary>
    private IReadOnlyList<SocialProvider> _socialProviders = [];

    /// <summary>
    /// 소셜 단추 목록을 받아 둔다. <b>실패해도 화면은 그대로 뜬다</b> —
    /// 단추가 없을 뿐이고 손으로 채우는 신청은 그대로 되어야 한다.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _socialProviders = await Auth.GetSocialProvidersAsync();
    }

    /// <summary>
    /// 신청을 보낸다.
    ///
    /// <para>
    /// 서버도 같은 것을 검사한다. 여기서 한 번 더 보는 이유는 <b>왕복 없이
    /// 알려 주려는 것</b>뿐이고, 진짜 판정은 언제나 서버가 한다 —
    /// 아이디 중복처럼 화면이 알 수 없는 것도 있다.
    /// </para>
    /// </summary>
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_input.LoginId)
            || string.IsNullOrWhiteSpace(_input.UserName)
            || string.IsNullOrWhiteSpace(_input.Email))
        {
            Say("아이디 · 이름 · 이메일은 반드시 넣어야 합니다.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrEmpty(_input.Password))
        {
            Say("비밀번호를 넣으십시오.", NoticeTone.Warning);
            return;
        }

        if (!string.Equals(_input.Password, _input.Confirm, StringComparison.Ordinal))
        {
            // 서버는 두 값이 같은지 알 방법이 없다. 여기서만 잡을 수 있다.
            Say("비밀번호와 확인이 다릅니다.", NoticeTone.Warning);
            return;
        }

        _sent = await RunAsync(
            () => Accounts.SignupAsync(_input),
            "가입 신청을 받았습니다. 관리자 승인 뒤에 로그인하실 수 있습니다.",
            "신청을 보내지 못했습니다");
    }
}
