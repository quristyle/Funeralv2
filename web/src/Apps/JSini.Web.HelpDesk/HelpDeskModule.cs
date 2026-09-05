using JSini.Web.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.HelpDesk;

/// <summary>
/// 헬프데스크 모듈이 셸에 자기를 알리는 자리.
///
/// 셸은 이 클래스를 이름으로 알지 못한다 — 어셈블리를 훑어
/// <see cref="IPortalModule"/> 구현을 찾아 등록할 뿐이다. 그래서 모듈을
/// 넣고 빼는 데 셸을 고칠 일이 없다.
/// </summary>
public sealed class HelpDeskModule : IPortalModule
{
    public string Key => "helpdesk";

    public string DisplayName => "헬프데스크";

    public string RoutePrefix => "/helpdesk";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 이 모듈 전용 서비스만 여기 등록한다.
        // 게이트웨이 클라이언트·권한·알림은 셸이 이미 올려 두었다.
    }
}
