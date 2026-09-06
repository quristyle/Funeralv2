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
/// </summary>
public sealed class LoginService(
    IHttpClientFactory httpClientFactory,
    ILogger<LoginService> logger)
{
    /// <summary>이 서비스가 쓰는 HttpClient 이름. Program.cs 에서 등록한다.</summary>
    public const string HttpClientName = "gateway-anonymous";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 로그인하고 셸의 인증 쿠키를 굽는다.
    /// </summary>
    /// <param name="httpContext">쿠키를 구울 요청. 정적 SSR 화면에서만 쓸 수 있다.</param>
    /// <param name="username">아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<LoginResult> SignInAsync(
        HttpContext httpContext,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(
                "auth/login", new { username, password }, JsonOptions, cancellationToken);
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
                logger.LogInformation("로그인 실패 ({Status})", (int)response.StatusCode);
                return new LoginResult(false, (int)response.StatusCode == 429
                    ? "로그인 시도가 너무 잦습니다. 잠시 뒤 다시 시도해 주세요."
                    : "아이디 또는 비밀번호가 올바르지 않습니다.");
            }

            var payload = await ReadLoginPayloadAsync(response, cancellationToken);
            if (payload is null or { AccessToken: null or "" })
            {
                logger.LogError("로그인 응답에서 accessToken 을 찾지 못했다.");
                return new LoginResult(false, "로그인 응답을 해석하지 못했습니다.");
            }

            var refreshCookie = ExtractRefreshCookie(response);
            if (refreshCookie is null)
            {
                // 없어도 로그인은 된다. 다만 access token 이 만료되면 갱신하지
                // 못해 다시 로그인해야 하므로, 조용히 넘기지 않고 남긴다.
                logger.LogWarning("로그인 응답에 리프레시 쿠키가 없다. 토큰 갱신이 되지 않는다.");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, username),
                new(TokenStore.AccessTokenClaim, payload.AccessToken!),
            };

            if (refreshCookie is not null)
            {
                claims.Add(new Claim(TokenStore.RefreshCookieClaim, refreshCookie));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return new LoginResult(true, PasswordExpired: payload.PasswordExpired);
        }
    }

    /// <summary>인증 쿠키를 지운다.</summary>
    public static Task SignOutAsync(HttpContext httpContext) =>
        httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

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
