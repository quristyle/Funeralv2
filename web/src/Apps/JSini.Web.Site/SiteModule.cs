using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Site.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Site;

/// <summary>
/// 소개사이트·AI 모듈이 셸에 자기를 알리는 자리.
/// </summary>
public sealed class SiteModule : IPortalModule
{
    public string Key => "site";

    public string DisplayName => "소개사이트·AI";

    public string RoutePrefix => "/site";

    /// <summary>소개사이트·AI 화면들이 함께 쓰는 스타일.</summary>
    public string? StyleSheet => "_content/JSini.Web.Site/site.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 관리 조회는 게이트웨이 클라이언트로 나간다 (문의내역).
        services.AddScoped<SiteAdminClient>();

        services.AddHttpClient<AiChatClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/");
        }).AddHttpMessageHandler<AuthTokenHandler>();
    }
}
