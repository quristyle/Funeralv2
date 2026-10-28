using JSini.Web.Components.Layout;
using JSini.Web.Models.Menu;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 메뉴의 「화면 유지」(<c>keep_alive</c>)가 탭을 오가도 화면을 살려 두는 길.
/// </summary>
/// <remarks>
/// 이 길도 <see cref="ScreenKeepAliveTests"/> 처럼 <b>끊어져도 화면이 멀쩡하다</b> —
/// 돌아오면 새로 만든 화면이 뜰 뿐이라 「원래 그런가 보다」로 지나간다.
/// 끊어질 자리가 넷이다: 메뉴 값이 셸까지 안 오거나(MenuWireDto), 라우터가 고른 화면을
/// 안 적거나(Routes), 레이아웃이 감추는 대신 버리거나(MainLayout), 탭을 닫아도 안 놓거나.
/// </remarks>
public sealed class KeepAliveScreensTests
{
    private sealed class PageA : ComponentBase;

    private sealed class PageB : ComponentBase;

    [Layout(typeof(BareLayout))]
    private sealed class BarePage : ComponentBase;

    private static RouteData Route<T>(params (string Key, object? Value)[] values) =>
        new(typeof(T), values.ToDictionary(v => v.Key, v => v.Value));

    [Fact]
    public void 화면유지_메뉴는_담고_다시_오면_같은_것을_켠다()
    {
        var screens = new KeepAliveScreens { Route = Route<PageA>() };

        screens.Show("/a", keepAlive: true, typeof(MainLayout));
        var first = Assert.Single(screens.Items);

        screens.Route = Route<PageB>();
        screens.Show("/b", keepAlive: false, typeof(MainLayout));

        Assert.False(screens.ActiveKept);
        Assert.False(first.Active);

        screens.Route = Route<PageA>();
        screens.Show("/A", keepAlive: true, typeof(MainLayout));

        Assert.Same(first, Assert.Single(screens.Items));
        Assert.True(first.Active);
    }

    [Fact]
    public void 화면유지가_아니면_담지_않는다()
    {
        var screens = new KeepAliveScreens { Route = Route<PageA>() };

        screens.Show("/a", keepAlive: false, typeof(MainLayout));

        Assert.Empty(screens.Items);
        Assert.False(screens.ActiveKept);
    }

    /// <summary>레이아웃이 다른 화면은 레이아웃 안에서 그릴 수 없다 — 담으면 셸 안에 맨 화면이 선다.</summary>
    [Fact]
    public void 다른_레이아웃_화면은_담지_않는다()
    {
        var screens = new KeepAliveScreens { Route = Route<BarePage>() };

        screens.Show("/bare", keepAlive: true, typeof(MainLayout));

        Assert.Empty(screens.Items);
    }

    /// <summary>라우트 값은 담을 때 베낀다 — 라우터가 다음 이동에서 같은 사전을 고치면 숨은 화면의 매개변수가 바뀐다.</summary>
    [Fact]
    public void 라우트_값은_담을_때_베낀다()
    {
        var values = new Dictionary<string, object?> { ["id"] = 7 };
        var screens = new KeepAliveScreens { Route = new RouteData(typeof(PageA), values) };

        screens.Show("/a/7", keepAlive: true, typeof(MainLayout));
        values["id"] = 8;

        Assert.Equal(7, Assert.Single(screens.Items).Parameters["id"]);
    }

    [Fact]
    public void 탭을_닫으면_놓고_보이는_것은_남긴다()
    {
        var screens = new KeepAliveScreens { Route = Route<PageA>() };
        screens.Show("/a", keepAlive: true, typeof(MainLayout));
        screens.Route = Route<PageB>();
        screens.Show("/b", keepAlive: true, typeof(MainLayout));

        // /b 는 지금 보이는 화면이다 — 탭이 아직 안 열렸어도 놓지 않는다.
        Assert.True(screens.Retain([]));

        Assert.Equal("/b", Assert.Single(screens.Items).Href);
    }

    [Fact]
    public void 메뉴의_화면유지_값이_메뉴_노드까지_온다()
    {
        var wire = new MenuWireDto { Path = "/x", Meta = new MenuMetaWireDto { Title = "x", KeepAlive = true } };
        var off = new MenuWireDto { Path = "/y", Meta = new MenuMetaWireDto { Title = "y" } };

        Assert.True(wire.ToNode().KeepAlive);
        Assert.False(off.ToNode().KeepAlive);
    }

    /// <summary>레이아웃보다 먼저 적혀야 한다 — 늦으면 첫 그림에 옛 화면 클래스로 담는다.</summary>
    [Fact]
    public void 라우터가_고른_화면을_레이아웃보다_먼저_적는다()
    {
        var routes = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "Components", "Routes.razor"));

        var capture = routes.IndexOf("<KeepAliveRoute RouteData=\"@routeData\"", StringComparison.Ordinal);
        var gate = routes.IndexOf("<ScreenGate", StringComparison.Ordinal);

        Assert.True(capture >= 0, "Routes 가 KeepAliveRoute 를 그리지 않는다 — 화면 유지가 통째로 꺼진다.");
        Assert.True(capture < gate, "KeepAliveRoute 는 ScreenGate 보다 앞이어야 한다.");
    }

    [Fact]
    public void 레이아웃은_살려_둔_화면을_key_로_그리고_감추기만_한다()
    {
        var layout = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor"));

        Assert.Matches(@"@foreach \(var kept in KeepAlive\.Items\)[\s\S]{0,300}?@key=""kept\.Href""[\s\S]{0,300}?<DynamicComponent Type=""@kept\.PageType""", layout);
        Assert.Contains("!KeepAlive.ActiveKept", layout, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));
        var parked = css[css.IndexOf(".jsini-shell__content--parked {", StringComparison.Ordinal)..];
        parked = parked[..parked.IndexOf('}')];

        // display: none 이면 상자가 없어져 구른 자리가 0 으로 돌아간다.
        Assert.DoesNotContain("display", parked, StringComparison.Ordinal);
        Assert.Contains("content-visibility: hidden", parked, StringComparison.Ordinal);
    }

    [Fact]
    public void 화면유지를_끄면_살려둔_목록에서_제거된다()
    {
        var screens = new KeepAliveScreens { Route = Route<PageA>() };
        screens.Show("/a", keepAlive: true, typeof(MainLayout));
        screens.Route = Route<PageB>();
        screens.Show("/b", keepAlive: true, typeof(MainLayout));

        Assert.Equal(2, screens.Items.Count);

        Assert.True(screens.Remove("/a"));
        Assert.Single(screens.Items);
        Assert.Equal("/b", screens.Items[0].Href);
    }

    [Fact]
    public void 탭바_컨텍스트메뉴에_화면유지_keepAlive_항목이_있다()
    {
        var tabBar = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "TabBar.razor"));

        Assert.Contains("IsKeepAlive", tabBar, StringComparison.Ordinal);
        Assert.Contains("ToggleKeepAliveAsync", tabBar, StringComparison.Ordinal);
        Assert.Contains("keepAlive", tabBar, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 탭바_코드비하인드에_화면유지_토글이_구현되어_있다()
    {
        var cs = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "TabBar.razor.cs"));

        Assert.Contains("ToggleKeepAliveAsync", cs, StringComparison.Ordinal);
        Assert.Contains("auth/menu/keep-alive", cs, StringComparison.Ordinal);
        Assert.Contains("Menus.ReloadAsync", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void 통은_회로마다_하나다()
    {
        var app = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "JSiniWebApp.cs"));

        Assert.Contains("AddScoped<KeepAliveScreens>()", app, StringComparison.Ordinal);
    }

    [Fact]
    public void 메뉴_수정_팝업에_화면유지_칸이_있다()
    {
        var page = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "MenuList.razor"));

        Assert.Contains("@bind-Checked=\"_edit.Meta.KeepAlive\"", page, StringComparison.Ordinal);
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
