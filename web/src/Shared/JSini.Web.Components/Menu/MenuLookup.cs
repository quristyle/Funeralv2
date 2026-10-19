using JSini.Web.Abstractions;

namespace JSini.Web.Components.Menu;

/// <summary>메뉴 트리에서 화면 하나를 찾는다. 헤더의 단추들이 쓴다. 그 화면을 쓸 수 있는 사람에게만 단추가 서야 한다 — 못 쓰는 사람이 눌러 봐야 준비 중이나 빈 판이라 단추가 고장 난 것으로 읽힌다. 어느 목록을 주느냐는 부르는 쪽이 정한다 — 메뉴로 가는 단추는 VisibleMenus, 같은 알맹이를 옆에서 여는 단추는 AllMenus 에서 찾고 권한을 따로 묻는다(사이드바에서 감춘 것이 헤더에서까지 없어지면 안 된다). 못 찾는 동안은 단추가 안 선다 — 잠깐 떴다 사라지는 것보다 늦게 나타나는 편이 낫다.
/// 주소가 아니라 RouteKey 로 찾는다 — DB 의 path 에는 옛 경로가 남아 있어 열쇠가 안 바뀌는 쪽으로 찾는다. paths 는 열쇠를 아직 안 채운 DB 를 위한 대비책이다. 단추에 주소를 박지 않고 LinkTarget 을 쓰면 화면을 옮겨도 열쇠만 따라간다. 단추를 감추는 것은 통제가 아니고 실제로 막는 것은 서버다.</summary>
public static class MenuLookup
{
    /// <summary>routeKey 로, 없으면 paths 중 하나로 맞는 마디를 깊이 우선으로 찾는다. 없으면 null.</summary>
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
