using System.Text.Json;
using JSini.Web.Components.Layout;
using JSini.Web.Components.Settings;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 데스크탑 환경에서 오른쪽 서랍(<see cref="RightSideDrawer"/> · <see cref="QuickAskDrawer"/>)의
/// 조절한 창 너비를 기억하고, 다음 실행 및 열기 시 기억된 창 너비로 열리는지 검사한다.
/// </summary>
public sealed class RightSideDrawerWidthTests
{
    [Fact]
    public void 빠른지시_서랍은_껍데기를_미리_마운트하고_알맹이만_지연_생성한다()
    {
        var quickAskRazor = RazorSource.Read(Path.Combine(
            SolutionRoot(),
            "src", "Shared", "JSini.Web.Components", "Layout", "QuickAskDrawer.razor"));

        var outerIfIdx = quickAskRazor.IndexOf("@if (Ask.PanelType is { } panel)", StringComparison.Ordinal);
        var drawerIdx = quickAskRazor.IndexOf("<RightSideDrawer Id=\"jsini-quick-ask-drawer\"", StringComparison.Ordinal);
        var innerEverIdx = quickAskRazor.IndexOf("@if (_ever)", StringComparison.Ordinal);
        var dynamicIdx = quickAskRazor.IndexOf("<DynamicComponent Type=\"@panel\" />", StringComparison.Ordinal);

        Assert.True(outerIfIdx >= 0, "QuickAskDrawer 는 Ask.PanelType 이 있으면 서랍 껍데기를 마운트해야 합니다.");
        Assert.True(drawerIdx > outerIfIdx, "RightSideDrawer 가 바깥 조건문 안에 있어야 합니다.");
        Assert.True(innerEverIdx > drawerIdx && dynamicIdx > innerEverIdx,
            "_ever 조건은 RightSideDrawer 의 ChildContent 내부에서 DynamicComponent 만 감싸야 첫 열기부터 기억된 너비가 적용됩니다.");
    }

    [Fact]
    public void 오른쪽_서랍은_데스크탑에서_기억된_너비를_복원하고_저장한다()
    {
        var drawerRazor = RazorSource.Read(Path.Combine(
            SolutionRoot(),
            "src", "Shared", "JSini.Web.Components", "Layout", "RightSideDrawer.razor"));
        var drawerCs = RazorSource.Read(Path.Combine(
            SolutionRoot(),
            "src", "Shared", "JSini.Web.Components", "Layout", "RightSideDrawer.razor.cs"));
        var drawerJs = RazorSource.Read(Path.Combine(
            SolutionRoot(),
            "src", "Shared", "JSini.Web.Components", "wwwroot", "js", "drawer-click.js"));

        Assert.Contains("style=\"@DrawerStyle\"", drawerRazor);
        Assert.Contains("!IsPhone && _widthPx is > 0", drawerCs);
        Assert.Contains("Boot.ReadAsync()", drawerCs);
        Assert.Contains("RightDrawerWidthPx", drawerCs);
        Assert.Contains("SetRightDrawerWidthAsync", drawerCs);
        Assert.Contains("OnWidthChanged", drawerCs);

        Assert.Contains("saveWidthToStorage", drawerJs);
        Assert.Contains("notifyDotNetWidthChanged", drawerJs);
        Assert.Contains("isDesktopViewport", drawerJs);
    }

    [Fact]
    public void 부트_상태와_계정_설정은_기억된_서랍_너비를_복원한다()
    {
        var wire = new PortalBoot.BootWire
        {
            Local = new Dictionary<string, string?>
            {
                [PortalBoot.RightDrawerWidthKey] = "580",
            },
        };

        var state = PortalBoot.BrowserState.From(wire);
        Assert.Equal(580, state.RightDrawerWidthPx);

        using var doc = JsonDocument.Parse("""{"portal.rightDrawer.width": 640}""");
        var prefsData = PortalPrefsData.From(doc.RootElement.Clone());
        var merged = state.WithAccountPrefs(prefsData);
        Assert.Equal(640, merged.RightDrawerWidthPx);
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
