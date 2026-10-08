using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Components.Layout;

public partial class AiChatDrawer
{
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IPermissionContext Permissions { get; set; } = default!;

    /// <summary>
    /// 「AI쳇」 화면의 열쇠. <b>주소가 아니라 이것으로 찾는다</b> —
    /// 까닭은 <see cref="MenuLookup"/> 머리말에.
    /// </summary>
    private const string ChatRouteKey = "site.ai.chat";

    /// <summary>
    /// <c>route_key</c> 를 아직 안 채운 DB 를 위한 대비책. <b>둘을 받는다</b> —
    /// 운영 DB 에는 아직 Vue 시절 경로가 들어 있고(<c>/ai/chat</c>),
    /// <c>menu-path-cutover.sql</c> 을 돌리면 <c>/site/ai/chat</c> 이 된다.
    /// </summary>
    private static readonly string[] ChatPaths = ["/ai/chat", "/site/ai/chat"];

    /// <summary>
    /// 이 서랍이 여는 화면의 메뉴. <b>그 화면의 열람 권한이 있을 때만</b> 값이
    /// 있고, 없으면 단추도 판도 안 그린다 — 쓸 수 없는 사람에게 빈 판을 열어
    /// 보여 주지 않는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [<b>걸러진 목록이 아니라 원본에서 찾고, 권한은 따로 묻는다</b>]
    /// </para>
    ///
    /// <para>
    /// 옆에 선 ⚡ 둘은 <see cref="IMenuProvider.VisibleMenus"/> 에서 찾는다.
    /// 그쪽은 <b>사이드바에 보이는 메뉴로 가는 단추</b>라 목록에서 빠지면 함께
    /// 사라지는 것이 맞다. 이 단추는 다르다 — 가는 것이 아니라 <b>같은 알맹이를
    /// 옆에서 여는</b> 것이라, 「헤더에 있으니 메뉴에서는 감추자」(<c>hide_in_menu</c>)가
    /// 곧 「헤더에서도 없앤다」가 되면 안 된다. 화면 크기 규칙(<c>use_mobile</c>)도
    /// 사이드바 이야기지 쓸 수 있느냐가 아니다.
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>묻는 것은 권한 하나</b>다. 판정은 언제나
    /// <see cref="IPermissionContext"/> 한 곳에서 한다 — 여기서 역할을 직접
    /// 따지면 「메뉴 관리에서는 줬는데 단추는 안 뜬다」가 되돌아온다.
    /// </para>
    ///
    /// <para>
    /// [권한표를 받기 전에는 안 뜬다]
    /// </para>
    ///
    /// <para>
    /// <see cref="IPermissionContext.Can"/> 은 표를 받기 전이면 <b>참</b>을 준다
    /// (못 받은 것을 「권한 없음」으로 다루지 않는다). 그런데 메뉴 원본도 그때는
    /// 비어 있어 찾을 마디가 없다 — <b>부트스트랩이 권한표를 메뉴보다 먼저
    /// 싣기 때문에</b>(<c>PortalBootstrap</c>) 마디가 생기는 순간에는 판정이
    /// 이미 제값이다. 그 사이에 단추가 떴다 사라지는 일이 없다.
    /// </para>
    /// </remarks>
    private MenuNode? ChatMenu =>
        MenuLookup.Find(Menus.AllMenus, ChatRouteKey, ChatPaths) is { } node
        && Permissions.CanView(node.Path)
            ? node
            : null;

    private bool _open;

    private void Toggle() => _open = !_open;

    /// <summary>
    /// 메뉴와 권한은 로그인 직후·권한 갱신 때 <b>뒤늦게</b> 실린다. 안 듣고
    /// 있으면 처음 그린 뒤로 단추가 영영 안 나타난다(<c>HeaderTools</c> 와 같은
    /// 자리). 권한표에는 알리는 자리가 없고 메뉴가 그때 다시 걸러지므로
    /// (<c>MenuProvider.Filtered</c> 가 <c>CanView</c> 를 쓴다) 이 소식을 듣는다.
    /// </summary>
    protected override void OnInitialized() => Menus.MenusChanged += OnMenusChanged;

    private void OnMenusChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Menus.MenusChanged -= OnMenusChanged;
}
