using JSini.Web.Abstractions;
using JSini.Web.Admin.Api;
using JSini.Web.Http;
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

        // 가입 신청 승인. 계정 관리와 갈라 둔 이유는 그 클래스 주석에 있다.
        services.AddScoped<SignupClient>();

        // 공지 첨부 업로드 (D5). GatewayClient 에는 멀티파트가 없어 따로 두지만
        // 같은 BaseAddress·같은 토큰 처리로 등록해 인증이 갈라지지 않게 한다.
        var baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5265/api/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        services.AddHttpClient<NoticeUploadClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);

                // 공지 첨부는 문서·그림이라 장례식장의 영상만큼 크지 않다.
                // 그래도 기본 100초는 짧다 — 20MB 를 느린 회선으로 올리면 넘긴다.
                client.Timeout = TimeSpan.FromMinutes(5);
            })
            .ConfigurePrimaryHttpMessageHandler(ServiceCollectionExtensions.NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();

        // 프로필 사진 업로드는 여기 없다. 브라우저가 `DxUpload` 으로 셸의
        // `/uploads/profile-photo` 로 보내고 셸이 게이트웨이로 넘긴다
        // (`JSini.Web.Components/Data/ProfilePhotoUpload`). 모듈이 들고 있던
        // `ProfileImageClient` 는 그래서 지웠다 — 같은 일을 하는 통로를 둘 두면
        // 언젠가 한쪽에만 규칙이 붙는다.
    }
}
