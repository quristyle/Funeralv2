using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using JSini.Web.Components.Security;
using Microsoft.Extensions.Caching.Memory;

namespace JSini.Web.Shell.Security;

/// <summary>로그인 결과.</summary>
/// <param name="Succeeded">성공 여부</param>
/// <param name="Message">실패했을 때 사용자에게 보여 줄 문구</param>
    /// <param name="PasswordExpired">비밀번호 사용 기간이 지났다. 토큰은 발급되므로 화면은 곧바로 비밀번호 변경으로 안내한다(다른 화면은 403 만 받는다).</param>
    /// <param name="PendingApproval">가입 신청이 접수된 상태(소셜 첫 로그인, 202). 실패로 뭉개면 사용자가 신청이 접수된 줄 모르고 단추를 누른다. 아이디·패스키 로그인에서는 항상 거짓이다.</param>
    /// <param name="Blocked">신원은 맞지만 쓸 수 없는 계정(승인 대기·정지, 403). 401 과 갈라 둔다. 다시 쳐 봐도 소용없는 일이라서다. 소셜 흐름만 주소로 이 갈래를 넘긴다.</param>
public readonly record struct LoginResult(
    bool Succeeded,
    string? Message = null,
    bool PasswordExpired = false,
    bool PendingApproval = false,
    bool Blocked = false);

/// <summary>
/// 로그인·로그아웃. 게이트웨이에 묻고, 응답으로 셸의 인증 쿠키를 굽는다.
/// 맨 HttpClient 를 쓴다 — Set-Cookie(리프레시 토큰)를 읽어야 하고 로그인 시점에는 붙일 토큰도 없다.
/// 비밀번호와 패스키는 게이트웨이 경로만 다르고, 굽는 일(<see cref="BakeAsync"/>)은 한 곳에 둔다.
/// </summary>
public sealed class LoginService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<LoginService> logger)
{
    /// <summary>이 서비스가 쓰는 HttpClient 이름. Program.cs 에서 등록한다.</summary>
    public const string HttpClientName = "gateway-anonymous";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>「로그인 유지」를 고른 인증 쿠키 수명(30일). AuthServer 의 갱신 토큰 수명과 맞춘다. 쿠키가 더 오래 살면 갱신이 거절되어 로그인된 채로 화면이 안 열린다.</summary>
    private int KeepSignedInDays => configuration.GetValue<int?>("Auth:KeepSignedInDays") ?? 30;

    /// <summary>아이디·비밀번호로 로그인하고 인증 쿠키를 굽는다.</summary>
    /// <param name="httpContext">쿠키를 구울 요청(정적 SSR 화면에서만 쓸 수 있다).</param>
    /// <param name="username">아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="keepSignedIn">브라우저를 닫아도 유지할지(<see cref="BuildProperties"/>).</param>
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

    /// <summary>패스키(지문·얼굴)로 로그인하고 인증 쿠키를 굽는다. 브라우저는 서명만 만들어 폼에 실어 보내고, 게이트웨이는 여기서 부른다(BFF).</summary>
    /// <param name="httpContext">쿠키를 구울 요청</param>
    /// <param name="assertion">브라우저가 만든 서명 묶음(JSON). <c>passkey.js</c> 가 폼의 감춘 칸에 넣는다.</param>
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
            // 아이디를 치지 않는 길이라 이름은 발급된 토큰에서 꺼낸다(BakeAsync).
            fallbackName: null,
            keepSignedIn,
            "등록된 기기가 아니거나 인증에 실패했습니다.",
            cancellationToken);
    }

    /// <summary>패스키 로그인에 쓸 도전값을 게이트웨이에서 받는다. 브라우저가 게이트웨이를 직접 부르지 않는다(BFF). 봉투를 벗겨 <c>{ sessionId, publicKey }</c> 만 내보낸다.</summary>
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

    // ── 소셜 (구글 · 네이버 · 카카오) ───────────────────────────

    /// <summary>소셜 공급자 목록을 담아 두는 자리. 사람마다 다르지 않아 한 통을 같이 쓴다.</summary>
    private const string SocialProvidersCacheKey = "JSini.Web.Shell.SocialProviders";

    /// <summary>담아 두는 시간. 짧게 둔다. 공급자 열쇠를 넣은 뒤 단추가 설 때까지의 시간이라 길면 「왜 안 나오지」를 겪는다.</summary>
    private static readonly TimeSpan SocialProvidersCacheLifetime = TimeSpan.FromMinutes(5);

    /// <summary>담아 둔 공급자 목록을 버린다. 실패한 뒤에 부른다(빈 목록을 담으면 5분 동안 단추가 사라진다).</summary>
    public void ForgetSocialProviders() => cache.Remove(SocialProvidersCacheKey);

    /// <summary>로그인 화면에 그릴 소셜 단추 목록(설정된 공급자만). 실패해도 예외를 던지지 않는다. 소셜 단추를 못 그린다고 아이디·비밀번호 로그인이 막히면 안 된다.</summary>
    public async Task<IReadOnlyList<SocialProvider>> GetSocialProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        // 로그인 화면이 뜰 때마다 부르므로 잠깐 담는다. 사람마다 다르지 않고, 공급자를 켜면 5분 안에 단추가 선다.
        if (cache.TryGetValue(SocialProvidersCacheKey, out IReadOnlyList<SocialProvider>? cached)
            && cached is not null)
        {
            return cached;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await client.GetAsync("auth/social/providers", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("소셜 공급자 목록을 받지 못했다 ({Status}).", (int)response.StatusCode);
                return [];
            }

            using var document = await response.Content
                .ReadFromJsonAsync<JsonDocument>(cancellationToken);

            if (document?.RootElement.TryGetProperty("data", out var data) is not true
                || !data.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var providers = new List<SocialProvider>();
            foreach (var item in result.EnumerateArray())
            {
                var key = item.TryGetProperty("provider", out var p) ? p.GetString() : null;
                var name = item.TryGetProperty("displayName", out var d) ? d.GetString() : null;

                if (!string.IsNullOrWhiteSpace(key))
                {
                    providers.Add(new SocialProvider(key, name ?? key));
                }
            }

            cache.Set(SocialProvidersCacheKey, (IReadOnlyList<SocialProvider>)providers,
                SocialProvidersCacheLifetime);

            return providers;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "소셜 공급자 목록을 받아 오지 못했다.");
            return [];
        }
    }

    /// <summary>공급자의 인가 화면 주소를 받아 온다(못 받으면 null). 주소는 셸이 짓지 않는다. client_id 가 AuthServer 설정과 한 벌이라 프론트에 두면 어긋난다. redirectUri 만 셸이 정한다(게이트웨이 뒤라 포털의 바깥 주소를 모른다).</summary>
    public async Task<string?> GetSocialAuthorizeUrlAsync(
        string provider, string redirectUri, string state,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await client.PostAsJsonAsync(
                "auth/social/authorize",
                new { provider, redirectUri, state },
                JsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "{Provider} 인가 주소를 받지 못했다 ({Status}).", provider, (int)response.StatusCode);
                return null;
            }

            using var document = await response.Content
                .ReadFromJsonAsync<JsonDocument>(cancellationToken);

            if (document?.RootElement.TryGetProperty("data", out var data) is not true
                || !data.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array
                || result.GetArrayLength() == 0)
            {
                return null;
            }

            return result[0].TryGetProperty("url", out var url) ? url.GetString() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "{Provider} 인가 주소를 받아 오지 못했다.", provider);
            return null;
        }
    }

    /// <summary>공급자가 준 인가 코드로 로그인하고 인증 쿠키를 굽는다. 코드는 브라우저가 게이트웨이로 직접 보내지 않는다(BFF). 처음 온 사람은 승인 대기로 만들어져 PendingApproval 이 참이고 쿠키는 굽지 않는다.</summary>
    public Task<LoginResult> SignInWithSocialAsync(
        HttpContext httpContext,
        string provider,
        string code,
        string redirectUri,
        string? state,
        bool keepSignedIn = false,
        CancellationToken cancellationToken = default)
        => SendAndBakeAsync(
            httpContext,
            "auth/social/login",
            new { provider, code, redirectUri, state },
            // 아이디를 치지 않는 길이라 이름은 발급된 토큰에서 꺼낸다(BakeAsync).
            fallbackName: null,
            keepSignedIn,
            "소셜 계정으로 로그인하지 못했습니다. 잠시 뒤 다시 시도해 주세요.",
            cancellationToken);

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
                // 아이디와 비밀번호 중 어느 쪽이 틀렸는지 알리지 않는다. 구분해 주면 존재하는 계정을 골라내는 데 쓰인다.
                logger.LogInformation("로그인 실패 ({Status}) — {Path}", (int)response.StatusCode, path);

                if ((int)response.StatusCode == 429)
                {
                    return new LoginResult(false, "로그인 시도가 너무 잦습니다. 잠시 뒤 다시 시도해 주세요.");
                }

                // 403 은 신원이 밝혀진 뒤의 말(승인 대기·정지)이라 그대로 옮긴다. 뭉뚱그리면 소셜 신청자가 신청이 어디까지 갔는지 모른 채 단추를 누른다.
                if ((int)response.StatusCode == 403)
                {
                    var reason = await ReadMessageAsync(response, cancellationToken);
                    return new LoginResult(false, reason ?? failureMessage, Blocked: true);
                }

                return new LoginResult(false, failureMessage);
            }

            // 202 는 계정은 만들어졌지만 토큰이 없다. 여기서 가르지 않으면 아래 accessToken 찾기가 실패해 「해석하지 못했습니다」가 뜬다(사실과 다르다). 소셜 로그인에서만 나온다.
            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                logger.LogInformation("소셜 가입 신청이 접수됐다 — {Path}", path);

                var notice = await ReadMessageAsync(response, cancellationToken);
                return new LoginResult(
                    Succeeded: false,
                    notice ?? "가입 신청을 받았습니다. 관리자 승인 뒤에 로그인하실 수 있습니다.",
                    PendingApproval: true);
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

    /// <summary>받은 토큰을 셸의 인증 쿠키에 굽는다. 로그인 방법과 무관하게 여기 하나다.</summary>
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
            // 없어도 로그인은 된다. 다만 갱신이 안 되므로 조용히 넘기지 않고 남긴다.
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

    /// <summary>인증 쿠키의 수명. 「로그인 유지」를 안 고르면 세션 쿠키에 8시간 미끄럼 만료를 건다. 고르면 ExpiresUtc 를 직접 적어 미끄럼 창이 30일로 앞으로 밀린다. 홈 화면 앱은 며칠 뒤 다시 열리므로 세션 쿠키면 로그인이 풀린다.</summary>
    private AuthenticationProperties? BuildProperties(bool keepSignedIn) =>
        keepSignedIn
            ? new AuthenticationProperties
            {
                IsPersistent = true,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(Math.Max(KeepSignedInDays, 1)),
            }
            : null;

    /// <summary>로그인 응답에서 토큰과 비밀번호 만료 여부를 꺼낸다. 응답은 봉투 안에 있다(<c>data.result[0]</c>).</summary>
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

    /// <summary>access token 에서 아이디를 꺼낸다. 검증하지 않는다. 이름을 띄우는 데만 쓰고 권한은 게이트웨이가 판단한다. 아이디를 미리 모르는 패스키 길에서만 쓴다.</summary>
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

            // 발급 쪽은 ClaimTypes.NameIdentifier 로 담는다. JWT 직렬화 설정에 따라 nameid 또는 전체 URI 로 남으므로 둘 다 본다.
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

    /// <summary>Set-Cookie 에서 갱신 쿠키의 <c>이름=값</c> 부분만 꺼낸다. 이름으로 찾는다. 로그인 응답에는 파일 읽기 쿠키도 함께 실려, 순서에 기대면 그것을 갱신 쿠키로 착각한다.</summary>
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

        // 이름이 안 맞으면 조용히 넘기지 않는다. 갱신만 멈추고 로그인은 멀쩡해 알아채는 데 며칠이 걸린다.
        logger.LogWarning(
            "로그인 응답에서 갱신 쿠키({Name})를 찾지 못했다. AuthServer 의 쿠키 이름이 바뀌었는지 확인한다.",
            RefreshCookieName);
        return null;
    }

    /// <summary>봉투의 message 한 줄. 서버가 지어 준 안내를 화면이 그대로 말하게 하며, 없으면 부르는 쪽이 제 문구를 쓴다.</summary>
    private static async Task<string?> ReadMessageAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await response.Content
                .ReadFromJsonAsync<JsonDocument>(cancellationToken);

            return document?.RootElement.TryGetProperty("message", out var message) is true
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record LoginPayload(string? AccessToken, bool PasswordExpired);
}

/// <summary>로그인 화면에 그릴 소셜 단추 하나.</summary>
/// <param name="Key">공급자 열쇠 (<c>google</c> · <c>naver</c> · <c>kakao</c>). 시작 주소에 들어간다.</param>
/// <param name="DisplayName">단추에 적을 이름.</param>
public sealed record SocialProvider(string Key, string DisplayName);
