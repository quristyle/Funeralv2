using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Http;

/// <summary>
/// 나가는 요청마다 토큰을 붙이고, 401 이면 한 번 갱신해서 다시 보낸다.
///
/// Vue 의 <c>api/request.ts</c> 에 흩어져 있던 요청 인터셉터
/// (<c>Authorization</c> 붙이기 · <c>Accept-Language</c> 붙이기 ·
/// <c>authenticateResponseInterceptor</c> 의 갱신 로직)를 한 군데로 모은 것이다.
/// 화면은 이제 토큰을 아예 모른다.
/// </summary>
public sealed class AuthTokenHandler(
    ITokenStore tokens,
    ILogger<AuthTokenHandler> logger)
    : DelegatingHandler
{
    /// <summary>
    /// 갱신 요청 자체가 401 을 받았을 때 다시 갱신하러 들어가지 않도록 세워 두는 표시.
    /// 없으면 만료된 리프레시 쿠키 하나로 무한히 돌 수 있다.
    /// </summary>
    private static readonly HttpRequestOptionsKey<bool> SkipRefresh = new("JSini.SkipRefresh");

    /// <summary>
    /// 한 회로 안에서 갱신이 겹치지 않게 한다. 화면 하나가 API 를 대여섯 개
    /// 동시에 부르는 일이 흔한데, 모두 401 을 받으면 갱신도 그 수만큼 나간다.
    /// 그러면 마지막 것만 살아남고 앞선 토큰들은 즉시 무효가 된다.
    /// </summary>
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>
    /// 갱신 요청이 <b>세션을 거절했는가</b>(401·403). 참일 때만 토큰을 버린다.
    ///
    /// 갱신 경로가 아예 없거나(404) 서버에 닿지 못한 것은 세션이 죽었다는
    /// 근거가 아니다. 그것으로 토큰을 버리면 멀쩡한 토큰까지 잃는다.
    /// </summary>
    private bool _sessionRejected;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await AttachAsync(request, cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (request.Options.TryGetValue(SkipRefresh, out var skip) && skip)
        {
            return response;
        }

        // 401 을 받은 시점의 토큰. 갱신을 기다리는 동안 다른 요청이 이미 갱신해
        // 두었다면 이 값과 달라져 있을 것이고, 그러면 갱신을 또 할 필요가 없다.
        var staleToken = await tokens.GetAccessTokenAsync(cancellationToken);

        response.Dispose();

        var refreshed = await TryRefreshAsync(staleToken, cancellationToken);

        if (!refreshed)
        {
            // **갱신할 길이 없을 때는 토큰을 버리지 않는다.**
            //
            // 버리면 그 표시가 스코프 끝까지 남아서, 이 뒤의 호출이 전부 토큰
            // 없이 나가고 화면 전체가 「로그인이 필요합니다」로 끝난다.
            // 스쳐 지나갈 401 하나가 로그아웃으로 번지는 것이다 — 실제로
            // 화면 열몇 개가 그렇게 들쭉날쭉하게 실패하고 있었다.
            //
            // 정말로 세션이 죽었다면 다음 호출도 401 을 받는다. 그때 셸이
            // 로그인으로 보내므로 늦지 않는다.
            if (_sessionRejected)
            {
                tokens.Clear();
            }

            // 401 을 그대로 돌려주면 GatewayClient 가
            // ApiException(IsUnauthorized) 로 바꾸고, 셸이 로그인으로 보낸다.
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = request,
            };
        }

        using var retry = await CloneAsync(request, cancellationToken);
        await AttachAsync(retry, cancellationToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    /// <summary>토큰과 언어를 붙인다.</summary>
    private async Task AttachAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (await tokens.GetAccessTokenAsync(cancellationToken) is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 서버가 오류 메시지를 이 언어로 내려준다. 지금은 한국어뿐이지만
        // 백엔드가 Accept-Language 를 보고 있으므로 계속 붙여 둔다.
        if (!request.Headers.Contains("Accept-Language"))
        {
            request.Headers.Add("Accept-Language", "ko-KR");
        }
    }

    /// <summary>
    /// access token 을 갱신한다. 성공하면 참.
    /// </summary>
    /// <param name="staleToken">401 을 받은 시점의 토큰. 이미 바뀌었으면 갱신을 건너뛴다.</param>
    /// <param name="cancellationToken">취소 토큰</param>
    private async Task<bool> TryRefreshAsync(string? staleToken, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // 기다리는 사이에 다른 요청이 갱신을 끝냈다.
            var current = await tokens.GetAccessTokenAsync(cancellationToken);
            if (!string.Equals(current, staleToken, StringComparison.Ordinal))
            {
                return current is { Length: > 0 };
            }

            if (await tokens.GetRefreshCookieAsync(cancellationToken) is not { Length: > 0 } cookie)
            {
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
            request.Options.Set(SkipRefresh, true);
            request.Headers.Add("Cookie", cookie);

            using var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // 401·403 만 「세션이 죽었다」로 읽는다. 404 는 갱신 경로가
                // 없다는 뜻일 뿐이고(지금 AuthServer 가 그렇다), 그것으로
                // 들고 있는 토큰까지 버릴 이유가 없다.
                _sessionRejected =
                    response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

                logger.Log(
                    _sessionRejected ? LogLevel.Information : LogLevel.Debug,
                    "토큰 갱신 실패 ({Status}). 세션 거절 여부: {Rejected}",
                    (int)response.StatusCode, _sessionRejected);

                return false;
            }

            var token = await ReadTokenAsync(response, cancellationToken);
            if (token is not { Length: > 0 })
            {
                logger.LogWarning("토큰 갱신 응답에서 access token 을 찾지 못했다.");
                return false;
            }

            tokens.UpdateAccessToken(token);
            return true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "토큰 갱신 중 서버에 연결하지 못했다.");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// 갱신 응답에서 토큰을 꺼낸다.
    ///
    /// <c>/auth/refresh</c> 는 봉투의 <c>data</c> 에 토큰 문자열을 그대로 싣는다
    /// (Vue 의 <c>refreshTokenApi</c> 가 <c>resp.data</c> 를 바로 토큰으로 쓴다).
    /// 다만 다른 엔드포인트처럼 <c>data.result[0]</c> 로 감싸 오는 경우도 있어
    /// 둘 다 받아 준다 — 여기서 틀리면 로그인이 조용히 풀린다.
    /// </summary>
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

    /// <summary>
    /// 재시도용으로 요청을 복제한다.
    ///
    /// <see cref="HttpRequestMessage"/> 는 한 번 보내면 다시 보낼 수 없고, 본문
    /// 스트림은 이미 다 읽혔다. 그래서 본문을 바이트로 떠서 새 요청을 만든다.
    /// </summary>
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
