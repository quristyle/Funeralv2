using JSini.Web.Abstractions;

namespace JSini.Web.Components.Menu;

/// <summary>
/// 메뉴 트리를 사이드바에 넣기 전에 거른다.
/// Vue 의 <c>router/menu-visibility.ts</c> 를 옮긴 것이다.
///
/// [순수 함수로 둔 이유]
///
/// 이 판정에는 지금까지 잔버그가 많았다 — 자식이 모두 걸러진 묶음이 남아
/// 눌러도 아무것도 없거나, 반대로 권한 있는 화면이 부모와 함께 사라지거나.
/// 상태를 들고 있지 않게 두면 그 경우들을 표로 만들어 시험할 수 있다.
/// <c>JSini.Web.Architecture.Tests</c> 가 그렇게 한다.
///
/// [거르는 것은 목록뿐이다]
///
/// 목록에서 빠진 화면도 라우트는 살아 있다. 실제로 막는 것은 라우트 가드와
/// 서버다. 목록에서 지우는 것은 통제가 아니라 정리다 — 이 둘을 섞으면
/// "목록에 없으니 안전하다" 는 착각이 생긴다.
/// </summary>
public static class MenuFilter
{
    /// <summary>
    /// 권한과 화면 크기로 거른 트리를 돌려준다.
    /// </summary>
    /// <param name="menus">거르기 전 원본 (<c>/auth/menu/all</c> 응답)</param>
    /// <param name="viewport">지금 화면 크기</param>
    /// <param name="canView">
    /// 이 경로를 열람할 수 있는가. <see cref="IPermissionContext.CanView"/> 를 넘긴다.
    ///
    /// 스토어를 직접 들여오지 않고 함수로 받는 이유는, 판정이 한 곳에 남으면서
    /// 이 클래스는 권한 없이도 시험할 수 있게 하려는 것이다. Vue 에서도 같은
    /// 이유로 <c>canViewMenu</c> 를 인자로 받았다.
    /// </param>
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

            // 남은 자식이 있거나, 자기 자신이 열 수 있는 화면이면 남긴다.
            // 둘 다 아니면 뺀다 — 자식이 모두 걸러진 묶음이 그렇다.
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

    /// <summary>
    /// 이 메뉴가 지금 크기의 목록에 보여야 하는가.
    /// 데스크톱은 크기 규칙과 무관하게 다 보인다.
    /// </summary>
    private static bool FitsViewport(MenuNode menu, Viewport viewport) => viewport switch
    {
        Viewport.Phone => menu.UseMobile,
        Viewport.Tablet => menu.UseTablet,
        _ => true,
    };

    /// <summary>
    /// 이 메뉴가 <b>자기 화면</b>으로서 목록에 남을 자격이 있는가.
    ///
    /// 묶음(CATALOG)은 자기 화면이 없으므로 언제나 아니다 — 남은 자식으로만 판단한다.
    /// 자식이 있다고 다 묶음이 아니라는 점이 중요하다. <c>/funeral/status</c>(현황관리)는
    /// 자식이 다섯인 화면 있는 메뉴고, 이걸 묶음으로 다루면 자식이 모두 걸러졌을 때
    /// 자기 열람 권한이 있는데도 함께 사라진다.
    ///
    /// 외부 링크는 앱 안의 화면이 아니라 권한표에도 없으므로 그대로 남긴다.
    /// </summary>
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
