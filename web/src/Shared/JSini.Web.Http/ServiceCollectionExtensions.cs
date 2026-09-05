using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이 클라이언트를 DI 에 올린다. 셸의 <c>Program.cs</c> 가 한 번 부른다.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="GatewayClient"/> 와 토큰 처리를 등록한다.
    ///
    /// [클라이언트를 하나만 두는 이유]
    ///
    /// Vue 에는 서비스마다 클라이언트가 있었지만(<c>requestClient</c> ·
    /// <c>baseRequestClient</c> · 헬프데스크 전용 · 프로젝트관리 전용), 실제로 다른
    /// 것은 <b>경로 접두사뿐</b>이었다. 게이트웨이가 <c>/api/{서비스}/…</c> 로 갈라
    /// 주므로 클라이언트를 나눌 이유가 없다. 모듈이 자기 경로만 부르는지는
    /// 아키텍처 테스트가 본다.
    ///
    /// [수명]
    ///
    /// <see cref="ITokenStore"/> 는 <b>scoped</b> 여야 한다 — Blazor Server 에서
    /// scoped 는 회로(사용자) 하나에 대응한다. 싱글턴으로 두면 모든 사용자가 토큰
    /// 하나를 공유해서, 먼저 로그인한 사람의 권한으로 남의 요청이 나간다.
    /// </summary>
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

        // 끝의 / 가 없으면 HttpClient 가 BaseAddress 의 마지막 칸을 상대 경로로
        // 덮어써서 /api 가 통째로 사라진다. 조용히 404 가 나는 종류의 실수라 막아 둔다.
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        services.AddScoped<AuthTokenHandler>();

        services.AddHttpClient<GatewayClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);

                // 파일 내려받기·엑셀 만들기가 오래 걸리는 화면이 있다.
                // 기본 100초는 짧아서 큰 목록에서 끊긴다.
                client.Timeout = TimeSpan.FromMinutes(3);
            })
            .AddHttpMessageHandler<AuthTokenHandler>();

        return services;
    }
}
