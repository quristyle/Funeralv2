using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Http;

/// <summary>
/// 나가는 요청마다 토큰을 붙이고, 401 이면 한 번 갱신해서 다시 보낸다. 언어 헤더도 여기서 붙인다.
/// 이 인스턴스는 모든 사용자가 함께 쓴다. 사람마다 다른 값을 칸에 두지 않는다. 두면 남의 요청에 실려 남의 이름·권한으로 화면이 뜬다. 누구의 토큰인지는 지금 요청의 인증 쿠키(TokenStore · RefreshedTokens)로 갈린다.
/// </summary>
public sealed class AuthTokenHandler(
    ITokenStore tokens,
    ILogger<AuthTokenHandler> logger,
    Uri gatewayBaseAddress)
    : DelegatingHandler
{
    /// <summary>갱신 요청 자체가 401 을 받았을 때 다시 갱신하러 들어가지 않도록 세우는 표시. 없으면 만료된 리프레시 쿠키 하나로 무한히 돈다.</summary>
    private static readonly HttpRequestOptionsKey<bool> SkipRefresh = new("JSini.SkipRefresh");

    /// <summary>이 요청이 실제로 붙여 보낸 토큰. 갱신 여부는 통이 아니라 이 값으로 가른다(옆 요청이 이미 갱신했으면 같은 갱신이 또 나가지 않게).</summary>
    private static readonly HttpRequestOptionsKey<string> SentToken = new("JSini.SentToken");

    /// <summary>갱신이 겹치지 않게 한다. 여러 호출이 동시에 401 을 받아도 갱신은 한 번만 나간다.</summary>
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 부르는 쪽이 직접 실어 준 토큰(앱알림 아이콘 중계)이면 신원이 아니므로, 갱신해 봐야 바뀌는 것이 없다.
        var callerSuppliedToken = request.Headers.Authorization is not null;

        await AttachAsync(request, cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (callerSuppliedToken
            || (request.Options.TryGetValue(SkipRefresh, out var skip) && skip))
        {
            return response;
        }

        // 이 요청이 들고 나간 토큰. 통을 다시 묻지 않는다(SentToken 머리말).
        request.Options.TryGetValue(SentToken, out var sentToken);

        response.Dispose();

        var (refreshed, sessionRejected) = await TryRefreshAsync(sentToken, cancellationToken);

        if (!refreshed)
        {
            // 갱신할 길이 없어도 토큰은 버리지 않는다. 버리면 스코프 끝까지 토큰 없이 나가 화면 전체가 「로그인이 필요합니다」가 된다. 세션이 죽었다면 다음 호출이 401 을 받아 그때 셸이 로그인으로 보낸다.
            if (sessionRejected)
            {
                tokens.Clear();
            }

            // 401 은 그대로 돌려준다. GatewayClient 가 IsUnauthorized 로 바꿔 셸이 로그인으로 보낸다.
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = request,
            };
        }

        using var retry = await CloneAsync(request, cancellationToken);

        // 옛 토큰을 떼고 보낸다. 복제가 헤더를 베끼므로 떼지 않으면 갱신하고도 옛 토큰으로 다시 물어 또 401 이 난다.
        // 만료를 처음 밟은 묶음만 빈손이 되는 것이 「새로고침하면 되는데」의 정체다.
        retry.Headers.Authorization = null;

        await AttachAsync(retry, cancellationToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    /// <summary>토큰과 언어를 붙인다. 이미 붙은 Authorization 은 덮지 않는다(부르는 쪽이 직접 실은 토큰, 예: 알림 아이콘 중계의 bearer).</summary>
    private async Task AttachAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null
            && await tokens.GetAccessTokenAsync(cancellationToken) is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 401 때 무엇을 들고 나갔는지 알 수 있는 자리가 여기뿐이다. 갱신 판정이 이 값으로 갈린다.
            request.Options.Set(SentToken, token);
        }

        // 백엔드가 Accept-Language 를 보므로 언어는 계속 붙여 둔다.
        if (!request.Headers.Contains("Accept-Language"))
        {
            request.Headers.Add("Accept-Language", "ko-KR");
        }
    }

    /// <summary>access token 을 갱신한다. Refreshed 는 성공, SessionRejected 는 갱신 요청이 세션을 거절했을 때(401·403)만 참이다. 그때만 토큰을 버린다. 404 나 연결 실패는 세션이 죽었다는 근거가 아니다.</summary>
    /// <param name="sentToken">401 을 받은 요청이 들고 나갔던 토큰. 통의 것이 이미 다르면 옆 요청이 먼저 갱신한 것이다.</param>
    /// <param name="cancellationToken">취소 토큰</param>
    private async Task<(bool Refreshed, bool SessionRejected)> TryRefreshAsync(
        string? sentToken, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // 기다리는 사이에 다른 요청이 갱신을 끝냈다.
            var current = await tokens.GetAccessTokenAsync(cancellationToken);
            if (!string.Equals(current, sentToken, StringComparison.Ordinal))
            {
                return (current is { Length: > 0 }, false);
            }

            if (await tokens.GetRefreshCookieAsync(cancellationToken) is not { Length: > 0 } cookie)
            {
                return (false, false);
            }

            // BaseAddress 는 HttpClient 의 것이라 아래 base.SendAsync 가 건너뛴다. 상대 경로면 SocketsHttpHandler 가 InvalidOperationException 을 던져 갱신이 한 번도 성공하지 못했다. 절대 주소여야 한다.
            using var request = new HttpRequestMessage(
                HttpMethod.Post, new Uri(gatewayBaseAddress, "auth/refresh"));
            request.Options.Set(SkipRefresh, true);
            request.Headers.Add("Cookie", cookie);

            using var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // 401·403 만 세션이 죽었다고 읽는다. 404 는 갱신 경로가 없다는 뜻(RefreshTokenDays 0)일 뿐이라 토큰을 버리지 않는다.
                var rejected =
                    response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

                logger.Log(
                    rejected ? LogLevel.Information : LogLevel.Debug,
                    "토큰 갱신 실패 ({Status}). 세션 거절 여부: {Rejected}",
                    (int)response.StatusCode, rejected);

                return (false, rejected);
            }

            var token = await ReadTokenAsync(response, cancellationToken);
            if (token is not { Length: > 0 })
            {
                logger.LogWarning("토큰 갱신 응답에서 access token 을 찾지 못했다.");
                return (false, false);
            }

            tokens.UpdateAccessToken(token);
            return (true, false);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "토큰 갱신 중 서버에 연결하지 못했다.");
            return (false, false);
        }
        catch (InvalidOperationException ex)
        {
            // 갱신이 깨져도 오류 화면으로 번지지 않는다. 갱신 실패는 로그인으로 보내는 일이다. 우리 쪽이 요청을 못 만든 것이므로 토큰도 버리지 않는다.
            logger.LogError(ex, "토큰 갱신 요청을 만들지 못했다.");
            return (false, false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>갱신 응답에서 토큰을 꺼낸다. data 에 토큰 문자열이 그대로 오거나 data.result[0] 로 감싸여 오므로 둘 다 받는다.</summary>
    private static async Task<string?> ReadTokenAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await response.Content
                .ReadFromJsonAsync<JsonDocument>(cancellationToken);
            if (document is null)
            {
                return null;
            }

            if (!document.RootElement.TryGetProperty("data", out var data))
            {
                return null;
            }

            if (data.ValueKind == JsonValueKind.String)
            {
                return data.GetString();
            }

            if (data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.Array
                && result.GetArrayLength() > 0)
            {
                var first = result[0];
                return first.ValueKind switch
                {
                    JsonValueKind.String => first.GetString(),
                    JsonValueKind.Object when first.TryGetProperty("accessToken", out var t)
                        => t.GetString(),
                    _ => null,
                };
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>재시도용으로 요청을 복제한다. 보낸 요청은 다시 못 보내므로 본문을 바이트로 떠서 새 요청을 만든다.</summary>
    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            clone.Content = content;
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshLock.Dispose();
        }
        base.Dispose(disposing);
    }
}
