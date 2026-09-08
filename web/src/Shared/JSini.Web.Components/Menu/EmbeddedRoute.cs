namespace JSini.Web.Components.Menu;

/// <summary>
/// 바깥 화면을 끼워 넣는(<c>EMBEDDED</c>) 메뉴가 쓰는 <b>공용 화면 하나</b>의 주소.
///
/// <para>
/// 한동안 그런 메뉴마다 화면 파일이 따로 있었다 — 서버 모니터 · 장례 프레임
/// 모니터 · JIN 보안 셋이 <b>주소 한 줄만 다른 같은 파일</b>이었다. 메뉴를
/// 하나 늘리려면 razor 를 쓰고 배포해야 했는데, 정작 그 주소는 이미
/// <c>scom.system_menus.iframe_src</c> 에 들어 있었다.
/// </para>
///
/// <para>
/// 이제 화면은 하나고(<c>/embed/{*경로}</c>) 메뉴가 자기 주소를 들고 온다.
/// <b>메뉴관리에서 유형을 <c>EMBEDDED</c> 로 두고 iframe 주소만 넣으면
/// 배포 없이 메뉴가 하나 생긴다.</b>
/// </para>
///
/// <para>
/// 주소에 메뉴의 <c>path</c> 를 그대로 잇는 이유는 <b>메뉴마다 주소가 달라야</b>
/// 하기 때문이다 — 탭 · 브레드크럼 · 뒤로 가기가 전부 주소로 서로를 가른다.
/// 메뉴 아이디를 쓰면 짧지만 주소만 보고는 어느 화면인지 알 수 없다.
/// </para>
/// </summary>
public static class EmbeddedRoute
{
    /// <summary>공용 화면의 접두사. <c>Embedded.razor</c> 의 <c>@page</c> 와 같아야 한다.</summary>
    public const string Prefix = "/embed";

    /// <summary>
    /// 이 메뉴들을 받는 화면 파일. <c>scom.system_menus.component</c> 에 적는 값이다.
    ///
    /// <para>
    /// 그 칸은 <b>라우팅에 쓰이지 않는다</b>(Vue 시절의 라우트 생성원이었고 지금은
    /// 메뉴 관리 화면이 「화면 파일」로 보여 주기만 한다). 그래도 값을 맞추는
    /// 이유는 <b>메뉴 관리에서 보이는 것이 사실과 달랐기</b> 때문이다 — 화면을
    /// 공용 하나로 합친 뒤에도 세 메뉴가 저마다 다른 옛 Vue 파일
    /// (<c>#/views/projmng/external/jsini.vue</c>)을 가리키고 있어서, 화면이
    /// 아직 셋인 것처럼 읽혔다.
    /// </para>
    ///
    /// <para>
    /// <b>열쇠(<c>route_key</c>)는 비운다.</b> 이 화면은 포괄 라우트라
    /// <c>RouteKey</c> 를 붙일 수 없고(아키텍처 테스트가 막는다) 붙일 필요도
    /// 없다 — 유형과 iframe 주소만으로 정해진다.
    /// </para>
    /// </summary>
    public const string ScreenFile = "JSini.Web.Shell/Components/Pages/Embedded.razor";

    /// <summary>메뉴 경로(<c>/projmng/external/jsini</c>)를 공용 화면 주소로.</summary>
    public static string HrefFor(string menuPath) =>
        Prefix + "/" + menuPath.TrimStart('/');
}
