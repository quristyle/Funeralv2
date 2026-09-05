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

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<AiChatClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/");
        }).AddHttpMessageHandler<AuthTokenHandler>();
    }
}
