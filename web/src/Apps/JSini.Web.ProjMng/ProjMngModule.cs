using JSini.Web.Abstractions;
using JSini.Web.ProjMng.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.ProjMng;

/// <summary>
/// 프로젝트관리 모듈이 셸에 자기를 알리는 자리.
///
/// 셸은 이 클래스를 이름으로 알지 못한다 — 어셈블리를 훑어
/// <see cref="IPortalModule"/> 구현을 찾아 등록할 뿐이다. 그래서 모듈을
/// 넣고 빼는 데 셸을 고칠 일이 없다.
/// </summary>
public sealed class ProjMngModule : IPortalModule
{
    public string Key => "projmng";

    public string DisplayName => "프로젝트관리";

    public string RoutePrefix => "/projmng";

    /// <summary>
    /// 이 업무 전용 스타일. 셸이 <c>&lt;head&gt;</c> 에 실어 준다.
    ///
    /// 한동안 이것이 없어서 화면들이 쓰는 <c>pm-*</c> 클래스 몇 개가
    /// 정의되지 않은 채였다. 화면은 뜨고 글자도 보이므로 눈에 잘 안 띈다.
    /// </summary>
    public string? StyleSheet => "_content/JSini.Web.ProjMng/projmng.css";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProjMngClient>();
        services.AddScoped<CommonCodes>();
        services.AddScoped<BizOptions>();

        // 이 앱 전용 서비스만 여기 등록한다.
        // 게이트웨이 클라이언트·권한·알림은 셸이 이미 올려 두었다.
    }
}
