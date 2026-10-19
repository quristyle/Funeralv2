using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Abstractions;

/// <summary>
/// 업무 모듈 하나가 셸에 자기를 알리는 유일한 통로. 모듈은 셸을 참조하지 않고, 셸은 모듈 타입을 모른다(어셈블리를 훑어 구현을 찾을 뿐이다).
/// 메뉴는 DB(scom.system_menus)가 소유하고 모듈은 라우트(@page)만 소유한다. DB 의 path 는 메뉴 노출만 정하며, 어긋나면 아키텍처 테스트가 빌드 때 잡는다.
/// </summary>
public interface IPortalModule
{
    /// <summary>모듈 식별자. 소문자 한 단어(<c>funeral</c>, <c>helpdesk</c>). 로그·진단 화면이 쓴다.</summary>
    string Key { get; }

    /// <summary>사람이 읽는 이름(진단 화면용). 사이드바 이름은 DB 의 title 이다.</summary>
    string DisplayName { get; }

    /// <summary>이 모듈이 소유한 라우트 접두사(<c>/funeral</c>). 모든 @page 는 이 아래에 있어야 한다. 그래야 URL 만 보고 소관을 알고, 모듈을 독립 호스트로 뺄 때 게이트웨이가 경로로 라우팅한다.</summary>
    string RoutePrefix { get; }

    /// <summary>라우트를 담은 어셈블리. 셸의 Router 가 AdditionalAssemblies 로 흡수한다. 기본 구현은 자기 어셈블리를 돌려준다.</summary>
    Assembly Assembly => GetType().Assembly;

    /// <summary>
    /// 이 모듈만 쓰는 스타일시트 주소(없으면 null). 셸의 head 는 한 벌뿐이고 모듈은 여럿이라 모듈이 스스로 알린다.
    /// 화면마다 .razor.css 를 쓰지 않는다. 같은 카드·안내 모양을 서른 번 복사하면 하나만 여백이 달라진다. 값은 RCL 정적자원 관례(_content/JSini.Web.Funeral/funeral.css)를 따른다.
    /// </summary>
    string? StyleSheet => null;

    /// <summary>이 모듈이 쓰는 서비스를 등록한다. 셸이 기동 때 한 번 부른다. 이 모듈 것만 등록하고, 공통 서비스(HTTP·권한·알림)는 셸이 이미 등록했다.</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
