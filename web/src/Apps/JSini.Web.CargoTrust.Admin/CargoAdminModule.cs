using JSini.Web.Abstractions;
using JSini.Web.CargoTrust.Admin.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.CargoTrust.Admin;

/// <summary>
/// 운송관리 관리자 모듈이 셸에 자기를 알리는 자리. 접두사는 /cargoadmin 으로 따로 둔다 — 사용자 모듈 밑에 두면 포괄 라우트(_Pending)가 관리자 화면의 오타 주소까지 삼킨다.
/// </summary>
public sealed class CargoAdminModule : IPortalModule
{
    public string Key => "cargoadmin";

    public string DisplayName => "운송관리 관리자";

    public string RoutePrefix => "/cargoadmin";

    /// <summary>대시보드 바로가기 · 감사 기록의 전·후 비교 · 통계 머리글.</summary>
    public string? StyleSheet => "_content/JSini.Web.CargoTrust.Admin/cargoadmin.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // AddHttpClient 가 아니라 AddScoped — 게이트웨이 통로는 셸이 이미 등록해 두었다. 이름 붙은 HttpClient 를 더 만들면 타입 이름 유일성을 떠안는다(HttpClientNamingTests).
        services.AddScoped<CargoAdminClient>();
    }
}
