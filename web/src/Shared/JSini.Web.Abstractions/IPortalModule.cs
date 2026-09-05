using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Abstractions;

/// <summary>
/// 업무 모듈 하나가 셸에 자기를 알리는 유일한 통로.
///
/// [이 인터페이스가 MFE 의 실체다]
///
/// 모듈은 셸(JSini.Web.Shell)을 참조하지 않는다. 셸도 모듈 타입을 모른다 —
/// 어셈블리를 훑어 이 인터페이스 구현을 찾아 등록할 뿐이다. 그래서 모듈을
/// 넣고 빼는 데 셸을 고칠 일이 없고, 모듈끼리도 서로를 모른다.
///
/// 이 단방향 의존이 깨지면 이름만 MFE 인 모놀리스가 된다. 그래서 규칙을 문서가
/// 아니라 테스트로 막는다 — <c>JSini.Web.Architecture.Tests</c> 를 보라.
///
/// [메뉴를 여기서 만들지 않는 이유]
///
/// 메뉴는 모듈이 아니라 DB(<c>scom.system_menus</c>)가 소유한다. 운영에서
/// [메뉴 관리] 화면으로 메뉴를 고치고 있고, 그 방식을 그대로 둔다. 모듈이
/// 소유하는 것은 <b>라우트뿐</b>이다.
///
/// 다만 Vue 때와 소유 방향이 뒤집혔다. 예전에는 DB 의 <c>component</c> 컬럼
/// (<c>#/views/portal/dashboard/index.vue</c> 같은 파일 경로)이 라우트를
/// 만들었다. Blazor 는 <c>@page</c> 어트리뷰트로 컴파일 시점에 라우트가 정해지므로
/// DB 가 라우트를 만들 수 없다. 이제 DB 는 <c>path</c> 로 "이 라우트를 메뉴에
/// 어떻게 노출할지"만 정한다. 둘이 어긋나면 메뉴는 보이는데 눌러도 404 가 되므로,
/// 그 대조를 아키텍처 테스트가 빌드 때 한다.
/// </summary>
public interface IPortalModule
{
    /// <summary>
    /// 모듈 식별자. 소문자 한 단어 (<c>funeral</c>, <c>helpdesk</c>).
    /// 로그·진단 화면에서 모듈을 가리키는 이름이다.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// 사람이 읽는 이름 (<c>장례식장</c>). 진단 화면에만 쓴다 —
    /// 사이드바에 보이는 이름은 DB 의 <c>title</c> 이다.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 이 모듈이 소유한 라우트 접두사. 앞에 <c>/</c> 를 붙이고 뒤에는 붙이지 않는다
    /// (<c>/funeral</c>).
    ///
    /// 모듈의 모든 <c>@page</c> 는 이 아래에 있어야 한다. 벗어나면 아키텍처
    /// 테스트가 빌드를 세운다 — 접두사가 지켜져야 "이 URL 은 어느 모듈 소관인가"
    /// 를 URL 만 보고 알 수 있고, 나중에 모듈을 독립 호스트로 뺄 때 게이트웨이가
    /// 경로만으로 라우팅할 수 있다.
    /// </summary>
    string RoutePrefix { get; }

    /// <summary>
    /// 라우트를 담고 있는 어셈블리. 셸의 <c>Router</c> 가
    /// <c>AdditionalAssemblies</c> 로 흡수한다.
    ///
    /// 기본 구현이 자기 어셈블리를 돌려주므로 모듈은 보통 이걸 덮지 않는다.
    /// </summary>
    Assembly Assembly => GetType().Assembly;

    /// <summary>
    /// 이 모듈이 쓰는 서비스를 등록한다. 셸의 <c>Program.cs</c> 가 기동 때 한 번 부른다.
    ///
    /// 여기서 등록하는 것은 <b>이 모듈 것만</b>이다. 공통 서비스(HTTP 클라이언트,
    /// 권한, 알림)는 셸이 이미 등록해 두었으므로 다시 등록하지 않는다.
    /// </summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
