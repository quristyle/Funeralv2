using System.Security.Cryptography;
using System.Text.Json;
using JSini.Web.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace JSini.Web.Shell.Security;

/// <summary>
/// 소셜 로그인의 브라우저 두 자리(공급자로 보내기 · 되돌아오기). 되돌아오는 주소는 사람이 보는 포털 주소여야 한다. API 로 돌아가면 JSON 이 보인다.
/// 받은 인가 코드는 서버끼리 게이트웨이로 넘기고, 토큰은 브라우저로 내려보내지 않는다(BFF).
/// state 는 난수를 암호화한 쿠키에 넣어 되돌아온 값과 맞춘다. 이 대조가 없으면 남의 링크로 로그인되거나 남의 소셜 계정이 붙는다.
/// mode·returnUrl 도 주소가 아니라 쿠키에 넣는다. 주소로 싣으면 공급자를 거치는 동안 mode 를 link 로 바꿔칠 수 있다.
/// </summary>
public static class SocialLoginFlow
{
    /// <summary>오가는 동안만 사는 쿠키. 돌아오면 그 자리에서 지운다.</summary>
    public const string StateCookieName = "jsini_social";

    /// <summary>쿠키 암호화 목적 문자열. 바꾸면 오가던 사람이 한 번 실패한다(그 뒤로는 정상).</summary>
    private const string ProtectorPurpose = "JSini.Web.Shell.SocialLoginState";

    /// <summary>공급자에 다녀오는 데 넉넉한 시간. 이보다 오래 걸리면 다시 누르는 편이 낫다.</summary>
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>두 경로를 건다. 셸 <c>Program.cs</c> 가 한 번 부른다.</summary>
    public static void MapSocialLoginFlow(this WebApplication app)
    {
        // ── 공급자로 보낸다 ─────────────────────────────────────
        //
            // 익명이다(로그인하려는 사람에게 토큰이 없다). 연결 모드의 로그인 판정은 되돌아왔을 때 한다.
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
                // 설정 없음과 게이트웨이 무응답은 사용자가 할 일이 같으므로 갈라 말하지 않는다.
                logger.LogWarning("{Provider} 인가 주소를 만들지 못해 로그인 화면으로 되돌린다.", provider);
                return Results.Redirect(LoginWith("unavailable", returnUrl));
            }

            // 쿠키는 주소를 받은 뒤에 심는다. 먼저 심으면 실패했을 때 쓸모없는 쿠키가 15분 동안 남는다.
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
        // 익명이다. 연결 모드일 때만 로그인 상태를 따지고, 아니면 401 대신 로그인 화면으로 보낸다.
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

            // 쿠키는 무슨 일이 있어도 지운다. 남기면 같은 state 로 다시 들어오는 길이 열린다(재사용 공격).
            var saved = ReadState(http, protection);
            http.Response.Cookies.Delete(StateCookieName, StateCookieOptions(http, expires: null));

            var returnUrl = saved?.ReturnUrl;

            if (!string.IsNullOrWhiteSpace(error))
            {
                // 공급자 화면에서 [취소] 를 누른 것이다. 고장이 아니므로 조용히 로그인 화면으로 되돌린다.
                logger.LogInformation("{Provider} 가 거절을 알려 왔다: {Error}", provider, error);
                return Results.Redirect(LoginWith("cancelled", returnUrl));
            }

            if (saved is null
                || !string.Equals(saved.State, state, StringComparison.Ordinal)
                || !string.Equals(saved.Provider, provider, StringComparison.Ordinal)
                || saved.ExpiresAt < DateTimeOffset.UtcNow)
            {
                // 쿠키가 없거나 값이 다르거나 시간이 지났다. 셋 다 「다시 눌러야 한다」라 한 문구로 합친다(세션 고정 공격도 여기서 걸린다).
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

                // 신원은 맞았는데 쓸 수 없는 계정(승인 대기·정지)이다. 실패로 뭉개면 승인 전 신청자가 다시 시도하라는 말을 듣고 승인될 때까지 누르게 된다.
                return Results.Redirect(
                    LoginWith(result.Blocked ? "blocked" : "failed", returnUrl));
            }

            // 비밀번호가 만료된 계정은 게이트웨이가 다른 경로를 모두 403 으로 막는다. 비밀번호·패스키 로그인과 같은 판단이다.
            return Results.Redirect(
                result.PasswordExpired ? "/password/change" : SafeReturnUrl(returnUrl));
        })
        .AllowAnonymous()
        .WithName("SocialLoginCallback");
    }

    // ── 잔손 ───────────────────────────────────────────────────

    /// <summary>공급자에게 알릴 콜백 주소. Portal:BaseUrl 을 먼저 본다. 운영은 nginx 뒤라 요청 주소가 사설 주소이므로 그대로 쓰면 사람이 되돌아오지 못한다. 설정이 비었을 때만 요청에서 만든다.</summary>
    public static string CallbackUri(HttpContext http, IConfiguration config, string provider)
    {
        var configured = config["Portal:BaseUrl"];

        var origin = string.IsNullOrWhiteSpace(configured)
            ? $"{http.Request.Scheme}://{http.Request.Host}"
            : configured.TrimEnd('/');

        return $"{origin}/social/{provider}/callback";
    }

    /// <summary>공급자 열쇠가 주소에 넣어도 되는 모양인가. 영소문자·숫자·_·- 만 받는다. 조이지 않으면 ../ 나 %2F 로 콜백 주소를 다른 곳으로 돌릴 수 있다.</summary>
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
            // 열쇠 링이 바뀌었거나 누가 손댄 값이다. 둘 다 없는 것으로 본다.
            return null;
        }
    }

    /// <summary>상태 쿠키 옵션. 심을 때와 지울 때가 같아야 한다(특히 Path). SameSite 는 Lax 여야 한다. 공급자가 되돌려 보내는 GET 이동에는 Lax 만 쿠키가 실린다. Secure 는 https 일 때만 건다(개발 http 에서는 쿠키가 저장되지 않는다).</summary>
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

    /// <summary>돌아갈 주소를 이 사이트 안의 절대 경로로만 추린다. //evil.com 은 / 로 시작해도 밖으로 나가므로 따로 막는다.</summary>
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
