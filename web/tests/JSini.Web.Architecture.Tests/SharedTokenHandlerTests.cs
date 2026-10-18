using System.Net;
using System.Security.Claims;
using JSini.Web.Components.Security;
using JSini.Web.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <c>HttpClientFactory</c> 는 <see cref="AuthTokenHandler"/> 와 그것이 받은
/// <see cref="TokenStore"/> 를 <b>한 벌만 만들어 모든 사용자에게 돌려 쓴다.</b>
/// 그 상태에서 한 사람이 토큰을 갱신해도 다른 사람의 요청은 <b>자기 토큰</b>으로
/// 나가야 한다.
///
/// <para>
/// 어긋나면 서버를 다시 띄운 직후(모두의 토큰이 한꺼번에 401 을 맞는 자리)
/// 먼저 갱신한 사람의 이름 · 메뉴 · 권한으로 남의 화면이 뜬다.
/// </para>
/// </summary>
public sealed class SharedTokenHandlerTests
{
    private static readonly Uri Gateway = new("http://gateway.test/api/");

    [Fact]
    public async Task 한_사람이_갱신한_토큰이_다른_사람의_요청에_실리지_않는다()
    {
        var accessor = new HttpContextAccessor();
        var refreshed = new RefreshedTokens();

        // 팩토리가 만드는 모양 그대로 — 핸들러 하나, 통 하나.
        var shared = new TokenStore(accessor, refreshed);
        var inner = new GatewayStub();
        using var invoker = new HttpMessageInvoker(
            new AuthTokenHandler(shared, NullLogger<AuthTokenHandler>.Instance, Gateway)
            {
                InnerHandler = inner,
            });

        // jupark 의 옛 토큰은 401 → 갱신 → 새 토큰.
        accessor.HttpContext = Context("jupark", "jupark-stale", "jsini_rt=jupark");
        using (var first = await invoker.SendAsync(Request(), CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        // 곧이어 quristyle 의 요청이 같은 핸들러로 나간다.
        accessor.HttpContext = Context("quristyle", "quristyle-valid", "jsini_rt=quristyle");
        using (var second = await invoker.SendAsync(Request(), CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        Assert.Equal("quristyle-valid", inner.LastToken);

        // 갱신한 사람은 새로고침해도(새 HttpContext · 같은 쿠키) 갱신한 토큰을 잇는다.
        accessor.HttpContext = Context("jupark", "jupark-stale", "jsini_rt=jupark");
        using (var third = await invoker.SendAsync(Request(), CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        }

        Assert.Equal("jupark-fresh", inner.LastToken);
    }

    [Fact]
    public async Task 한_사람의_세션_거절이_다른_사람의_토큰을_버리지_않는다()
    {
        var accessor = new HttpContextAccessor();
        var shared = new TokenStore(accessor, new RefreshedTokens());
        var inner = new GatewayStub();
        using var invoker = new HttpMessageInvoker(
            new AuthTokenHandler(shared, NullLogger<AuthTokenHandler>.Instance, Gateway)
            {
                InnerHandler = inner,
            });

        accessor.HttpContext = Context("gone", "gone-stale", "jsini_rt=dead");
        using (var first = await invoker.SendAsync(Request(), CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        }

        accessor.HttpContext = Context("quristyle", "quristyle-valid", "jsini_rt=quristyle");
        using (var second = await invoker.SendAsync(Request(), CancellationToken.None))
        {
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        Assert.Equal("quristyle-valid", inner.LastToken);
    }

    private static HttpRequestMessage Request() =>
        new(HttpMethod.Get, "http://gateway.test/api/things");

    private static DefaultHttpContext Context(string name, string token, string refreshCookie) =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, name),
                    new Claim(TokenStore.AccessTokenClaim, token),
                    new Claim(TokenStore.RefreshCookieClaim, refreshCookie),
                ],
                "test")),
        };

    /// <summary>
    /// <c>*-stale</c> 은 401, 갱신은 쿠키 주인의 <c>*-fresh</c>(죽은 쿠키는 401),
    /// 그 밖에는 200.
    /// </summary>
    private sealed class GatewayStub : HttpMessageHandler
    {
        public string? LastToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("auth/refresh", StringComparison.Ordinal))
            {
                var cookie = request.Headers.GetValues("Cookie").Single();
                var owner = cookie["jsini_rt=".Length..];

                return Task.FromResult(owner == "dead"
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            $"{{\"data\":\"{owner}-fresh\"}}",
                            System.Text.Encoding.UTF8,
                            "application/json"),
                    });
            }

            var token = request.Headers.Authorization?.Parameter;
            LastToken = token;

            return Task.FromResult(new HttpResponseMessage(
                token is null || token.EndsWith("-stale", StringComparison.Ordinal)
                    ? HttpStatusCode.Unauthorized
                    : HttpStatusCode.OK));
        }
    }
}
