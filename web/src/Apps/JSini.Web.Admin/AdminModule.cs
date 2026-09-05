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

    /// <summary>포털관리 화면들이 함께 쓰는 스타일 (조직도 · 설정 줄 · 두 판 배치).</summary>
    public string? StyleSheet => "_content/JSini.Web.Admin/admin.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AdminClient>();
    }
}
