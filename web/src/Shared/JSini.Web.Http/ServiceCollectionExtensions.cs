using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이 클라이언트를 DI 에 올린다. 셸의 <c>Program.cs</c> 가 한 번 부른다.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>토큰을 붙이지 않는 게이트웨이 클라이언트 이름. 로그인 전에도 부르는 공개 경로(공지 등)가 쓴다. 토큰을 붙이면 만료된 토큰 하나 때문에 로그인 화면으로 튕긴다.</summary>
    public const string AnonymousClientName = "JSini.Gateway.Anonymous";

    /// <summary><see cref="GatewayClient"/> 와 토큰 처리를 등록한다. 클라이언트는 하나만 둔다. 서비스마다 다른 것은 경로 접두사뿐이다. ITokenStore 는 사용자(회로)마다 하나인 scoped 여야 한다. 싱글턴이면 먼저 로그인한 사람의 권한으로 남의 요청이 나간다.</summary>
    /// <param name="services">서비스 모음</param>
    /// <param name="configuration">
    /// <c>Gateway:BaseUrl</c> 을 읽는다 (기본 <c>http://localhost:5265/api/</c>).
    /// 운영에서는 컨테이너 이름으로 바꾼다.
    /// </param>
    public static IServiceCollection AddJSiniGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";

        // 끝의 / 가 없으면 BaseAddress 의 마지막 칸이 상대 경로에 덮여 /api 가 사라진다(조용한 404).
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        // 토큰 갱신 요청은 base.SendAsync 로 안쪽 핸들러에 바로 가 BaseAddress 가 닿지 않는다. 절대 주소를 여기서 넘긴다.
        services.AddScoped(sp => new AuthTokenHandler(
            sp.GetRequiredService<ITokenStore>(),
            sp.GetRequiredService<ILogger<AuthTokenHandler>>(),
            new Uri(baseUrl)));

        // 원래 클라이언트의 주소를 게이트웨이로 실어 보내 속도 제한을 사용자별로 센다. 토큰을 붙이는 쪽과 안 붙이는 쪽 둘 다 건다.
        services.AddScoped<ClientIpHandler>();

        services.AddHttpClient<GatewayClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);

                // 파일 내려받기·엑셀 만들기는 오래 걸리므로 기본 100초를 늘린다.
                client.Timeout = TimeSpan.FromMinutes(3);
            })
            .ConfigurePrimaryHttpMessageHandler(NoCookieJar)
            .AddHttpMessageHandler<ClientIpHandler>()
            .AddHttpMessageHandler<AuthTokenHandler>();

        // ── 로그인 전에도 부르는 경로 ─────────────────────────
        // 토큰 핸들러를 붙이지 않는다(AnonymousClientName 참고).
        services.AddHttpClient(AnonymousClientName, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .ConfigurePrimaryHttpMessageHandler(NoCookieJar)
            .AddHttpMessageHandler<ClientIpHandler>();

        // ── AI 대화 (D11) ────────────────────────────────────
        // 스트리밍(text/event-stream)은 GatewayClient 로 못 읽는다. 헤더의 대화창이 공용 레이아웃에 있어 여기서 등록한다. 긴 답이 100초에 끊기지 않도록 시간을 늘린다.
        services.AddHttpClient<AiChatClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .ConfigurePrimaryHttpMessageHandler(NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();

        // ── 로그 번역 (「AI 작업」 상세의 로그 탭) ────────────
        // AI 쳇과 같은 서비스(/api/ai/…)라 여기 둔다. 봉투를 쓰는 평범한 호출이라 GatewayClient 를 그대로 쓴다.
        services.AddScoped<AiLogTranslator>();

        // ── 사진 그룹 (ImageGroup 부품) ───────────────────────
        // 사진 그룹 부품이 공용이라 여기서 등록한다. 휴대폰 사진은 한 장이 10MB 를 넘고 여러 장을 올리므로 시간을 늘린다.
        services.AddHttpClient<FileGroupClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromMinutes(5);
            })
            .ConfigurePrimaryHttpMessageHandler(NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();

        return services;
    }

    /// <summary>
    /// 쿠키 통을 끈다. 게이트웨이로 나가는 모든 클라이언트에 붙인다.
    /// HttpClientFactory 의 기본 핸들러는 사용자와 무관하게 돌아 쓰이므로, 통이 살아 있으면 한 사람의 로그인 쿠키가 남의 요청에 실려 나간다(첨부 중계가 이 경로를 쓰면서 로그인하지 않은 요청이 비공개 첨부를 받았다).
    /// 끄는 것이 잃는 것은 없다. 토큰 갱신은 ITokenStore 가 값을 Cookie 헤더에 직접 싣는다. public 인 것은 모듈이 자기 HttpClient 를 등록할 때 붙여야 하기 때문이다.
    /// </summary>
    public static HttpMessageHandler NoCookieJar() => new HttpClientHandler
    {
        UseCookies = false,
    };
}
