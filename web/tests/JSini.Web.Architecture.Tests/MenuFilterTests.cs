using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 메뉴 거르기. Vue 의 <c>menu-visibility.test.ts</c> 가 지키던 경우들을 옮긴다.
///
/// 여기 있는 사례는 대부분 <b>실제로 겪은 사고</b>다 — 사이드바가 통째로 비거나,
/// 눌러도 아무것도 없는 빈 묶음이 남거나. 옮기면서 다시 겪지 않으려고 그대로 들고 온다.
/// </summary>
public sealed class MenuFilterTests
{
    private static MenuNode Catalog(string title, params MenuNode[] children) => new()
    {
        Path = $"/{title}",
        Title = title,
        IsCatalog = true,
        Children = children,
    };

    private static MenuNode Screen(string path, params MenuNode[] children) => new()
    {
        Path = path,
        Title = path,
        IsCatalog = false,
        Children = children,
    };

    private static bool All(string _) => true;

    private static bool None(string _) => false;

    [Fact]
    public void 열람권한이_없는_화면은_목록에서_빠진다()
    {
        var menus = new[] { Screen("/funeral/status"), Screen("/helpdesk/request") };

        var result = MenuFilter.Filter(menus, Viewport.Desktop,
            path => path == "/funeral/status");

        Assert.Single(result);
        Assert.Equal("/funeral/status", result[0].Path);
    }

    /// <summary>
    /// 자식이 모두 걸러진 묶음은 함께 빠진다.
    /// 남겨 두면 눌러도 아무것도 없는 빈 묶음이 사이드바에 남는다.
    /// </summary>
    [Fact]
    public void 자식이_모두_걸러진_묶음은_함께_빠진다()
    {
        var menus = new[] { Catalog("업무", Screen("/a"), Screen("/b")) };

        Assert.Empty(MenuFilter.Filter(menus, Viewport.Desktop, None));
    }

    /// <summary>
    /// <b>가장 중요한 사례.</b> 자식이 있다고 다 묶음이 아니다.
    ///
    /// <c>/funeral/status</c>(현황관리)는 자식이 다섯인 <b>화면 있는 메뉴</b>다.
    /// 이걸 묶음으로 다루면 자식이 모두 걸러졌을 때 자기 열람 권한이 있는데도
    /// 함께 사라지고, 그 위 묶음까지 빈 묶음이 되어 사이드바가 통째로 비어 버린다.
    /// </summary>
    [Fact]
    public void 화면있는_메뉴는_자식이_모두_걸러져도_자기_권한이_있으면_남는다()
    {
        var menus = new[]
        {
            Screen("/funeral/status",
                Screen("/funeral/status/room"),
                Screen("/funeral/status/device")),
        };

        var result = MenuFilter.Filter(menus, Viewport.Desktop,
            path => path == "/funeral/status");

        Assert.Single(result);
        Assert.Equal("/funeral/status", result[0].Path);
        Assert.Empty(result[0].Children);
    }

    /// <summary>
    /// 묶음 자체에는 열람 권한이 없다(화면이 없으니 권한표에도 없다).
    /// 묶음에 권한을 따지면 트리가 통째로 사라진다 — 실제로 겪은 사고다.
    /// </summary>
    [Fact]
    public void 묶음은_자기_권한이_없어도_남은_자식이_있으면_남는다()
    {
        var menus = new[] { Catalog("업무", Screen("/a"), Screen("/b")) };

        var result = MenuFilter.Filter(menus, Viewport.Desktop,
            path => path == "/a");

        Assert.Single(result);
        Assert.Single(result[0].Children);
        Assert.Equal("/a", result[0].Children[0].Path);
    }

    [Fact]
    public void 휴대폰에서는_use_mobile이_꺼진_메뉴가_빠진다()
    {
        var menus = new[]
        {
            Screen("/a") with { UseMobile = false },
            Screen("/b"),
        };

        var phone = MenuFilter.Filter(menus, Viewport.Phone, All);
        Assert.Single(phone);
        Assert.Equal("/b", phone[0].Path);

        // 데스크톱은 크기 규칙과 무관하게 다 보인다.
        Assert.Equal(2, MenuFilter.Filter(menus, Viewport.Desktop, All).Count);
    }

    [Fact]
    public void 태블릿에서는_use_tablet이_꺼진_메뉴가_빠진다()
    {
        var menus = new[] { Screen("/a") with { UseTablet = false }, Screen("/b") };

        var tablet = MenuFilter.Filter(menus, Viewport.Tablet, All);

        Assert.Single(tablet);
        Assert.Equal("/b", tablet[0].Path);
    }

    /// <summary>
    /// 외부 링크는 앱 안의 화면이 아니라 권한표에도 없다.
    /// 권한을 따지면 링크가 전부 사라진다.
    /// </summary>
    [Fact]
    public void 외부링크는_권한을_따지지_않고_남는다()
    {
        var menus = new[]
        {
            new MenuNode { Path = "/help", Title = "도움말", Link = "https://help.jin114.co.kr" },
        };

        Assert.Single(MenuFilter.Filter(menus, Viewport.Desktop, None));
    }

    [Fact]
    public void hide_in_menu가_켜진_메뉴는_목록에_넣지_않는다()
    {
        var menus = new[] { Screen("/a") with { HideInMenu = true }, Screen("/b") };

        var result = MenuFilter.Filter(menus, Viewport.Desktop, All);

        Assert.Single(result);
        Assert.Equal("/b", result[0].Path);
    }

    [Fact]
    public void order_no_순으로_정렬한다()
    {
        var menus = new[]
        {
            Screen("/c") with { OrderNo = 3 },
            Screen("/a") with { OrderNo = 1 },
            Screen("/b") with { OrderNo = 2 },
        };

        var result = MenuFilter.Filter(menus, Viewport.Desktop, All);

        Assert.Equal(["/a", "/b", "/c"], result.Select(m => m.Path));
    }

    /// <summary>
    /// 원본을 바꾸지 않는다. 화면 크기가 바뀌면 같은 원본을 다시 거르므로,
    /// 거르기가 원본을 건드리면 두 번째부터 결과가 달라진다.
    /// </summary>
    [Fact]
    public void 거르기는_원본을_바꾸지_않는다()
    {
        var child = Screen("/a/1");
        var parent = Screen("/a", child);
        var menus = new[] { parent };

        MenuFilter.Filter(menus, Viewport.Desktop, None);

        Assert.Single(parent.Children);
        Assert.Equal("/a/1", parent.Children[0].Path);
    }
}
