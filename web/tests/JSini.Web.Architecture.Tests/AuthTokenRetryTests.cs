using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using JSini.Web.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 토큰이 만료돼 401 을 받으면 <b>갱신한 토큰으로</b> 다시 보내는지 본다.
///
/// <para>
/// [왜 테스트로 막는가 — 「갱신은 되는데 그 요청만 실패한다」]
/// </para>
///
/// <para>
/// <see cref="AuthTokenHandler"/> 는 재시도용으로 요청을 복제하는데, 복제는
/// 헤더를 그대로 베끼므로 <b>방금 만료된 토큰이 그대로 실려 있다.</b> 그리고
/// 토큰을 붙이는 쪽은 이미 붙어 있는 <c>Authorization</c> 을 덮지 않는다
/// (부르는 쪽이 직접 실어 준 토큰을 지키려는 규칙이다). 둘이 맞물리면
/// <b>갱신해 놓고 옛 토큰으로 다시 물어보는 꼴</b>이 되어 또 401 을 받는다.
/// </para>
///
/// <para>
/// 갱신 자체는 성공하므로 <b>그 뒤의 요청</b>은 멀쩡히 나간다. 실패하는 것은
/// 만료를 처음 밟은 그 요청들뿐이라, 증상이 <b>「휴대폰을 한참 두었다 돌아오면
/// 자료를 못 읽고 로그인이 필요하다고 하는데 새로고침하면 된다」</b> 로 나온다 —
/// 회로가 다시 설 때는 호출 대여섯이 한꺼번에 나가서 그 묶음이 통째로 빈손이 된다.
/// </para>
/// </summary>
public sealed class AuthTokenRetryTests
{
    private const string Stale = "stale-token";
    private const string Fresh = "fresh-token";
    private static readonly Uri Gateway = new("http://gateway.test/api/");

    [Fact]
    public async Task 만료된_토큰으로_401_을_받으면_갱신한_토큰으로_다시_보낸다()
    {
        var inner = new RecordingHandler();
        using var invoker = Invoker(inner, new FakeTokenStore(Stale));

        using var response = await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sent = inner.Tokens("http://gateway.test/api/things");
        Assert.Equal(2, sent.Count);
        Assert.Equal(Stale, sent[0]);

        // 여기가 핵심이다. 옛 토큰이면 사용자는 「로그인이 필요합니다」를 본다.
        Assert.Equal(Fresh, sent[1]);
    }

    [Fact]
    public async Task 한꺼번에_401_을_받아도_갱신은_한_번만_나간다()
    {
        var inner = new RecordingHandler();
        using var invoker = Invoker(inner, new FakeTokenStore(Stale));

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => invoker.SendAsync(Request(), CancellationToken.None)));

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
        }

        // 네 번 나가면 앞선 토큰들이 줄줄이 버려진다 — 갱신을 돌려 쓰는 쪽에서는
        // 그것이 곧 로그아웃이다.
        Assert.Equal(1, inner.Count("http://gateway.test/api/auth/refresh"));
    }

    [Fact]
    public async Task 부르는_쪽이_실어_준_토큰은_덮지도_갱신하지도_않는다()
    {
        var inner = new RecordingHandler();
        using var invoker = Invoker(inner, new FakeTokenStore(Stale));

        using var request = Request();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "one-off-token");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        // 그 자리(앱알림 아이콘 중계)는 지금 사용자의 신원이 아니라서, 우리 토큰을
        // 갱신해 봐야 바뀌는 것이 없다.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, inner.Count("http://gateway.test/api/auth/refresh"));
        Assert.Equal(["one-off-token"], inner.Tokens("http://gateway.test/api/things"));
    }

    private static HttpMessageInvoker Invoker(RecordingHandler inner, ITokenStore tokens)
    {
        var handler = new AuthTokenHandler(tokens, NullLogger<AuthTokenHandler>.Instance, Gateway)
        {
            InnerHandler = inner,
        };

        return new HttpMessageInvoker(handler);
    }

    private static HttpRequestMessage Request() =>
        new(HttpMethod.Get, "http://gateway.test/api/things");

    /// <summary>
    /// 만료된 토큰에는 401, 갱신 요청에는 새 토큰, 그 밖에는 200.
    /// 오간 요청을 전부 적어 둔다.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Lock _gate = new();
        private readonly List<(string Url, string? Token)> _seen = [];

        public List<string?> Tokens(string url)
        {
            lock (_gate)
            {
                return [.. _seen.Where(s => s.Url == url).Select(s => s.Token)];
            }
        }

        public int Count(string url)
        {
            lock (_gate)
            {
                return _seen.Count(s => s.Url == url);
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var token = request.Headers.Authorization?.Parameter;

            lock (_gate)
            {
                _seen.Add((url, token));
            }

            if (url.EndsWith("auth/refresh", StringComparison.Ordinal))
            {
                // 겹쳐 들어오는지 보려면 갱신이 한동안 붙들려 있어야 한다.
                await Task.Delay(30, cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"data\":\"{Fresh}\"}}", System.Text.Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(
                token == Fresh ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        }
    }

    private sealed class FakeTokenStore(string accessToken) : ITokenStore
    {
        private string? _token = accessToken;

        public void Initialize(ClaimsPrincipal user) { }

        public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Volatile.Read(ref _token));

        public ValueTask<string?> GetRefreshCookieAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<string?>("jsini_rt=cookie-value");

        public void UpdateAccessToken(string accessToken) => Volatile.Write(ref _token, accessToken);

        public void Clear() => Volatile.Write(ref _token, null);
    }
}
