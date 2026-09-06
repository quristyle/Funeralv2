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

        // 프로필 사진 업로드. 공지 첨부와 경로가 다르다(파일 **그룹**) —
        // 왜 그룹이어야 하는지는 ProfileImageClient 머리말에 있다.
        services.AddHttpClient<ProfileImageClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);

                // 얼굴 사진 몇 장이라 크지 않다. 그래도 휴대폰 원본은 한 장이
                // 10MB 를 넘는 일이 흔해 기본 100초로는 모자란다.
                client.Timeout = TimeSpan.FromMinutes(5);
            })
            .ConfigurePrimaryHttpMessageHandler(ServiceCollectionExtensions.NoCookieJar)
            .AddHttpMessageHandler<AuthTokenHandler>();
    }
}
