using JSini.Web.Abstractions;
using JSini.Web.Admin.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Admin;

/// <summary>
/// 포털관리 모듈이 셸에 자기를 알리는 자리.
/// </summary>
public sealed class AdminModule : IPortalModule
{
    public string Key => "admin";

    public string DisplayName => "포털관리";

    public string RoutePrefix => "/admin";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AdminClient>();
    }
}
