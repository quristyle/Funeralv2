using JSini.Web.Abstractions;
using JSini.Web.HelpDesk.Api;
using JSini.Web.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.HelpDesk;

/// <summary>
/// 헬프데스크 모듈이 셸에 자기를 알리는 자리.
///
/// 셸은 이 클래스를 이름으로 알지 못한다 — 어셈블리를 훑어
/// <see cref="IPortalModule"/> 구현을 찾아 등록할 뿐이다. 그래서 모듈을
/// 넣고 빼는 데 셸을 고칠 일이 없다.
/// </summary>
public sealed class HelpDeskModule : IPortalModule
{
    public string Key => "helpdesk";

    public string DisplayName => "헬프데스크";

    public string RoutePrefix => "/helpdesk";

    /// <summary>헬프데스크 화면들이 함께 쓰는 스타일 (요청 상세 · 입력 폼).</summary>
    public string? StyleSheet => "_content/JSini.Web.HelpDesk/helpdesk.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 이 모듈 전용 서비스만 여기 등록한다.
        // 게이트웨이 클라이언트·권한·알림은 공통 등록(AddJSiniWebApp)이 이미 올려 두었다.

        var baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        // 헬프데스크는 응답 봉투가 다른 서비스와 다르다({success,data,meta}).
        // GatewayClient 를 못 쓰고 전용 클라이언트를 둔다 — 토큰 처리는 같은
        // AuthTokenHandler 를 끼워 그대로 물려받는다 (AddJSiniGateway 가 등록해 둠).
        services.AddHttpClient<HelpDeskApi>(client =>
            {
                client.BaseAddress = new Uri(baseUrl + "helpdesk/");
                client.Timeout = TimeSpan.FromMinutes(3);
            })
            .ConfigurePrimaryHttpMessageHandler(ServiceCollectionExtensions.NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();

        // 한주 OADR 외부 시스템. 게이트웨이 /api/oadr 라우트가 중계한다.
        services.AddHttpClient<OadrApi>(client =>
            {
                client.BaseAddress = new Uri(baseUrl + "oadr/");
                client.Timeout = TimeSpan.FromMinutes(3);
            })
            .ConfigurePrimaryHttpMessageHandler(ServiceCollectionExtensions.NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();

        // 회로(사용자) 수명의 캐시들 — Vue 의 Pinia 스토어와 같은 폭이다.
        services.AddScoped<BizOptionService>();
        services.AddScoped<HelpDeskContext>();
    }
}
