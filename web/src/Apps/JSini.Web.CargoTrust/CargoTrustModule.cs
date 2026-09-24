using JSini.Web.Abstractions;
using JSini.Web.CargoTrust.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.CargoTrust;

/// <summary>
/// 운송관리(CargoTrust) 모듈이 셸에 자기를 알리는 자리.
///
/// <para>
/// [관리자 화면은 다른 모듈이다]
/// </para>
///
/// <para>
/// 같은 서버(CargoTrustServer)를 부르지만 관리자 화면은
/// <c>JSini.Web.CargoTrust.Admin</c>(<c>/cargoadmin</c>)에 따로 있다.
/// 차주가 보는 화면과 관리자가 보는 화면은 <b>보여 줘도 되는 것이 다르다</b> —
/// 사업자번호를 가리느냐, 누가 등록했는지를 싣느냐가 갈린다(설계안 29).
/// 한 모듈에 섞으면 차주 화면에서 관리자 DTO 를 한 번 잘못 집는 것만으로
/// 가려야 할 칸이 드러난다. 그래서 DTO 도 서로 복제해 쓴다.
/// </para>
/// </summary>
public sealed class CargoTrustModule : IPortalModule
{
    public string Key => "cargotrust";

    public string DisplayName => "운송관리";

    public string RoutePrefix => "/cargotrust";

    /// <summary>검색 카드 · 통계 타일 · 미수금 카드 (휴대폰 폭에서 무너지지 않게).</summary>
    public string? StyleSheet => "_content/JSini.Web.CargoTrust/cargotrust.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // AddHttpClient 가 아니라 Scoped 다 — 게이트웨이 통로(GatewayClient)는
        // 셸이 이미 사용자별로 만들어 두었고, 여기서 새 HttpClient 를 등록하면
        // 이름이 저장소 전체에서 유일해야 하는 규칙(HttpClientNamingTests)까지 짊어진다.
        services.AddScoped<CargoTrustClient>();
    }
}
