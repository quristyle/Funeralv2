namespace JSini.Web.Components.Menu;

/// <summary>바깥 화면을 끼워 넣는(EMBEDDED) 메뉴가 쓰는 공용 화면 하나. 한동안은 메뉴마다 주소 한 줄만 다른 화면 파일이 있었다 — 메뉴 하나 늘리려면 razor 를 쓰고 배포해야 했다. 이제 화면은 하나고(/embed/{*경로}) 메뉴가 자기 주소를 iframe_src 로 들고 온다 — 메뉴관리에서 유형을 EMBEDDED 로 두고 주소만 넣으면 배포 없이 메뉴가 생긴다.
/// 주소에 메뉴의 path 를 그대로 잇는 것은 메뉴마다 주소가 달라야 탭 · 브레드크럼 · 뒤로 가기가 주소로 서로를 구별하기 때문이다.</summary>
public static class EmbeddedRoute
{
    /// <summary>공용 화면의 접두사. <c>Embedded.razor</c> 의 <c>@page</c> 와 같아야 한다.</summary>
    public const string Prefix = "/embed";

    /// <summary>이 메뉴들을 받는 화면 파일. component 칸에 적는 값이지만 라우팅에는 쓰이지 않고 메뉴 관리가 화면 파일로 보여 줄 뿐이다. 값을 두는 이유는 메뉴 관리에서 보이는 것이 사실과 달랐기 때문이다 — 공용 하나로 합친 뒤에도 옛 Vue 파일을 가리키고 있어 화면이 아직 셋인 것처럼 읽혔다. 열쇠(route_key)는 비운다 — 포괄 라우트라 RouteKey 를 붙일 수 없고(아키텍처 테스트가 막는다) 유형과 주소만으로 정해진다.</summary>
    public const string ScreenFile = "JSini.Web.Shell/Components/Pages/Embedded.razor";

    /// <summary>메뉴 경로(<c>/projmng/external/jsini</c>)를 공용 화면 주소로.</summary>
    public static string HrefFor(string menuPath) =>
        Prefix + "/" + menuPath.TrimStart('/');
}
