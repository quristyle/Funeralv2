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
/// <param name="PasswordExpired">
/// 비밀번호 사용 기간이 지났는가.
///
/// 지났어도 토큰은 정상 발급된다 — 비밀번호를 바꾸려면 로그인 상태여야 하기
/// 때문이다. 대신 게이트웨이가 비밀번호 변경에 필요한 경로만 통과시키므로,
/// 화면은 이 값을 보고 곧바로 비밀번호 변경으로 안내해야 한다. 안 그러면
/// 사용자는 아무 화면이나 열 때마다 403 만 보게 된다.
/// </param>
/// <param name="PendingApproval">
/// 로그인이 아니라 <b>가입 신청이 접수된</b> 상태인가.
///
/// <para>
/// 소셜(구글·네이버·카카오)로 <b>처음</b> 들어온 사람에게만 참이 된다. 그때
/// 서버는 승인 대기 계정을 만들어 두고 <c>202 Accepted</c> 로 답한다 —
/// 성공도 실패도 아니라서 갈래가 하나 더 필요하다. 이것을 실패로 뭉뚱그리면
/// 화면이 「인증에 실패했습니다」라고 말하게 되고, 사용자는 자기 신청이
/// 접수됐다는 것을 모른 채 단추를 계속 누른다.
/// </para>
///
/// <para>
/// 아이디·비밀번호 로그인과 패스키 로그인에서는 <b>언제나 거짓</b>이다 —
/// 그 길에는 계정이 저절로 만들어지는 자리가 없다.
/// </para>
/// </param>
/// <param name="Blocked">
/// 신원은 맞았는데 <b>쓸 수 없는 계정</b>인가 (승인 대기 · 정지).
///
/// <para>
/// 서버가 403 으로 답한 경우다. 아이디·비밀번호가 틀린 401 과 갈라 두는 이유는
/// <b>사용자가 할 일이 다르기</b> 때문이다 — 401 은 다시 쳐 보면 되고, 이쪽은
/// 아무리 다시 눌러도 관리자가 승인하기 전까지 들어올 수 없다.
/// </para>
///
/// <para>
/// 화면이 폼 위에 <see cref="Message"/> 를 그대로 띄우면 이 값을 볼 일이 없다.
/// 주소로 갈래만 넘기는 소셜 흐름(<c>SocialLoginFlow</c>)에서만 쓴다 —
/// 거기서는 문구를 주소에 실을 수 없어서(남이 아무 말이나 띄우는 길이 된다)
/// 갈래를 알아야 제 문구를 고를 수 있다.
/// </para>
/// </param>
public readonly record struct LoginResult(
    bool Succeeded,
    string? Message = null,
    bool PasswordExpired = false,
    bool PendingApproval = false,
    bool Blocked = false);

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
    IMemoryCache cache,
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

    // ── 소셜 (구글 · 네이버 · 카카오) ───────────────────────────

    /// <summary>소셜 공급자 목록을 담아 두는 자리. 사람마다 다르지 않아 한 통을 같이 쓴다.</summary>
    private const string SocialProvidersCacheKey = "JSini.Web.Shell.SocialProviders";

    /// <summary>
    /// 담아 두는 시간. <b>짧게 둔다</b> — 공급자 열쇠를 넣고 나서 단추가 설
    /// 때까지 기다리는 시간이 이 값이고, 그동안 「왜 안 나오지」를 겪게 된다.
    /// </summary>
    private static readonly TimeSpan SocialProvidersCacheLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 담아 둔 공급자 목록을 버린다. <b>실패한 뒤에 부른다</b> — 게이트웨이가
    /// 잠깐 답하지 않아 빈 목록을 담아 두면 5분 동안 단추가 사라진다.
    /// </summary>
    public void ForgetSocialProviders() => cache.Remove(SocialProvidersCacheKey);

    /// <summary>
    /// 로그인 화면에 그릴 소셜 단추 목록. <b>설정된 공급자만</b> 온다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 목록을 화면에 박아 두지 않는 이유는, 열쇠를 아직 받지 못한 공급자의
    /// 단추가 그려지면 <b>눌러 본 사람이 고장으로 신고하기</b> 때문이다.
    /// 어느 것이 설정됐는지는 AuthServer 만 안다(비밀 열쇠가 거기 있다).
    /// </para>
    /// <para>
    /// <b>실패해도 예외를 던지지 않는다.</b> 소셜 단추를 못 그리는 것이
    /// 로그인 화면 전체를 못 여는 이유가 되어서는 안 된다 — 아이디·비밀번호는
    /// 그대로 되어야 한다.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<SocialProvider>> GetSocialProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        // **로그인 화면이 뜰 때마다 부르는 값이라 잠깐 담아 둔다.**
        //
        // 이 화면은 모두가 가장 먼저 받는 화면이고(회로도 DevExpress 도 없앤
        // 자리다), 여기에 게이트웨이 왕복을 하나 더 얹으면 그 일이 헛것이 된다.
        // 담는 것은 **설정에서 온 값**이라 사람마다 다르지 않고 자주 바뀌지도
        // 않는다 — 공급자를 새로 켜면 5분 안에 단추가 선다.
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

    /// <summary>
    /// 공급자의 인가 화면 주소를 받아 온다. 못 받으면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>주소를 여기서 짓지 않는 까닭.</b> 주소에는 <c>client_id</c> 가 들어가고
    /// 그 값은 비밀 열쇠와 한 벌로 AuthServer 설정에 있다. 셸이 지으려면 그
    /// 값을 프론트 설정에 한 벌 더 두어야 하고, 그러면 <b>둘이 어긋나는 날</b>이
    /// 온다 — 그때 증상은 공급자가 던지는 <c>invalid_client</c> 하나다.
    /// </para>
    /// <para>
    /// 반대로 <paramref name="redirectUri"/> 는 <b>셸이 정한다.</b> AuthServer 는
    /// 게이트웨이 뒤라 포털의 바깥 주소(도메인·스킴)를 모른다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 공급자가 되돌려 준 인가 코드로 로그인하고 셸의 인증 쿠키를 굽는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>코드를 브라우저가 게이트웨이로 직접 보내지 않는다.</b> 그렇게 하면
    /// 토큰이 브라우저까지 내려가 BFF 가 아니게 된다(web/CLAUDE.md 「인증 — BFF」).
    /// 브라우저가 하는 일은 공급자에게 다녀와 코드를 <b>셸의 콜백 주소로
    /// 들고 오는</b> 것까지고, 그 뒤는 여느 로그인과 똑같이 여기서 처리한다.
    /// </para>
    /// <para>
    /// 처음 온 사람이면 계정이 <b>승인 대기</b>로 만들어지고 결과의
    /// <see cref="LoginResult.PendingApproval"/> 가 참으로 온다. 쿠키는 굽지 않는다.
    /// </para>
    /// </remarks>
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
            // 아이디를 치지 않고 들어오는 길이라 여기서는 이름을 모른다.
            // 발급된 토큰에서 꺼낸다(BakeAsync).
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
                // 아이디가 틀렸는지 비밀번호가 틀렸는지 구분해서 알리지 않는다 —
                // 구분해 주면 존재하는 계정을 골라내는 데 쓰인다.
                // 게이트웨이가 auth-attempts 정책으로 IP 당 분당 10회로 막고 있지만,
                // 그건 속도만 늦출 뿐 구분 자체를 막지는 못한다.
                logger.LogInformation("로그인 실패 ({Status}) — {Path}", (int)response.StatusCode, path);

                if ((int)response.StatusCode == 429)
                {
                    return new LoginResult(false, "로그인 시도가 너무 잦습니다. 잠시 뒤 다시 시도해 주세요.");
                }

                // ── 403 은 서버가 지어 준 말을 그대로 옮긴다 ─────────
                //
                // **401 과 갈라 두는 것이 요점이다.** 401 은 신원 확인이 실패한
                // 것이라 까닭을 말하면 안 된다(위 주석). 403 은 **신원이 이미
                // 밝혀진 뒤**에 나온다 — 「승인 대기 중」 · 「정지된 계정」이고,
                // 그 사실은 본인에게 숨길 것이 아니다(AuthServer 의
                // `RejectIfNotActiveAsync` 가 그 판단으로 문구를 짓는다).
                //
                // 뭉뚱그리면 소셜로 두 번째 누른 사람이 「로그인하지
                // 못했습니다」만 보고 **자기 신청이 어디까지 갔는지 모른 채**
                // 단추를 계속 누른다.
                if ((int)response.StatusCode == 403)
                {
                    var reason = await ReadMessageAsync(response, cancellationToken);
                    return new LoginResult(false, reason ?? failureMessage, Blocked: true);
                }

                return new LoginResult(false, failureMessage);
            }

            // ── 202 = 「가입 신청을 받았다」 ─────────────────────
            //
            // **200 과 갈라 둔 것이 요점이다.** 202 는 계정이 만들어졌지만 아직
            // 쓸 수 없다는 뜻이라 토큰이 없다. 여기서 가르지 않으면 아래
            // `accessToken` 찾기가 실패해서 「응답을 해석하지 못했습니다」가
            // 뜨고, 그 문구는 **사실과 다르다** — 해석은 됐고 신청이 접수된 것이다.
            //
            // 소셜 로그인에서만 나온다(AuthServer 의 `SocialLoginEndpoints`).
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

    /// <summary>
    /// 봉투의 <c>message</c> 한 줄만 꺼낸다. 서버가 지어 준 안내를 화면이
    /// 그대로 말하게 하려는 것이고, 없으면 부르는 쪽이 제 문구를 쓴다.
    /// </summary>
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
