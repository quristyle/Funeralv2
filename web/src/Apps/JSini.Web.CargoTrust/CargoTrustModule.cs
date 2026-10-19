using JSini.Web.Abstractions;
using JSini.Web.CargoTrust.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.CargoTrust;

/// <summary>
/// 운송관리 모듈을 셸에 등록한다. 관리자 화면은 별도 모듈(JSini.Web.CargoTrust.Admin)이다.
/// 차주 화면과 관리자 화면은 보여 줄 칸이 달라(사업자번호·등록자) DTO 도 복제해 섞지 않는다.
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
        // 게이트웨이 통로는 셸이 사용자별로 만들어 두므로 HttpClient 를 새로 등록하지 않고 Scoped 로 받는다(이름 유일성 테스트).
        services.AddScoped<CargoTrustClient>();
    }
}
