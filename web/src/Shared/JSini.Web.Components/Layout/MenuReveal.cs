using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 「이 메뉴를 사이드바에서 보여 달라」는 부탁. 헤더의 브레드크럼이 하고
/// 사이드바(<c>SidebarMenu</c>)와 레이아웃(<c>MainLayout</c>)이 받는다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 부품끼리 직접 말하지 않나]
/// </para>
///
/// <para>
/// 브레드크럼은 헤더 안이고 트리는 사이드바 안이라, 둘은 <b>형제도 부모
/// 자식도 아니다.</b> 파라미터로 잇자면 레이아웃이 「지금 펼쳐 보여야 할
/// 메뉴」를 상태로 들고 브레드크럼의 콜백을 받아 사이드바로 내려 줘야 하는데,
/// 그러면 레이아웃이 <b>남의 화면 상태를 대신 들고 있는</b> 꼴이 된다.
/// 열어 둔 탭(<c>PortalTabs</c>)·화면 전환 표시(<c>PageTransition</c>)를
/// 회로 하나짜리 통으로 둔 것과 같은 이유로 여기도 통을 하나 둔다.
/// </para>
///
/// <para>
/// [<see cref="MenuNode"/> 가 아니라 경로를 넘긴다]
/// </para>
///
/// <para>
/// 브레드크럼이 든 줄기는 <b>거르기 전 원본</b>에서 나오고
/// (<c>IMenuProvider.Trail</c>), 사이드바가 그리는 것은 <b>걸러진 트리</b>다
/// (<c>VisibleMenus</c>). 거르기는 <c>menu with { Children = … }</c> 로 새
/// 기록을 만들므로 <b>같은 메뉴라도 두 트리의 인스턴스가 다르다</b> —
/// 참조로 맞추면 아무것도 못 찾는다. 그래서 경로로 맞춘다.
/// 그 값은 권한표와 즐겨찾기가 이미 열쇠로 쓰고 있고 메뉴 표에서 유일하다.
/// </para>
///
/// <para>
/// [찾지 못해도 아무 일도 일어나지 않는다]
/// </para>
///
/// <para>
/// 권한 때문에 사이드바에 없는 화면을 주소로 열 수 있다 — 그때 브레드크럼은
/// 보이지만(원본에서 찾으므로) 트리에는 그 가지가 없다. 받는 쪽은 못 찾으면
/// 그냥 둔다. <b>없는 메뉴를 만들어 보여 주지 않는다.</b>
/// </para>
/// </remarks>
public sealed class MenuReveal
{
    /// <summary>보여 달라는 메뉴의 경로(<see cref="MenuNode.Path"/>).</summary>
    public event Action<string>? Requested;

    /// <summary>사이드바에서 이 경로의 메뉴를 펼쳐 고르게 한다.</summary>
    public void Request(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Requested?.Invoke(path);
    }
}
