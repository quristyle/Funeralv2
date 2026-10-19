using JSini.Web.Abstractions;
using JSini.Web.Funeral.Api;
using JSini.Web.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Funeral;

/// <summary>셸이 어셈블리를 훑어 찾는 장례식장 모듈 진입점. 셸은 이 이름을 모른다.</summary>
public sealed class FuneralModule : IPortalModule
{
    public string Key => "funeral";

    public string DisplayName => "장례식장";

    public string RoutePrefix => "/funeral";

    /// <summary>장례식장 화면 공용 스타일. 셸이 head 에 싣는다.</summary>
    public string? StyleSheet => "_content/JSini.Web.Funeral/funeral.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 이 모듈 전용 서비스만 등록한다. 게이트웨이 클라이언트·권한·알림은 셸이 올린다.
        //
        // scoped: GatewayClient 가 사용자 토큰을 물고 있어 싱글턴이면 남의 요청에 토큰이 섞인다.
        services.AddScoped<FuneralApi>();
        services.AddScoped<HelpApi>();

        // 공통코드 캐시는 회로(scoped) 하나에 하나여야 살아남는다.
        services.AddScoped<CommonCodeClient>();

        // 장비 실시간 상태 중계는 싱글턴. 화면마다 허브 연결을 열면 관리자 수만큼 생긴다.
        // 허브는 인증이 없어 신원이 섞이지 않는다.
        services.AddSingleton<DeviceStatusRelay>();

        // 업로드는 멀티파트라 GatewayClient 를 못 쓴다. 같은 주소·AuthTokenHandler 로 붙여 인증을 맞춘다.
        var baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        services.AddHttpClient<FileUploadClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);

                // 영상이 수백 MB 라 기본 100초로는 끊긴다.
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .ConfigurePrimaryHttpMessageHandler(ServiceCollectionExtensions.NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();
    }
}
