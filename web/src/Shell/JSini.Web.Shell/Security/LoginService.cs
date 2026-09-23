using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using JSini.Web.Components.Security;

namespace JSini.Web.Shell.Security;

/// <summary>로그인 결과.</summary>
/// <param name="Succeeded">성공 여부</param>
/// <param name="Message">실패했을 때 사용자에게 보여 줄 문구</param>
/// <param name="PasswordExpired">
/// 비밀번호 사용 기간이 지났는가.
///
/// 지났어도 토큰은 정상 발급된다 — 비밀번호를 바꾸려면 로그인 상태여야 하기
/// 때문이다. 대신 게이트웨이가 비밀번호 변경에 필요한 경로만 통과시키므로,
/// 화면은 이 값을 보고 곧바로 비밀번호 변경으로 안내해야 한다. 안 그러면
/// 사용자는 아무 화면이나 열 때마다 403 만 보게 된다.
/// </param>
public readonly record struct LoginResult(
    bool Succeeded,
    string? Message = null,
    bool PasswordExpired = false);

/// <summary>
/// 로그인·로그아웃. 게이트웨이에 물어보고, 그 결과로 셸의 인증 쿠키를 굽는다.
///
/// [게이트웨이 클라이언트를 쓰지 않는 이유]
///
/// 로그인 응답의 <c>Set-Cookie</c> 헤더(리프레시 토큰)를 손에 넣어야 하는데,
/// <see cref="JSini.Web.Http.GatewayClient"/> 는 본문만 돌려준다. 그리고 로그인
/// 시점에는 붙일 토큰도 없어서 <c>AuthTokenHandler</c> 를 거칠 이유가 없다.
/// 그래서 맨 <see cref="HttpClient"/> 를 따로 쓴다.
///
/// [들어오는 길이 둘이다 — 비밀번호와 패스키]
///
/// 갈리는 것은 <b>게이트웨이의 어느 경로를 두드리느냐</b> 하나뿐이고, 돌아온
/// 응답을 쿠키로 굽는 일은 완전히 같다. 그래서 그 일은 <see cref="BakeAsync"/>
/// 한 곳에만 있다 — 갈라 두면 한쪽에만 클레임을 더하는 날이 오고, 그때 증상은
/// 「지문으로 들어가면 토큰 갱신이 안 된다」처럼 원인과 멀다.
/// </summary>
public sealed class LoginService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<LoginService> logger)
{
    /// <summary>이 서비스가 쓰는 HttpClient 이름. Program.cs 에서 등록한다.</summary>
    public const string HttpClientName = "gateway-anonymous";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 「로그인 유지」를 골랐을 때 인증 쿠키가 사는 날수.
    ///
    /// <para>
    /// 기본 30일은 <b>지어낸 값이 아니다</b> — AuthServer 의 갱신 토큰 수명
    /// (<c>Auth:RefreshTokenDays</c>)이 30일이다. 인증 쿠키가 그보다 오래 살면
    /// 쿠키는 멀쩡한데 갱신이 거절되어 <b>로그인된 채로 아무 화면도 안 열리는</b>
    /// 상태가 된다. 둘 중 짧은 쪽이 실제 수명이므로 같은 값으로 맞춘다.
    /// </para>
    /// </summary>
    private int KeepSignedInDays => configuration.GetValue<int?>("Auth:KeepSignedInDays") ?? 30;

    /// <summary>
    /// 아이디·비밀번호로 로그인하고 셸의 인증 쿠키를 굽는다.
    /// </summary>
    /// <param name="httpContext">쿠키를 구울 요청. 정적 SSR 화면에서만 쓸 수 있다.</param>
    /// <param name="username">아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="keepSignedIn">
    /// 브라우저를 닫아도 로그인을 유지할 것인가. 자세한 것은
    /// <see cref="BuildProperties"/> 참고.
    /// </param>
    /// <param name="cancellationToken">취소 토큰</param>
    public Task<LoginResult> SignInAsync(
        HttpContext httpContext,
        string username,
        string password,
        bool keepSignedIn = false,
        CancellationToken cancellationToken = default)
        => SendAndBakeAsync(
            httpContext,
            "auth/login",
            new { username, password },
            fallbackName: username,
            keepSignedIn,
            "아이디 또는 비밀번호가 올바르지 않습니다.",
            cancellationToken);

    /// <summary>
    /// 패스키(지문·얼굴)로 로그인하고 셸의 인증 쿠키를 굽는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>브라우저가 게이트웨이를 직접 부르지 않는다.</b> 그렇게 하면 토큰이
    /// 브라우저까지 내려가 BFF 가 아니게 된다(web/CLAUDE.md 「인증 — BFF」).
    /// 브라우저가 하는 일은 기기에서 서명을 받아 <b>로그인 폼에 실어 보내는</b>
    /// 것까지고, 그 뒤는 여느 로그인과 똑같이 여기서 처리한다.
    /// </para>
    /// </remarks>
    /// <param name="httpContext">쿠키를 구울 요청</param>
    /// <param name="assertion">
    /// 브라우저가 만든 서명 묶음(JSON). <c>passkey.js</c> 가 폼의 감춘 칸에 넣는다.
    /// </param>
    /// <param name="keepSignedIn">로그인을 유지할 것인가</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public Task<LoginResult> SignInWithPasskeyAsync(
        HttpContext httpContext,
        string assertion,
        bool keepSignedIn = false,
        CancellationToken cancellationToken = default)
    {
        JsonElement body;
        try
        {
            body = JsonDocument.Parse(assertion).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "패스키 응답을 해석하지 못했다.");
            return Task.FromResult(new LoginResult(false, "기기 인증 값을 읽지 못했습니다. 다시 시도해 주세요."));
        }

        return SendAndBakeAsync(
            httpContext,
            "auth/webauthn/login",
            body,
            // 아이디를 치지 않고 들어오는 길이라 여기서는 이름을 모른다.
            // 발급된 토큰에서 꺼낸다(BakeAsync).
            fallbackName: null,
            keepSignedIn,
            "등록된 기기가 아니거나 인증에 실패했습니다.",
            cancellationToken);
    }

    /// <summary>
    /// 패스키 로그인에 쓸 도전값을 게이트웨이에서 받아 온다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>브라우저가 게이트웨이를 직접 부르지 않는 까닭.</b> 도전값 자체에는
    /// 비밀이 없지만, 브라우저가 <c>/api/</c> 로 직접 나가기 시작하면
    /// 「어디까지 직접 부르는가」의 경계가 흐려진다. 포털은 BFF 다 — 바깥으로
    /// 나가는 문을 셸 하나로 둔다.
    /// </para>
    ///
    /// <para>
    /// 봉투를 여기서 벗겨 <c>{ sessionId, publicKey }</c> 만 내보낸다.
    /// 브라우저에 봉투 해석 규칙을 하나 더 심어 둘 이유가 없다.
    /// </para>
    /// </remarks>
    /// <param name="username">아이디를 적어 넣었으면 그 값. 비워도 된다.</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>브라우저에 그대로 줄 JSON. 실패하면 <c>null</c>.</returns>
    public async Task<JsonElement?> GetPasskeyLoginOptionsAsync(
        string? username,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await client.PostAsJsonAsync(
                "auth/webauthn/login/options", new { username }, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("패스키 도전값을 받지 못했다 ({Status}).", (int)response.StatusCode);
                return null;
            }

            using var document = await response.Content
                .ReadFromJsonAsync<JsonDocument>(cancellationToken);

            if (document?.RootElement.TryGetProperty("data", out var data) is not true
                || !data.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array
                || result.GetArrayLength() == 0)
            {
                logger.LogWarning("패스키 도전값 응답의 모양이 다르다.");
                return null;
            }

            return result[0].Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            logger.LogError(ex, "패스키 도전값을 받아 오지 못했다.");
            return null;
        }
    }

    /// <summary>인증 쿠키를 지운다.</summary>
    public static Task SignOutAsync(HttpContext httpContext) =>
        httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    // ── 공통 ─────────────────────────────────────────────────────

    /// <summary>게이트웨이를 두드리고, 성공하면 쿠키를 굽는다.</summary>
    private async Task<LoginResult> SendAndBakeAsync(
        HttpContext httpContext,
        string path,
        object body,
        string? fallbackName,
        bool keepSignedIn,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "게이트웨이에 연결하지 못했다.");
            return new LoginResult(false, "서버에 연결하지 못했습니다. 잠시 뒤 다시 시도해 주세요.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // 아이디가 틀렸는지 비밀번호가 틀렸는지 구분해서 알리지 않는다 —
                // 구분해 주면 존재하는 계정을 골라내는 데 쓰인다.
                // 게이트웨이가 auth-attempts 정책으로 IP 당 분당 10회로 막고 있지만,
                // 그건 속도만 늦출 뿐 구분 자체를 막지는 못한다.
                logger.LogInformation("로그인 실패 ({Status}) — {Path}", (int)response.StatusCode, path);

                return new LoginResult(false, (int)response.StatusCode == 429
                    ? "로그인 시도가 너무 잦습니다. 잠시 뒤 다시 시도해 주세요."
                    : failureMessage);
            }

            var payload = await ReadLoginPayloadAsync(response, cancellationToken);
            if (payload is null or { AccessToken: null or "" })
            {
                logger.LogError("로그인 응답에서 accessToken 을 찾지 못했다.");
                return new LoginResult(false, "로그인 응답을 해석하지 못했습니다.");
            }

            await BakeAsync(httpContext, response, payload, fallbackName, keepSignedIn);
            return new LoginResult(true, PasswordExpired: payload.PasswordExpired);
        }
    }

    /// <summary>
    /// 받은 토큰을 셸의 인증 쿠키에 굽는다. <b>로그인 방법과 무관하게 여기 하나다.</b>
    /// </summary>
    private async Task BakeAsync(
        HttpContext httpContext,
        HttpResponseMessage response,
        LoginPayload payload,
        string? fallbackName,
        bool keepSignedIn)
    {
        var refreshCookie = ExtractRefreshCookie(response);
        if (refreshCookie is null)
        {
            // 없어도 로그인은 된다. 다만 access token 이 만료되면 갱신하지
            // 못해 다시 로그인해야 하므로, 조용히 넘기지 않고 남긴다.
            logger.LogWarning("로그인 응답에 리프레시 쿠키가 없다. 토큰 갱신이 되지 않는다.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, fallbackName ?? ReadUserId(payload.AccessToken!) ?? string.Empty),
            new(TokenStore.AccessTokenClaim, payload.AccessToken!),
        };

        if (refreshCookie is not null)
        {
            claims.Add(new Claim(TokenStore.RefreshCookieClaim, refreshCookie));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            BuildProperties(keepSignedIn));
    }

    /// <summary>
    /// 인증 쿠키의 수명을 정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「로그인 유지」를 안 고르면 아무것도 지정하지 않는다.</b> 그러면
    /// 세션 쿠키(브라우저를 닫으면 사라진다)에 <c>ExpireTimeSpan</c>(8시간)
    /// 미끄럼 만료가 걸린다 — 여태 하던 그대로다.
    /// </para>
    ///
    /// <para>
    /// 고르면 <c>IsPersistent</c> 와 함께 <c>ExpiresUtc</c> 를 <b>직접</b> 적는다.
    /// 그 값이 있으면 쿠키 미들웨어는 <c>ExpireTimeSpan</c> 대신 이 시각을 쓰고,
    /// 미끄럼 만료도 「발급~만료」 간격(=30일)으로 다시 계산한다. 그래서 계속
    /// 쓰는 사람은 30일 창이 계속 앞으로 밀린다.
    /// </para>
    ///
    /// <para>
    /// <b>이 값이 PWA 에서 특히 중요하다.</b> 홈 화면 앱은 며칠씩 뒤로 물러나
    /// 있다가 다시 열리는데, 세션 쿠키면 그사이 브라우저가 앱을 내렸다 올린
    /// 것만으로 로그인이 풀린다. 사용자에게는 「앱이 자꾸 로그아웃된다」로 보인다.
    /// </para>
    /// </remarks>
    private AuthenticationProperties? BuildProperties(bool keepSignedIn) =>
        keepSignedIn
            ? new AuthenticationProperties
            {
                IsPersistent = true,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(Math.Max(KeepSignedInDays, 1)),
            }
            : null;

    /// <summary>
    /// 로그인 응답에서 토큰과 비밀번호 만료 여부를 꺼낸다.
    ///
    /// 응답은 봉투에 싸여 온다 — 객체 하나도 <c>data.result[0]</c> 이다
    /// (<c>ApiResponse.BuildSerializedData</c>).
    /// </summary>
    private static async Task<LoginPayload?> ReadLoginPayloadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await response.Content
                .ReadFromJsonAsync<JsonDocument>(cancellationToken);

            if (document?.RootElement.TryGetProperty("data", out var data) is not true
                || !data.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array
                || result.GetArrayLength() == 0)
            {
                return null;
            }

            var first = result[0];
            return new LoginPayload(
                first.TryGetProperty("accessToken", out var token) ? token.GetString() : null,
                first.TryGetProperty("passwordExpired", out var expired)
                    && expired.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// access token 에서 로그인 아이디를 꺼낸다. <b>검증하지 않는다</b> —
    /// 이 값은 화면에 이름을 띄우는 데만 쓰고, 권한 판단은 전부 게이트웨이가 한다.
    /// 패스키 로그인처럼 <b>아이디를 미리 모르는</b> 길에서만 쓴다.
    /// </summary>
    private string? ReadUserId(string accessToken)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            var json = Convert.FromBase64String(Pad(parts[1].Replace('-', '+').Replace('_', '/')));
            var payload = JsonDocument.Parse(json).RootElement;

            // 토큰을 만드는 쪽(AccessTokenFactory)이 ClaimTypes.NameIdentifier 로
            // 담는다. JWT 로 직렬화되면 짧은 이름 `nameid` 가 되지만, 직렬화기
            // 설정에 따라 전체 URI 로 남는 경우도 있어 둘 다 본다.
            foreach (var key in (string[])
                     ["nameid", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "sub"])
            {
                if (payload.TryGetProperty(key, out var value) && value.GetString() is { Length: > 0 } id)
                {
                    return id;
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            logger.LogDebug(ex, "access token 에서 아이디를 읽지 못했다.");
            return null;
        }

        static string Pad(string value) =>
            value.PadRight(value.Length + ((4 - (value.Length % 4)) % 4), '=');
    }

    /// <summary>AuthServer 가 심는 갱신 쿠키 이름. 그쪽 <c>AccessTokenFactory</c> 와 짝이다.</summary>
    private const string RefreshCookieName = "jsini_rt";

    /// <summary>
    /// <c>Set-Cookie</c> 헤더에서 리프레시 쿠키를 꺼낸다.
    ///
    /// 다시 보낼 때 필요한 것은 <c>이름=값</c> 부분뿐이다. <c>Path</c>·
    /// <c>HttpOnly</c>·<c>Expires</c> 같은 속성은 브라우저에게 하는 지시라
    /// 서버가 서버에게 보낼 때는 실으면 안 된다.
    ///
    /// <para>
    /// <b>이름으로 찾는다.</b> 한동안 「첫 번째 것」을 썼는데, 로그인 응답에는
    /// 파일 읽기용 쿠키(<c>jsini_file_at</c>)도 함께 실려 있다. 그때는 갱신 쿠키가
    /// 아예 없어서 우연히 문제가 되지 않았을 뿐이고, 갱신 쿠키가 생긴 지금
    /// 순서에 기대면 <b>파일 쿠키를 갱신 쿠키로 착각</b>한다. 그러면 갱신이 늘
    /// 401 이고, 증상은 「일주일 뒤 갑자기 로그아웃」이다.
    /// </para>
    /// </summary>
    private string? ExtractRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            var pair = cookie.Split(';', 2)[0].Trim();
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (pair.AsSpan(0, separator).Equals(RefreshCookieName, StringComparison.Ordinal))
            {
                return pair;
            }
        }

        // 이름이 안 맞으면 조용히 넘기지 않는다 — AuthServer 가 이름을 바꾸면
        // 갱신만 멈추고 로그인은 멀쩡해서, 알아채는 데 며칠이 걸린다.
        logger.LogWarning(
            "로그인 응답에서 갱신 쿠키({Name})를 찾지 못했다. AuthServer 의 쿠키 이름이 바뀌었는지 확인한다.",
            RefreshCookieName);
        return null;
    }

    private sealed record LoginPayload(string? AccessToken, bool PasswordExpired);
}
