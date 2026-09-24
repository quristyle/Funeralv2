using JSini.Web.Abstractions;
using JSini.Web.CargoTrust.Admin.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.CargoTrust.Admin;

/// <summary>
/// 운송관리 관리자 모듈이 셸에 자기를 알리는 자리.
/// </summary>
/// <remarks>
/// <para>
/// [사용자 모듈 밑(<c>/cargotrust/admin</c>)이 아니라 <c>/cargoadmin</c> 인 까닭]
/// </para>
///
/// <para>
/// 사용자 모듈의 접두사 밑에 두면 한 접두사를 두 모듈이 쥐게 된다. 포괄 라우트
/// (<c>_Pending</c>)는 모듈마다 하나라서 그때는 사용자 모듈의 <c>/cargotrust/{*rest}</c>
/// 가 관리자 화면의 오타 주소까지 삼키고, 「어느 업무 소관인지」가 흐려진다.
/// 열쇠와 접두사를 통째로 따로 둔다 — 앞머리도 겹치지 않게(<c>MigrationPending</c> 은
/// 주소 앞머리가 가장 길게 맞는 모듈을 소관으로 본다).
/// </para>
/// </remarks>
public sealed class CargoAdminModule : IPortalModule
{
    public string Key => "cargoadmin";

    public string DisplayName => "운송관리 관리자";

    public string RoutePrefix => "/cargoadmin";

    /// <summary>대시보드 바로가기 · 감사 기록의 전·후 비교 · 통계 머리글.</summary>
    public string? StyleSheet => "_content/JSini.Web.CargoTrust.Admin/cargoadmin.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // AddHttpClient 가 아니라 AddScoped 다 — 게이트웨이 통로(GatewayClient)는
        // 셸이 이미 등록해 두었고, 여기서는 그 위에 경로만 얹는다. 이름 붙은
        // HttpClient 를 하나 더 만들면 타입 이름이 저장소 전체에서 유일해야 하는
        // 짐까지 떠안는다(HttpClientNamingTests).
        services.AddScoped<CargoAdminClient>();
    }
}
