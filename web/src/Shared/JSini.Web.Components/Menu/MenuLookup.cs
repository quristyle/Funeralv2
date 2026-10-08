using JSini.Web.Abstractions;

namespace JSini.Web.Components.Menu;

/// <summary>
/// 메뉴 트리에서 화면 하나를 찾는다. <b>헤더의 단추들이 쓴다.</b>
/// </summary>
/// <remarks>
/// <para>
/// [왜 트리를 뒤지는가 — 「이 화면을 쓸 수 있는 사람에게만」]
/// </para>
///
/// <para>
/// 헤더에는 어느 화면으로 가거나 그 화면의 알맹이를 여는 단추가 몇 개 있다
/// (⚡ 빠른 지시 · 노란 번개 요청 · 🤖 AI쳇). 그것들은 <b>그 화면을 쓸 수
/// 있는 사람에게만</b> 서야 한다 — 못 쓰는 사람이 눌러 봐야 「준비 중」이나
/// 빈 판을 볼 뿐이고, 그건 단추가 고장 난 것으로 읽힌다.
/// </para>
///
/// <para>
/// <b>어느 목록을 주느냐는 부르는 쪽이 정한다.</b> 이 메서드는 <b>찾기만</b>
/// 하고 권한을 따지지 않는다 — 가르는 선이 단추마다 다르기 때문이다.
/// </para>
///
/// <list type="bullet">
///   <item>
///     메뉴로 <b>가는</b> 단추(⚡ 둘)는 <see cref="IMenuProvider.VisibleMenus"/> 를
///     준다. 사이드바에서 빠진 메뉴로 가는 길만 헤더에 남아 있으면 안 된다.
///   </item>
///   <item>
///     같은 알맹이를 <b>옆에서 여는</b> 단추(🤖)는 <c>AllMenus</c> 에서 찾고
///     권한을 따로 묻는다(<c>AiChatDrawer</c> 머리말). 사이드바에서 감췄다고
///     헤더에서까지 없어지면 안 된다.
///   </item>
/// </list>
///
/// <para>
/// 어느 쪽이든 그 목록은 권한표가 실린 뒤에 채워지므로, 못 찾는 동안은 단추가
/// 아직 안 선 것이다 — <b>잠깐 떴다 사라지는 것보다 늦게 나타나는 편이 낫다.</b>
/// 늦게 실리는 것은 <see cref="IMenuProvider.MenusChanged"/> 를 들어 받는다.
/// </para>
///
/// <para>
/// [주소가 아니라 <see cref="MenuNode.RouteKey"/> 로 찾는다]
/// </para>
///
/// <para>
/// DB 의 <c>path</c> 에는 아직 Vue 시절 경로가 남아 있고(<c>/ai/chat</c>),
/// 언젠가 <c>menu-path-cutover.sql</c> 이 돌면 그 값이 바뀐다. 열쇠는 한 번
/// 정하면 안 바꾸는 값이라 그쪽으로 찾는다(<c>RouteKeyAttribute</c> 머리말).
/// 함께 받는 <c>paths</c> 는 <b>열쇠를 아직 안 채운 DB 를 위한 대비책</b>이고,
/// 이행 중에는 옛 경로와 새 경로가 둘 다 쓰일 수 있어 여럿을 받는다.
/// </para>
///
/// <para>
/// [찾은 뒤에는 그 마디가 주소와 이름을 준다]
/// </para>
///
/// <para>
/// 단추에 주소를 박아 두지 않는다 — <see cref="MenuNode.LinkTarget"/> 을 쓰면
/// 화면을 옮겨도 열쇠만 따라가고 단추는 그대로다. 얹는 글도 <c>Title</c> 에서
/// 가져오면 메뉴에서 이름을 고쳤을 때 헤더만 옛 이름으로 남지 않는다.
/// </para>
///
/// <para>
/// <b>단추를 감추는 것이 통제는 아니다.</b> 그것은 정리고, 실제로 막는 것은
/// 서버다(<see cref="MenuFilter"/> 머리말과 같은 선이다).
/// </para>
/// </remarks>
public static class MenuLookup
{
    /// <summary>
    /// <paramref name="routeKey"/> 로, 없으면 <paramref name="paths"/> 중
    /// 하나로 맞는 마디를 깊이 우선으로 찾는다. 없으면 <c>null</c>.
    /// </summary>
    public static MenuNode? Find(
        IReadOnlyList<MenuNode> nodes, string routeKey, params string[] paths)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.RouteKey, routeKey, StringComparison.OrdinalIgnoreCase)
                || paths.Any(p => string.Equals(node.Path, p, StringComparison.OrdinalIgnoreCase)))
            {
                return node;
            }

            if (Find(node.Children, routeKey, paths) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }
}
