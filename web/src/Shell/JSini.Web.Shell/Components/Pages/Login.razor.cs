using Microsoft.AspNetCore.Components;
using JSini.Web.Shell.Security;

namespace JSini.Web.Shell.Components.Pages;

public partial class Login
{
    [Inject] private LoginService Auth { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IWebHostEnvironment Env { get; set; } = default!;
    [Inject] private IConfiguration Config { get; set; } = default!;
    [Inject] private ILoggerFactory Loggers { get; set; } = default!;

    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;

    // 선언부 초기값은 폼 제출 때 null 로 덮일 수 있다(BL0008). OnInitialized 에서 비어 있을 때만 채운다.
    //
    // 폼이 둘이라 Model 이름을 적어야 한다. 안 적으면 두 속성이 같은 자료를 봐서 엉뚱한 칸을 제출한다.
    [SupplyParameterFromForm(FormName = "login")]
    private LoginInput? Input { get; set; }

    /// <summary>패스키 폼에 실려 온 값. 그 폼이 제출됐을 때만 채워진다.</summary>
    [SupplyParameterFromForm(FormName = "passkey-login")]
    private PasskeyLoginInput? Passkey { get; set; }

    /// <summary>「로그인 유지」 딱지의 날수. 설정을 같은 곳에서 읽어 말만 한다(글자를 박아 두면 설정을 바꿔도 화면만 옛말을 한다).</summary>
    private int KeepSignedInDays => Config.GetValue<int?>("Auth:KeepSignedInDays") ?? 30;

    /// <summary>로그인 전에 가려던 곳. 즐겨찾기나 알림 링크로 들어온 사람을 원래 자리로 돌려보낸다.</summary>
    [SupplyParameterFromQuery] private string? ReturnUrl { get; set; }

    private string? _error;

    /// <summary>실패가 아닌 안내(가입 신청 접수 등). 실패 판과 갈라 그린다.</summary>
    private string? _notice;

    /// <summary>그릴 소셜 단추들. 서버가 알려 준 것만 들어 있다.</summary>
    private IReadOnlyList<SocialProvider> _socialProviders = [];

    /// <summary>자동 로그인을 이번만 건너뛴다 — <c>/login?noauto=1</c>.</summary>
    [SupplyParameterFromQuery(Name = "noauto")] private string? NoAuto { get; set; }

    /// <summary>소셜에서 되돌아왔을 때 붙는 짧은 표시(<c>/login?social=pending</c>). 문구를 주소에 싣지 않는다. 남의 링크로 아무 말이나 띄울 수 있다.</summary>
    [SupplyParameterFromQuery(Name = "social")] private string? SocialNotice { get; set; }

    /// <summary>소셜 단추가 갈 곳. returnUrl 은 언제나 붙인다. 스크립트가 뒤에 <c>&amp;keep=1</c> 을 덧붙이므로 물음표 개수를 따지지 않게 한다.</summary>
    private string SocialHref(string provider) =>
        $"/social/{provider}/start?returnUrl={Uri.EscapeDataString(SafeReturnUrl())}";

    /// <summary>소셜에서 되돌아온 표시를 사람이 읽을 말로 바꾼다. 실패 판의 말과 안내 판의 말을 돌려준다(둘 다 없을 수 있다).</summary>
    private static (string? Error, string? Notice) ReadSocialNotice(string? code) => code switch
    {
        // 계정이 만들어졌다. 실패가 아니다(서버가 202 로 답한 갈래).
        "pending" => (null,
            "가입 신청을 받았습니다. 관리자 승인 뒤에 로그인하실 수 있습니다. "
            + "승인되면 소셜 계정에 적힌 이메일로 알려 드립니다."),

        // 승인 대기와 정지를 한 문구로 덮는다. 갈라 말하면 「정지된 계정」이 소셜 화면을 거쳐 본인이 아닌 사람에게도 보인다.
        "blocked" => (null,
            "지금은 로그인하실 수 없는 계정입니다. "
            + "가입 승인을 기다리는 중이거나 사용이 중지된 계정입니다. "
            + "승인되면 알려 드립니다."),

        // 공급자 화면에서 [취소] 를 눌렀다. 고장이 아니라 사람이 그만둔 것이다.
        "cancelled" => (null, "소셜 로그인을 취소하셨습니다."),

        // 오가는 사이에 시간이 지났거나 쿠키가 사라졌다.
        "expired" => ("시간이 지나 다시 확인이 필요합니다. 한 번 더 눌러 주세요.", null),

        // 설정 없음과 게이트웨이 무응답은 사용자가 할 일이 같으므로 갈라 말하지 않는다.
        "unavailable" => ("지금은 소셜 로그인을 쓸 수 없습니다. 아이디로 로그인해 주세요.", null),

        // 연결하러 갔는데 세션이 끊겼다.
        "signin" => (null, "다시 로그인하신 뒤에 소셜 계정을 연결해 주세요."),

        "failed" or "badrequest" => ("소셜 계정으로 로그인하지 못했습니다. 다시 시도해 주세요.", null),

        _ => (null, null),
    };

    /// <summary>
    /// 폼을 만들고, <b>개발 장비에서는 로그인까지 자동으로 해 버린다</b>(<c>DevLogin</c> 설정이 있을 때만, <c>IsDevelopment</c> 에서만).
    /// 자동 로그인은 <c>/login?noauto=1</c> 로 한 번 건너뛴다. 로그아웃은 언제나 이 주소로 보낸다. 맨 /login 이면 바로 다시 들어가 버린다.
    /// 들어갈 때마다 경고를 남긴다. 조용히 로그인되는 것이 가장 나쁘다.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        // 패스키 폼 제출 요청인가. 자동 로그인을 건너뛰는 근거라서 Passkey 를 채우기 전에 본다.
        var passkeySubmitted = Passkey is not null;
        Passkey ??= new PasskeyLoginInput();

        // 모델은 첫 await 전에 채운다. 뒤에 채우면 소셜 단추 조회가 기다리는 사이 EditForm 이 Model 없이 그려져 로그인 화면이 500 이 된다.
        var loginSubmitted = Input is not null;
        Input ??= new LoginInput();

        // 소셜에서 되돌아온 안내는 폼 제출 요청에도 읽는다. 안 그러면 비밀번호를 한 번 틀린 뒤 왜 못 들어갔는지 사라진다.
        (_error, _notice) = ReadSocialNotice(SocialNotice);

        // 단추 목록은 게이트웨이에 묻는다. 값이 캐시에 있으면 대개 왕복이 없고, 실패해도 빈 목록이라 아이디·비밀번호 로그인은 그대로 된다.
        _socialProviders = await Auth.GetSocialProvidersAsync();

        // 폼 제출로 들어온 요청이면 사용자가 친 값이 들어 있다. 손대지 않는다.
        if (loginSubmitted)
        {
            return;
        }

        if (!Env.IsDevelopment())
        {
            return;
        }

            // 개발 자동 로그인이 패스키 제출을 가로채면 안 된다. 수명 주기가 폼 처리기보다 먼저 돌아, 막지 않으면 지문 요청이 설정 계정으로 들어가 버린다.
        if (passkeySubmitted)
        {
            return;
        }

        var id = Config["DevLogin:Username"];
        var password = Config["DevLogin:Password"];

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        Input.Username = id;
        Input.Password = password;

        if (!Config.GetValue("DevLogin:Auto", true) || NoAuto == "1")
        {
            return;
        }

        var result = await Auth.SignInAsync(HttpContext, id, password);

        if (!result.Succeeded)
        {
            // 자동 로그인에 실패하면 화면에 남겨 손으로 다시 누를 수 있게 한다.
            _error = result.Message;
            return;
        }

        Loggers.CreateLogger<Login>().LogWarning(
            "개발 자동 로그인: {LoginId}. 끄려면 appsettings.Local.json 의 DevLogin:Auto 를 false 로 두십시오.", id);

        Navigation.NavigateTo(
            result.PasswordExpired ? "/password/change" : SafeReturnUrl(),
            forceLoad: true);
    }

    private async Task SubmitAsync()
    {
        var result = await Auth.SignInAsync(
            HttpContext, Input!.Username, Input.Password, Input.KeepSignedIn);

        await GoAsync(result);
    }

    /// <summary>지문·얼굴로 들어온다. 서명은 passkey.js 가 감춘 칸에 담아 두었고 여기서는 게이트웨이로 넘긴다.</summary>
    private async Task SubmitPasskeyAsync()
    {
        if (string.IsNullOrWhiteSpace(Passkey!.Assertion))
        {
            // 단추는 type="button" 이라 정상 흐름에서는 오지 않는다. 스크립트가 막힌 브라우저에서 엔터로 제출된 경우다.
            _error = "기기 인증 값이 비어 있습니다. 다시 시도해 주세요.";
            return;
        }

        var result = await Auth.SignInWithPasskeyAsync(
            HttpContext, Passkey.Assertion, Passkey.KeepSignedIn);

        await GoAsync(result);
    }

    /// <summary>로그인 결과를 받아 다음 화면으로 보낸다. 들어온 길과 무관하게 같다.</summary>
    private Task GoAsync(LoginResult result)
    {
        if (!result.Succeeded)
        {
            _error = result.Message;
            return Task.CompletedTask;
        }

        // 비밀번호 만료 계정은 다른 경로가 모두 403 이므로 곧바로 변경 화면으로 안내한다.
        var destination = result.PasswordExpired
            ? "/password/change"
            : SafeReturnUrl();

        // forceLoad: 방금 구운 쿠키를 브라우저가 다시 들고 들어와야 인증된 회로가 만들어진다.
        Navigation.NavigateTo(destination, forceLoad: true);
        return Task.CompletedTask;
    }

    /// <summary>돌아갈 주소를 이 사이트 안의 절대 경로로만 추린다(오픈 리다이렉트 방지). //evil.com 은 / 로 시작해도 밖으로 나간다.</summary>
    private string SafeReturnUrl()
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl)
            || !ReturnUrl.StartsWith('/')
            || ReturnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return ReturnUrl;
    }
}
