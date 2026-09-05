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

    /// <summary>생일 화면들이 함께 쓰는 스타일 (캘린더 격자 · 축하 보내기 줄).</summary>
    public string? StyleSheet => "_content/JSini.Web.LifeEnv/lifeenv.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<LifeEnvClient>();

        // 생일은 자료가 포털(AuthServer)에 있고 화면만 여기 있다. 자세한 내용은
        // BirthdayClient 주석을 보라.
        services.AddScoped<BirthdayClient>();
    }
}
