using System.Security.Cryptography;
using System.Text.Json;
using JSini.Web.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace JSini.Web.Shell.Security;

/// <summary>
/// 소셜 로그인(구글 · 네이버 · 카카오)의 <b>브라우저가 지나가는 두 자리</b> —
/// 공급자로 보내는 곳과 되돌아오는 곳.
/// </summary>
/// <remarks>
/// <para>
/// [왜 셸에 이 두 경로가 있나]
/// </para>
///
/// <para>
/// OAuth 는 <b>브라우저를 실제로 공급자에게 보냈다가 되돌려 받는</b> 흐름이라,
/// 되돌아오는 주소(<c>redirect_uri</c>)가 사람이 보고 있는 그 사이트여야 한다.
/// 그 주소를 게이트웨이(<c>/api/…</c>)로 두면 공급자가 사람을 API 로 돌려보내고,
/// 그 응답은 화면이 아니라 JSON 이다. 그래서 여기가 포털 주소여야 한다.
/// </para>
///
/// <para>
/// 그리고 여기서 받은 <b>인가 코드는 서버끼리</b> 게이트웨이로 넘긴다 — 토큰이
/// 브라우저까지 내려가면 BFF 가 아니게 된다(web/CLAUDE.md 「인증 — BFF」).
/// 브라우저가 들고 다니는 것은 한 번 쓰면 죽는 인가 코드뿐이다.
/// </para>
///
/// <para>
/// [<c>state</c> — 되돌아온 것이 우리가 보낸 것인지]
/// </para>
///
/// <para>
/// 보낼 때 32바이트 난수를 지어 <b>암호화한 쿠키</b>에 넣어 두고, 되돌아온
/// 값과 맞춰 본다. 이 대조가 없으면 남이 만든 링크 하나로 <b>공격자의 소셜
/// 계정이 피해자 브라우저에 로그인</b>되거나(세션 고정), 로그인한 사람의
/// 계정에 공격자의 소셜 계정이 붙는다(연결 모드).
/// </para>
///
/// <para>
/// 쿠키에는 어디로 돌아갈지(<c>returnUrl</c>)와 무엇을 하러 갔는지(<c>mode</c>)도
/// 함께 넣는다. 주소에 실어 보내면 공급자를 거치는 동안 사람이 고칠 수 있고,
/// 그러면 <c>mode</c> 를 <c>link</c> 로 바꿔치기하는 길이 열린다.
/// </para>
///
/// <para>
/// [PKCE 를 쓰지 않는 까닭]은 AuthServer 의 <c>SocialLoginService</c> 머리말에 있다.
/// </para>
/// </remarks>
public static class SocialLoginFlow
{
    /// <summary>오가는 동안만 사는 쿠키. 돌아오면 그 자리에서 지운다.</summary>
    public const string StateCookieName = "jsini_social";

    /// <summary>
    /// 쿠키를 암호화할 때 쓰는 목적 문자열. <b>바꾸면 오가던 사람이 한 번 실패한다</b>
    /// (그 뒤로는 새 쿠키라 정상이다).
    /// </summary>
    private const string ProtectorPurpose = "JSini.Web.Shell.SocialLoginState";

    /// <summary>공급자에 다녀오는 데 넉넉한 시간. 이보다 오래 걸리면 다시 누르는 편이 낫다.</summary>
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>두 경로를 건다. 셸 <c>Program.cs</c> 가 한 번 부른다.</summary>
    public static void MapSocialLoginFlow(this WebApplication app)
    {
        // ── 공급자로 보낸다 ─────────────────────────────────────
        //
        // **익명이다.** 로그인하려는 사람에게 토큰이 있을 리 없다. 연결
        // (`mode=link`)로 올 때만 로그인 상태를 따지는데, 그 판정은 **돌아왔을
        // 때** 한다 — 여기서 막으면 세션이 만료된 채로 눌렀을 때 공급자까지
        // 다녀오고서야 실패한다.
        app.MapGet("/social/{provider}/start", async (
            string provider,
            string? returnUrl,
            string? mode,
            string? keep,
            HttpContext http,
            LoginService auth,
            IDataProtectionProvider protection,
            IConfiguration config,
            ILoggerFactory loggers,
            CancellationToken ct) =>
        {
            var logger = loggers.CreateLogger("SocialLoginFlow");

            if (!IsSafeProviderKey(provider))
            {
                // 주소에서 온 값이 그대로 콜백 주소에 들어가므로 여기서 조인다.
                return Results.Redirect(LoginWith("badrequest", returnUrl));
            }

            var linking = string.Equals(mode, "link", StringComparison.Ordinal);
            var redirectUri = CallbackUri(http, config, provider);

            // 32바이트 난수. 찍어 맞힐 수 있는 값이 아니어야 대조에 뜻이 있다.
            var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

            var url = await auth.GetSocialAuthorizeUrlAsync(provider, redirectUri, state, ct);

            if (url is null)
            {
                // 설정이 없거나 게이트웨이가 답하지 않았다. 둘 다 사용자가 할 수
                // 있는 일은 같으므로 갈라 말하지 않는다.
                logger.LogWarning("{Provider} 인가 주소를 만들지 못해 로그인 화면으로 되돌린다.", provider);
                return Results.Redirect(LoginWith("unavailable", returnUrl));
            }

            // 쿠키는 **주소를 받은 뒤에** 심는다. 먼저 심으면 실패했을 때
            // 쓸모없는 쿠키가 15분 동안 브라우저에 남는다.
            var payload = JsonSerializer.Serialize(
                new SocialState(state, provider, linking ? "link" : "login", returnUrl,
                    keep == "1", DateTimeOffset.UtcNow.Add(StateLifetime)),
                JsonOptions);

            http.Response.Cookies.Append(
                StateCookieName,
                protection.CreateProtector(ProtectorPurpose).Protect(payload),
                StateCookieOptions(http, expires: DateTimeOffset.UtcNow.Add(StateLifetime)));

            return Results.Redirect(url);
        })
        .AllowAnonymous()
        .WithName("SocialLoginStart");

        // ── 공급자가 되돌려 보낸 자리 ───────────────────────────
        //
        // **익명이다.** 로그인 중인 사람이 여기로 온다. 연결 모드일 때만
        // 로그인 상태를 따지고, 아니면 401 대신 로그인 화면으로 보낸다 —
        // 여기까지 온 사람에게 API 오류를 보여 줄 이유가 없다.
        app.MapGet("/social/{provider}/callback", async (
            string provider,
            string? code,
            string? state,
            string? error,
            HttpContext http,
            LoginService auth,
            GatewayClient gateway,
            IDataProtectionProvider protection,
            IConfiguration config,
            ILoggerFactory loggers,
            CancellationToken ct) =>
        {
            var logger = loggers.CreateLogger("SocialLoginFlow");

            // 쿠키는 **무슨 일이 있어도 지운다.** 남겨 두면 같은 state 로 다시
            // 들어오는 길이 열리고, 그것이 곧 재사용 공격이다.
            var saved = ReadState(http, protection);
            http.Response.Cookies.Delete(StateCookieName, StateCookieOptions(http, expires: null));

            var returnUrl = saved?.ReturnUrl;

            if (!string.IsNullOrWhiteSpace(error))
            {
                // 사용자가 공급자 화면에서 [취소] 를 눌렀을 때 여기 온다.
                // 고장이 아니므로 조용히 로그인 화면으로 되돌린다.
                logger.LogInformation("{Provider} 가 거절을 알려 왔다: {Error}", provider, error);
                return Results.Redirect(LoginWith("cancelled", returnUrl));
            }

            if (saved is null
                || !string.Equals(saved.State, state, StringComparison.Ordinal)
                || !string.Equals(saved.Provider, provider, StringComparison.Ordinal)
                || saved.ExpiresAt < DateTimeOffset.UtcNow)
            {
                // 쿠키가 없거나·값이 다르거나·시간이 지났다. 셋 다 「다시
                // 눌러야 한다」는 같은 결론이라 한 문구로 합친다. 세션 고정
                // 공격도 여기서 걸린다.
                logger.LogWarning(
                    "{Provider} 콜백의 state 가 맞지 않는다 (쿠키 {Has}).",
                    provider, saved is null ? "없음" : "있음");

                return Results.Redirect(LoginWith("expired", returnUrl));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Redirect(LoginWith("failed", returnUrl));
            }

            var redirectUri = CallbackUri(http, config, provider);

            // ── 연결 모드 — 이미 로그인한 사람이 자기 계정에 붙인다 ──
            if (saved.Mode == "link")
            {
                if (http.User.Identity?.IsAuthenticated is not true)
                {
                    // 공급자에 다녀오는 사이에 세션이 끊겼다.
                    return Results.Redirect(LoginWith("signin", "/admin/profile"));
                }

                try
                {
                    await gateway.PostAsync(
                        "auth/social/links",
                        new { provider, code, redirectUri, state },
                        ct);

                    return Results.Redirect(ProfileWith("linked"));
                }
                catch (ApiException ex)
                {
                    logger.LogWarning(ex, "{Provider} 계정을 연결하지 못했다.", provider);
                    return Results.Redirect(ProfileWith("linkfailed"));
                }
            }

            // ── 로그인 모드 ─────────────────────────────────────
            var result = await auth.SignInWithSocialAsync(
                http, provider, code, redirectUri, state, saved.KeepSignedIn, ct);

            if (result.PendingApproval)
            {
                // 성공도 실패도 아니다 — 계정이 만들어졌고 승인을 기다린다.
                return Results.Redirect(LoginWith("pending", returnUrl));
            }

            if (!result.Succeeded)
            {
                logger.LogInformation("{Provider} 로그인 실패: {Message}", provider, result.Message);

                // 신원은 맞았는데 아직 못 쓰는 계정이다(승인 대기 · 정지).
                // **「실패」로 뭉뚱그리면 안 된다** — 앞서 소셜로 신청해 둔
                // 사람이 두 번째로 누르는 자리라, 다시 시도하라고 말하면
                // 승인될 때까지 계속 누르게 된다.
                return Results.Redirect(
                    LoginWith(result.Blocked ? "blocked" : "failed", returnUrl));
            }

            // 비밀번호가 만료된 계정은 게이트웨이가 다른 경로를 모두 403 으로
            // 막는다 — 비밀번호 로그인·패스키 로그인과 같은 판단이다.
            return Results.Redirect(
                result.PasswordExpired ? "/password/change" : SafeReturnUrl(returnUrl));
        })
        .AllowAnonymous()
        .WithName("SocialLoginCallback");
    }

    // ── 잔손 ───────────────────────────────────────────────────

    /// <summary>
    /// 공급자에게 알려 줄 콜백 주소를 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>설정을 먼저 본다</b>(<c>Portal:BaseUrl</c>). 운영에서는 nginx 뒤라
    /// 요청이 <c>http://127.0.0.1:5557</c> 로 들어오는데, 그 값을 그대로 쓰면
    /// 공급자에게 <b>사설 주소를 콜백으로 알려 주는</b> 셈이 된다 — 그러면
    /// 사람이 되돌아오지 못한다.
    /// </para>
    /// <para>
    /// 설정이 비어 있으면 요청에서 만든다(개발 장비에서 쓰는 길이다).
    /// </para>
    /// </remarks>
    public static string CallbackUri(HttpContext http, IConfiguration config, string provider)
    {
        var configured = config["Portal:BaseUrl"];

        var origin = string.IsNullOrWhiteSpace(configured)
            ? $"{http.Request.Scheme}://{http.Request.Host}"
            : configured.TrimEnd('/');

        return $"{origin}/social/{provider}/callback";
    }

    /// <summary>
    /// 공급자 열쇠가 주소에 넣어도 되는 모양인가.
    /// </summary>
    /// <remarks>
    /// 이 값은 주소에서 와서 그대로 콜백 주소에 들어간다. 조이지 않으면
    /// <c>../</c> 나 <c>%2F</c> 로 콜백 주소를 다른 곳으로 돌릴 수 있다.
    ///
    /// <para>
    /// 영소문자·숫자와 <c>_</c> · <c>-</c> 만 받는다. 설정에 공급자를 더할 때
    /// 쓸 만한 이름(<c>github</c> · <c>naver_works</c>)은 전부 통과하고,
    /// 경로를 비틀 수 있는 글자는 하나도 들어오지 못한다.
    /// </para>
    /// </remarks>
    private static bool IsSafeProviderKey(string? provider) =>
        !string.IsNullOrWhiteSpace(provider)
        && provider.Length <= 20
        && provider.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '_' or '-');

    /// <summary>쿠키에서 상태를 꺼낸다. 없거나 못 풀면 <c>null</c>.</summary>
    private static SocialState? ReadState(HttpContext http, IDataProtectionProvider protection)
    {
        var raw = http.Request.Cookies[StateCookieName];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var json = protection.CreateProtector(ProtectorPurpose).Unprotect(raw);
            return JsonSerializer.Deserialize<SocialState>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // 열쇠 링이 바뀌었거나 누가 손댄 값이다. 둘 다 「다시 눌러야
            // 한다」로 끝나므로 조용히 없는 것으로 본다.
            return null;
        }
    }

    /// <summary>
    /// 상태 쿠키의 옵션. <b>심을 때와 지울 때가 같아야</b> 브라우저가 같은
    /// 쿠키로 알아본다(특히 <c>Path</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SameSite=Lax</c> 여야 한다. 공급자가 사람을 되돌려 보내는 것은
    /// <b>맨 위 창의 GET 이동</b>이라 Lax 로도 실려 온다. <c>Strict</c> 로
    /// 두면 그 요청에 쿠키가 실리지 않아 <b>언제나 state 가 안 맞는다</b> —
    /// 증상은 「계속 다시 시도해 달라고 한다」 하나뿐이라 알아내기 어렵다.
    /// </para>
    /// <para>
    /// <c>Secure</c> 는 지금 요청이 https 일 때만 건다. 개발 장비는 http 라
    /// 늘 걸면 쿠키가 아예 저장되지 않는다.
    /// </para>
    /// </remarks>
    private static CookieOptions StateCookieOptions(HttpContext http, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = http.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/social",
        Expires = expires,
    };

    /// <summary>로그인 화면으로 되돌린다. <paramref name="notice"/> 가 무슨 일이 있었는지 말한다.</summary>
    private static string LoginWith(string notice, string? returnUrl)
    {
        var safe = SafeReturnUrl(returnUrl);

        return safe == "/"
            ? $"/login?social={notice}"
            : $"/login?social={notice}&returnUrl={Uri.EscapeDataString(safe)}";
    }

    /// <summary>「내 정보」로 되돌린다. 연결 모드로 왔을 때만 쓴다.</summary>
    private static string ProfileWith(string notice) => $"/admin/profile?social={notice}";

    /// <summary>
    /// 돌아갈 주소를 안전한 것으로만 추린다.
    /// </summary>
    /// <remarks>
    /// <c>Login.razor</c> 의 같은 이름과 같은 규칙이다 — <b>이 사이트 안의
    /// 절대 경로</b>만 받는다. <c>//evil.com</c> 은 프로토콜 상대 URL 이라
    /// <c>/</c> 로 시작해도 밖으로 나간다.
    /// </remarks>
    private static string SafeReturnUrl(string? returnUrl) =>
        string.IsNullOrWhiteSpace(returnUrl)
        || !returnUrl.StartsWith('/')
        || returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? "/"
            : returnUrl;

    /// <summary>오가는 동안 쿠키에 담아 두는 것.</summary>
    /// <param name="State">공급자에게 실어 보낸 난수. 돌아온 값과 맞춰 본다.</param>
    /// <param name="Provider">어느 공급자에게 갔는지. 돌아온 경로와 맞춰 본다.</param>
    /// <param name="Mode"><c>login</c> 또는 <c>link</c>.</param>
    /// <param name="ReturnUrl">로그인 뒤 돌아갈 곳.</param>
    /// <param name="KeepSignedIn">「로그인 유지」를 켜고 눌렀는가.</param>
    /// <param name="ExpiresAt">이 시각이 지나면 쓰지 않는다.</param>
    private sealed record SocialState(
        string State,
        string Provider,
        string Mode,
        string? ReturnUrl,
        bool KeepSignedIn,
        DateTimeOffset ExpiresAt);
}
