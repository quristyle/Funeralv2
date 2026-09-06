using JSini.Web.Abstractions;

namespace JSini.Web.Components.Menu;

/// <summary>
/// 즐겨찾기를 <b>트리로</b> 보여 주기 위해, 보이는 메뉴 트리에서 담아 둔 화면과
/// 그 위 묶음만 남긴다.
///
/// [평평한 목록이 아니라 트리인 이유]
///
/// 메뉴 제목은 묶음 안에서만 유일하다. 「현황」·「목록」·「설정」 같은 이름이
/// 업무마다 따로 있어서, 평평하게 늘어놓으면 담아 둔 「목록」이 장례식장 것인지
/// 헬프데스크 것인지 알 수 없다. 위 묶음이 함께 보이면 그 자리에서 구분된다.
///
/// [<see cref="MenuFilter"/> 와 짝이다]
///
/// 거기서 권한·화면 크기로 이미 거른 트리를 받아 한 번 더 좁힌다. 그래서
/// <b>권한이 없어진 화면은 즐겨찾기에서도 저절로 빠진다</b> — 여기서 권한을
/// 다시 따지지 않는다. 판정이 두 곳으로 갈라지면 어긋나고, 그 어긋남은
/// "어떤 사용자에게만" 나타나서 재현이 어렵다.
///
/// [경로로 묻는다]
///
/// 즐겨찾기는 메뉴 <c>path</c> 로 담긴다(<see cref="MenuFavorites"/> 참고).
/// 그래서 판정은 <see cref="MenuNode.Path"/> 로 해야 한다 — <c>RouteAliases</c> 로
/// 옮긴 링크 주소(<see cref="MenuNode.Href"/>)로 물으면 이미 담아 둔 것이
/// 안 담긴 것으로 보인다.
///
/// <see cref="MenuFilter"/> 와 같은 이유로 순수 함수다. 판정을 함수로 받으므로
/// 즐겨찾기 서비스 없이도 시험할 수 있다.
/// </summary>
public static class FavoriteTree
{
    /// <summary>
    /// 담아 둔 화면과 그 조상만 남긴 트리를 돌려준다.
    /// </summary>
    /// <param name="menus">보이는 메뉴 트리 (<see cref="MenuFilter.Filter"/> 를 거친 것)</param>
    /// <param name="isFavorite">
    /// 이 경로가 담겨 있는가. <see cref="MenuFavorites.Contains"/> 를 넘긴다.
    /// </param>
    public static IReadOnlyList<MenuNode> Prune(
        IReadOnlyList<MenuNode> menus,
        Func<string, bool> isFavorite)
    {
        var kept = new List<MenuNode>();

        foreach (var menu in menus)
        {
            var keptChildren = menu.Children.Count > 0
                ? Prune(menu.Children, isFavorite)
                : [];

            // 남은 자식이 있으면 조상으로서 남기고, 자기가 담겨 있으면 자기로서
            // 남긴다. 둘 다 아니면 뺀다.
            if (keptChildren.Count == 0 && !IsFavoriteScreen(menu, isFavorite))
            {
                continue;
            }

            // 원본을 바꾸지 않는다. 즐겨찾기를 담고 뺄 때마다 같은 원본을 다시
            // 좁히므로, 원본을 건드리면 두 번째부터 결과가 달라진다.
            kept.Add(menu.Children.Count > 0
                ? menu with { Children = keptChildren }
                : menu);
        }

        return kept;
    }

    /// <summary>
    /// 좁힌 트리 안에 실제로 담긴 화면이 몇 개인가.
    ///
    /// 사이드바가 이 값을 서버가 준 즐겨찾기 개수와 견준다. 적으면 그만큼이
    /// <b>지금 메뉴에 없는 것</b>이다(권한이 없어졌거나 메뉴에서 지워졌다).
    /// 트리에서 소리 없이 빠지면 사용자는 즐겨찾기가 사라졌다고 읽는다.
    /// </summary>
    public static int CountScreens(
        IReadOnlyList<MenuNode> menus,
        Func<string, bool> isFavorite) =>
        menus.Sum(menu =>
            (IsFavoriteScreen(menu, isFavorite) ? 1 : 0)
            + CountScreens(menu.Children, isFavorite));

    /// <summary>
    /// 이 메뉴가 <b>담긴 화면</b>인가.
    ///
    /// 묶음(CATALOG)은 자기 화면이 없어 담을 수 없다. 그래서 묶음은 언제나
    /// 조상으로만 남는다 — 담긴 것으로 세면 눌러도 아무 데도 가지 않는 항목이
    /// 즐겨찾기 개수에 끼어든다(묶음의 <see cref="MenuNode.NavigateUrl"/> 는
    /// <c>null</c> 이다).
    /// </summary>
    private static bool IsFavoriteScreen(MenuNode menu, Func<string, bool> isFavorite) =>
        !menu.IsCatalog && isFavorite(menu.Path);
}
