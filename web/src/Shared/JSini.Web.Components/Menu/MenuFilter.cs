using JSini.Web.Abstractions;

namespace JSini.Web.Components.Menu;

/// <summary>메뉴 트리를 사이드바에 넣기 전에 거른다(Vue 의 menu-visibility.ts 를 옮김). 상태를 들고 있지 않는 순수 함수라 권한 없이 표로 시험한다 — 거르는 것은 목록뿐이고 실제 통제는 라우트 가드와 서버다.</summary>
public static class MenuFilter
{
    /// <summary>권한과 화면 크기로 거른 트리를 돌려준다.</summary>
    /// <param name="menus">거르기 전 원본(/auth/menu/all 응답).</param>
    /// <param name="viewport">지금 화면 크기.</param>
    /// <param name="canView">이 경로를 열람할 수 있는가. 스토어를 직접 들이지 않고 함수로 받아 판정을 한 곳에 두고 권한 없이도 시험하게 한다.</param>
    public static IReadOnlyList<MenuNode> Filter(
        IReadOnlyList<MenuNode> menus,
        Viewport viewport,
        Func<string, bool> canView)
    {
        var kept = new List<MenuNode>();

        foreach (var menu in menus.OrderBy(m => m.OrderNo))
        {
            if (menu.HideInMenu)
            {
                continue;
            }

            if (!FitsViewport(menu, viewport))
            {
                continue;
            }

            var keptChildren = menu.Children.Count > 0
                ? Filter(menu.Children, viewport, canView)
                : [];

            // 남은 자식이 있거나 자기 자신이 열 수 있는 화면이면 남긴다. 둘 다 아니면 뺀다.
            if (keptChildren.Count == 0 && !IsOwnScreen(menu, canView))
            {
                continue;
            }

            kept.Add(menu.Children.Count > 0
                ? menu with { Children = keptChildren }
                : menu);
        }

        return kept;
    }

    /// <summary>이 메뉴가 지금 크기의 목록에 보여야 하는가. 데스크톱은 크기 규칙과 무관하게 다 보인다.</summary>
    private static bool FitsViewport(MenuNode menu, Viewport viewport) => viewport switch
    {
        Viewport.Phone => menu.UseMobile,
        Viewport.Tablet => menu.UseTablet,
        _ => true,
    };

    /// <summary>이 메뉴가 자기 화면으로서 목록에 남을 자격이 있는가. 묶음(CATALOG)은 자기 화면이 없어 남은 자식으로만 판단한다 — 자식이 있다고 다 묶음은 아니다(자식이 모두 걸러졌을 때 자기 열람 권한이 있으면 함께 사라지면 안 된다). 외부 링크는 권한표에 없으므로 그대로 남긴다.</summary>
    private static bool IsOwnScreen(MenuNode menu, Func<string, bool> canView)
    {
        if (menu.IsCatalog)
        {
            return false;
        }

        if (menu.IsExternalLink || string.IsNullOrEmpty(menu.Path))
        {
            return true;
        }

        return canView(menu.Path);
    }
}
