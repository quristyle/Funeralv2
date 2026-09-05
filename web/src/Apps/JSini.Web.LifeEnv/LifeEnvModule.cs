using JSini.Web.Abstractions;
using JSini.Web.LifeEnv.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.LifeEnv;

/// <summary>
/// 생활과환경 모듈이 셸에 자기를 알리는 자리.
/// </summary>
public sealed class LifeEnvModule : IPortalModule
{
    public string Key => "life";

    public string DisplayName => "생활과환경";

    public string RoutePrefix => "/life";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<LifeEnvClient>();
    }
}
