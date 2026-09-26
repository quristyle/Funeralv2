using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

public partial class Breadcrumb
{
    [Inject] private MenuReveal Menus { get; set; } = default!;
    [Inject] private TabMenuRequest TabMenu { get; set; } = default!;

    /// <summary>뿌리부터 지금 화면까지의 메뉴 줄기.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<MenuNode> Trail { get; set; } = [];

    /// <summary>
    /// 휴대폰인가(≤767px). 레이아웃이 내려 준다 — 그쪽이 이미
    /// <c>DxLayoutBreakpoint</c> 로 재고 있다.
    /// </summary>
    /// <remarks>
    /// 맨 끝 칸이 하는 일이 이 값으로 갈린다. CSS 로는 못 가르는 대목이라
    /// (누르면 무엇이 일어나는가) 값으로 받는다. 프리렌더 중에는 화면 크기를
    /// 모르므로 거짓이고, 재고 나면 다시 그려진다 — 그전에 누를 수는 없다.
    /// </remarks>
    [Parameter] public bool IsPhone { get; set; }

    /// <summary>
    /// 사이드바에 「이 메뉴를 펴서 보여 달라」고 한다. 받는 쪽은
    /// <c>SidebarMenu</c>(펴고 고르기)와 <c>MainLayout</c>(접혀 있으면 펴기)이다.
    /// </summary>
    private void Reveal(MenuNode node) => Menus.Request(node.Path);

    /// <summary>
    /// 맨 끝 칸(지금 화면)을 눌렀다. <b>휴대폰에서는 탭 메뉴</b>를 누른 자리에
    /// 열고, 넓은 화면에서는 하던 대로 사이드바를 그 자리로 편다.
    /// </summary>
    private void OnCurrentClick(MouseEventArgs args, MenuNode node)
    {
        if (IsPhone)
        {
            TabMenu.Request(args);
            return;
        }

        Reveal(node);
    }
}
