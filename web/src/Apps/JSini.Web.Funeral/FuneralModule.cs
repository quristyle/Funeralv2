using JSini.Web.Abstractions;
using JSini.Web.Funeral.Api;
using JSini.Web.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Funeral;

/// <summary>
/// 장례식장 모듈이 셸에 자기를 알리는 자리.
///
/// 셸은 이 클래스를 이름으로 알지 못한다 — 어셈블리를 훑어
/// <see cref="IPortalModule"/> 구현을 찾아 등록할 뿐이다. 그래서 모듈을
/// 넣고 빼는 데 셸을 고칠 일이 없다.
/// </summary>
public sealed class FuneralModule : IPortalModule
{
    public string Key => "funeral";

    public string DisplayName => "장례식장";

    public string RoutePrefix => "/funeral";

    /// <summary>
    /// 장례식장 화면들이 함께 쓰는 스타일 (빈소현황 카드 · 설정 줄 · 첨부 목록).
    /// 셸이 &lt;head&gt; 에 실어 준다.
    /// </summary>
    public string? StyleSheet => "_content/JSini.Web.Funeral/funeral.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 이 모듈 전용 서비스만 여기 등록한다.
        // 게이트웨이 클라이언트·권한·알림은 셸이 이미 올려 두었다.
        //
        // **scoped 다.** GatewayClient 를 물고 있고 그 안에 사용자 토큰이 있다.
        // 싱글턴으로 두면 먼저 로그인한 사람의 토큰으로 남의 요청이 나간다.
        services.AddScoped<FuneralApi>();
        services.AddScoped<HelpApi>();

        // 공통코드. 호실 구분·사망 종류 같은 고르개가 쓴다.
        // 회로 하나에 하나여야 캐시가 산다 — 그래서 scoped 다.
        services.AddScoped<CommonCodeClient>();

        // 업로드만 멀티파트라 GatewayClient 를 쓸 수 없다. 같은 주소와 같은
        // 토큰 처리(AuthTokenHandler)로 붙여서 인증이 갈라지지 않게 한다.
        var baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        services.AddHttpClient<FileUploadClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);

                // 영상 파일이 수백 MB 다. 기본 100초로는 큰 파일에서 끊긴다.
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .AddHttpMessageHandler<AuthTokenHandler>();
    }
}
