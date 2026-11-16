using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Shell.Security;

namespace JSini.Web.Shell.Components.Pages;

public partial class Register
{
    [Inject] private AccountClient Accounts { get; set; } = default!;
    [Inject] private LoginService Auth { get; set; } = default!;

    private readonly SignupInput _input = new();

    /// <summary>신청을 보냈는가. 참이면 폼 대신 안내만 보인다.</summary>
    private bool _sent;

    /// <summary>보내는 중인가. 두 번 누르면 같은 아이디로 두 번 나가 두 번째가 「이미 있는 아이디」로 실패한다.</summary>
    private bool _busy;

    /// <summary>신청하기를 한 번이라도 눌렀는가. 그 전에는 빈 칸을 꾸짖지 않는다(처음 연 화면이 온통 붉으면 안 된다).</summary>
    private bool _tried;

    /// <summary>단추 위에 남는 안내. 다음에 누를 때까지 남는다.</summary>
    private string? _notice;

    private bool _failed;

    /// <summary>회로가 붙기 전에는 단추를 눌러도 아무 일이 없다 — 그때와 보내는 중에는 잠근다.</summary>
    private bool Blocked => _busy || !RendererInfo.IsInteractive;

    /// <summary>확인 칸에 무엇이 들어 있고 비밀번호와 같은가. 적는 동안 바로 보여 준다.</summary>
    private bool ConfirmMatches =>
        !string.IsNullOrEmpty(_input.Confirm)
        && string.Equals(_input.Password, _input.Confirm, StringComparison.Ordinal);

    /// <summary>소셜 단추 목록을 받아 둔다. 실패해도 화면은 뜬다(단추가 없을 뿐 손으로 채우는 신청은 되어야 한다).</summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _socialProviders = await Auth.GetSocialProvidersAsync();
    }

    /// <summary>그릴 소셜 단추들. 서버가 알려 준 것만 들어 있다.</summary>
    private IReadOnlyList<SocialProvider> _socialProviders = [];

    /// <summary>칸마다의 잘못. 비밀번호 확인이 다른 것은 누르기 전에도 알린다 — 그것만은 적는 순간 틀린 줄 알 수 있다.</summary>
    private string? ErrorOf(string key) => key switch
    {
        "loginId" when _tried && string.IsNullOrWhiteSpace(_input.LoginId) => "아이디를 넣으십시오.",
        "userName" when _tried && string.IsNullOrWhiteSpace(_input.UserName) => "이름을 넣으십시오.",
        "email" when _tried && string.IsNullOrWhiteSpace(_input.Email) => "이메일을 넣으십시오.",
        "email" when !string.IsNullOrWhiteSpace(_input.Email) && !LooksLikeEmail(_input.Email) => "이메일 형식이 아닙니다.",
        "password" when _tried && string.IsNullOrEmpty(_input.Password) => "비밀번호를 넣으십시오.",
        "confirm" when !string.IsNullOrEmpty(_input.Confirm) && !ConfirmMatches => "비밀번호와 확인이 다릅니다.",
        "confirm" when _tried && string.IsNullOrEmpty(_input.Confirm) && !string.IsNullOrEmpty(_input.Password) => "비밀번호를 한 번 더 넣으십시오.",
        _ => null,
    };

    private string? Invalid(string key) => ErrorOf(key) is null ? null : "true";

    private string InputClass(string key) =>
        ErrorOf(key) is null ? "jsini-login__input" : "jsini-login__input jsini-login__input--invalid";

    private RenderFragment FieldError(string key) => builder =>
    {
        if (ErrorOf(key) is not { } text)
        {
            return;
        }

        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "jsini-login__fielderr");
        builder.AddAttribute(2, "role", "alert");
        builder.AddContent(3, text);
        builder.CloseElement();
    };

    /// <summary>서버가 진짜로 따지므로 여기서는 「@ 앞뒤에 무엇이 있나」만 본다.</summary>
    private static bool LooksLikeEmail(string value)
    {
        var at = value.Trim().IndexOf('@');
        return at > 0 && at < value.Trim().Length - 1;
    }

    private static readonly string[] Keys = ["loginId", "userName", "email", "password", "confirm"];

    /// <summary>신청을 보낸다. 여기서 보는 것은 왕복 없이 알려 주려는 것뿐이고 진짜 판정은 서버가 한다(아이디 중복처럼 화면이 모르는 것도 있다).</summary>
    private async Task SubmitAsync()
    {
        if (_busy)
        {
            return;
        }

        _tried = true;

        var wrong = Keys.Count(k => ErrorOf(k) is not null);
        if (wrong > 0)
        {
            _notice = $"입력을 확인해 주십시오 — 고칠 칸이 {wrong}곳 있습니다.";
            _failed = true;
            return;
        }

        _notice = null;
        _failed = false;
        _busy = true;

        try
        {
            await Accounts.SignupAsync(_input);
            _sent = true;
        }
        catch (ApiException ex)
        {
            _notice = $"신청을 보내지 못했습니다 — {ex.Message}";
            _failed = true;
        }
        finally
        {
            _busy = false;
        }
    }
}
