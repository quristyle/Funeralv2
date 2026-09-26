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

    // 초기값을 선언부에 두면 폼 제출 때 null 로 덮일 수 있다(BL0008).
    // OnInitialized 에서 비어 있을 때만 채운다.
    //
    // **폼이 둘이라 이름을 적어 줘야 한다.** 안 적으면 어느 폼이 제출되든
    // 두 속성이 같은 자료를 보려 들고, 그때 증상은 「지문 단추를 눌렀는데
    // 아이디를 입력하라고 한다」다.
    [SupplyParameterFromForm(FormName = "login")]
    private LoginInput? Input { get; set; }

    /// <summary>패스키 폼에 실려 온 값. 그 폼이 제출됐을 때만 채워진다.</summary>
    [SupplyParameterFromForm(FormName = "passkey-login")]
    private PasskeyLoginInput? Passkey { get; set; }

    /// <summary>
    /// 「로그인 유지」 딱지에 적을 날수. 실제 수명을 정하는 것은
    /// <see cref="LoginService"/> 이고 여기는 <b>같은 설정을 읽어</b> 말만 한다 —
    /// 글자를 손으로 박아 두면 설정을 바꿔도 화면만 옛말을 한다.
    /// </summary>
    private int KeepSignedInDays => Config.GetValue<int?>("Auth:KeepSignedInDays") ?? 30;

    /// <summary>
    /// 로그인 전에 가려던 곳. 즐겨찾기나 알림 링크로 들어온 사람을 원래
    /// 자리로 돌려보내려고 들고 다닌다.
    /// </summary>
    [SupplyParameterFromQuery] private string? ReturnUrl { get; set; }

    private string? _error;

    /// <summary>실패가 아닌 안내(가입 신청 접수 등). 실패 판과 갈라 그린다.</summary>
    private string? _notice;

    /// <summary>그릴 소셜 단추들. 서버가 알려 준 것만 들어 있다.</summary>
    private IReadOnlyList<SocialProvider> _socialProviders = [];

    /// <summary>자동 로그인을 이번만 건너뛴다 — <c>/login?noauto=1</c>.</summary>
    [SupplyParameterFromQuery(Name = "noauto")] private string? NoAuto { get; set; }

    /// <summary>
    /// 소셜에서 되돌아왔을 때 붙는 표시 — <c>/login?social=pending</c>.
    /// <see cref="SocialLoginFlow"/> 가 붙인다.
    /// </summary>
    /// <remarks>
    /// <b>짧은 표시만 주고받는다.</b> 문구를 주소에 그대로 실으면 남이 만든
    /// 링크로 이 화면에 아무 말이나 띄울 수 있다 — 로그인 화면에 뜬 글은
    /// 사용자가 서버의 말로 읽는다.
    /// </remarks>
    [SupplyParameterFromQuery(Name = "social")] private string? SocialNotice { get; set; }

    /// <summary>
    /// 소셜 단추가 갈 곳. 돌아올 자리를 함께 들려 보낸다.
    /// </summary>
    /// <remarks>
    /// <c>&amp;</c> 로 잇도록 <c>returnUrl</c> 을 언제나 붙인다 — 스크립트가
    /// 뒤에 <c>&amp;keep=1</c> 을 덧붙이는데, 물음표가 하나뿐인지 둘인지를
    /// 그쪽이 따지지 않아도 되게 한다.
    /// </remarks>
    private string SocialHref(string provider) =>
        $"/social/{provider}/start?returnUrl={Uri.EscapeDataString(SafeReturnUrl())}";

    /// <summary>
    /// 소셜에서 되돌아온 표시를 사람이 읽을 말로 바꾼다.
    /// </summary>
    /// <returns>실패 판에 그릴 말과 안내 판에 그릴 말. 둘 다 없을 수 있다.</returns>
    private static (string? Error, string? Notice) ReadSocialNotice(string? code) => code switch
    {
        // 계정이 만들어졌다. **실패가 아니다** — 이 갈래를 두려고 서버가
        // 202 로 답한다(LoginService.PendingApproval).
        "pending" => (null,
            "가입 신청을 받았습니다. 관리자 승인 뒤에 로그인하실 수 있습니다. "
            + "승인되면 소셜 계정에 적힌 이메일로 알려 드립니다."),

        // 신원은 맞았는데 아직 못 쓰는 계정이다. **승인 대기와 정지를 한
        // 문구로 덮는다** — 갈라 말하면 「정지된 계정입니다」가 소셜 화면을
        // 거쳐 나가는데, 그 말은 본인이 아닌 사람에게도 보일 수 있다.
        "blocked" => (null,
            "지금은 로그인하실 수 없는 계정입니다. "
            + "가입 승인을 기다리는 중이거나 사용이 중지된 계정입니다. "
            + "승인되면 알려 드립니다."),

        // 공급자 화면에서 [취소] 를 눌렀다. 고장이 아니라 사람이 그만둔 것이다.
        "cancelled" => (null, "소셜 로그인을 취소하셨습니다."),

        // 오가는 사이에 시간이 지났거나 쿠키가 사라졌다.
        "expired" => ("시간이 지나 다시 확인이 필요합니다. 한 번 더 눌러 주세요.", null),

        // 설정이 없거나 게이트웨이가 답하지 않았다. 사용자가 할 수 있는 일은
        // 어느 쪽이든 같으므로 갈라 말하지 않는다.
        "unavailable" => ("지금은 소셜 로그인을 쓸 수 없습니다. 아이디로 로그인해 주세요.", null),

        // 연결하러 갔는데 세션이 끊겼다.
        "signin" => (null, "다시 로그인하신 뒤에 소셜 계정을 연결해 주세요."),

        "failed" or "badrequest" => ("소셜 계정으로 로그인하지 못했습니다. 다시 시도해 주세요.", null),

        _ => (null, null),
    };

    /// <summary>
    /// 폼을 만들고, <b>개발 장비에서는 그대로 로그인까지 해 버린다.</b>
    ///
    /// <para>
    /// 화면을 다듬는 동안 하루에도 몇 번씩 재기동하는데, 그때마다 로그인 화면을
    /// 지나가는 것이 작업 시간의 잡음이다. 그래서 개발 중에는 이 화면이
    /// <b>보이지 않고</b> 곧바로 들어간다.
    /// </para>
    ///
    /// <para>
    /// [막는 곳이 셋이다]
    /// </para>
    ///
    /// <list type="number">
    ///   <item><c>IsDevelopment()</c> — 운영에서는 아예 안 돈다.</item>
    ///   <item><c>DevLogin</c> 설정이 있어야 한다. 그 값은
    ///         <c>appsettings.Local.json</c>(git 제외)에만 있다.
    ///         <b>값을 소스에 적지 않는다</b> — 개발 환경이 운영 DB 를 바라보고
    ///         있어 여기 적히는 것은 진짜 계정의 진짜 비밀번호다.</item>
    ///   <item><c>DevLogin:Auto</c> 를 <c>false</c> 로 두면 채우기만 하고
    ///         들어가지는 않는다.</item>
    /// </list>
    ///
    /// <para>
    /// 한 번만 건너뛰려면 <c>/login?noauto=1</c>. <b>로그아웃은 언제나 이
    /// 주소로 보낸다</b>(Program.cs 의 <c>/logout</c>) — 맨 <c>/login</c> 으로
    /// 보내던 때는 로그아웃하자마자 이 화면이 다시 넣어 버려서 「새로 고침만
    /// 된다」로 보였다.
    /// </para>
    ///
    /// <para>
    /// 들어갈 때마다 경고를 남긴다. <b>조용히 로그인되는 것이 가장 나쁘다</b> —
    /// 로그를 보는 사람이 그 요청을 누가 한 것인지 알 수 있어야 한다.
    /// </para>
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        // 패스키 폼이 제출된 요청인가. **아래에서 자동 로그인을 건너뛰는
        // 근거**라 `Passkey` 를 채우기 전에 봐 둔다 — 채운 뒤에 보면 늘 참이다.
        var passkeySubmitted = Passkey is not null;
        Passkey ??= new PasskeyLoginInput();

        // [두 폼의 모델을 **첫 await 전에** 채운다 — 2026-09-24]
        //
        // 아래 소셜 단추 조회가 실제로 기다리면(값이 캐시에 없을 때) 그 자리에서
        // 화면이 한 번 먼저 그려진다. 그때 `Input` 이 비어 있으면 EditForm 이
        // 「Model 이 없다」로 던져 **로그인 화면이 500** 이 된다. 캐시가 찬 뒤로는
        // 멀쩡해서 가끔만 났다(운영에서 500 → 200 을 번갈아 봤다).
        var loginSubmitted = Input is not null;
        Input ??= new LoginInput();

        // 소셜에서 되돌아온 안내를 먼저 읽는다. **폼 제출로 들어온 요청에도
        // 읽어야 한다** — 안 그러면 되돌아온 직후 화면에서만 보이고, 그 자리에서
        // 비밀번호를 한 번 틀리면 왜 못 들어갔는지가 사라진다.
        (_error, _notice) = ReadSocialNotice(SocialNotice);

        // 단추 목록은 게이트웨이에 물어본다. 값이 담겨 있어 대개 왕복이 없다
        // (LoginService.GetSocialProvidersAsync). 실패해도 빈 목록이라
        // 아이디·비밀번호 로그인은 그대로 된다.
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

        // 개발 자동 로그인이 **패스키 제출을 가로채면 안 된다.** 수명 주기가
        // 폼 처리기보다 먼저 도므로, 막지 않으면 지문을 대고 온 요청이
        // 설정에 적힌 계정으로 들어가 버린다.
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
            // 화면에 남겨 둔다. 자동으로 들어가려다 실패한 것이라 사용자가
            // 손으로 다시 눌러 볼 수 있어야 한다.
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

    /// <summary>
    /// 지문·얼굴로 들어온다. 기기 서명은 이미 <c>passkey.js</c> 가 받아
    /// 감춘 칸에 담아 놓았고, 여기서는 그것을 게이트웨이로 넘긴다.
    /// </summary>
    private async Task SubmitPasskeyAsync()
    {
        if (string.IsNullOrWhiteSpace(Passkey!.Assertion))
        {
            // 단추가 `type="button"` 이라 정상 흐름에서는 여기 오지 않는다.
            // 스크립트가 막힌 브라우저에서 엔터로 제출된 경우다.
            _error = "기기 인증 값이 비어 있습니다. 다시 시도해 주세요.";
            return;
        }

        var result = await Auth.SignInWithPasskeyAsync(
            HttpContext, Passkey.Assertion, Passkey.KeepSignedIn);

        await GoAsync(result);
    }

    /// <summary>
    /// 로그인 결과를 받아 다음 화면으로 보낸다. <b>들어온 길과 무관하게 같다.</b>
    /// </summary>
    private Task GoAsync(LoginResult result)
    {
        if (!result.Succeeded)
        {
            _error = result.Message;
            return Task.CompletedTask;
        }

        // 비밀번호가 만료된 계정은 게이트웨이가 다른 경로를 모두 403 으로 막는다.
        // 어디로 보내든 403 만 보게 되므로 곧바로 변경 화면으로 안내한다.
        var destination = result.PasswordExpired
            ? "/password/change"
            : SafeReturnUrl();

        // forceLoad: 방금 구운 쿠키를 브라우저가 들고 다시 들어와야
        // 인증된 상태로 회로가 만들어진다.
        Navigation.NavigateTo(destination, forceLoad: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 돌아갈 주소를 안전한 것으로만 추린다.
    ///
    /// `returnUrl` 을 그대로 믿으면 남이 만든 링크로 외부 사이트에 보낼 수 있다
    /// (오픈 리다이렉트). 로그인 직후라 사용자가 주소를 잘 보지 않는 시점이라
    /// 특히 위험하다. 그래서 **이 사이트 안의 절대 경로**만 받는다.
    /// `//evil.com` 은 프로토콜 상대 URL 이라 `/` 로 시작해도 밖으로 나간다.
    /// </summary>
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
