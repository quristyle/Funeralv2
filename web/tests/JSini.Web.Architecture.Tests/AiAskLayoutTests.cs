using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 「빠른 지시」(<c>AiAskPanel</c>)의 상단 입력부 고정 및 하단 목록 스크롤 구조를 검사한다.
/// </summary>
public sealed class AiAskLayoutTests
{
    [Fact]
    public void 상단_입력부와_하단_목록_판이_분리되고_목록은_스크롤_영역을_갖는다()
    {
        var razor = PanelRazor();
        var code = PanelCode();

        Assert.Contains("CssClass=\"@WritePaneCss\"", razor, StringComparison.Ordinal);
        Assert.Contains("CssClass=\"@ListPaneCss\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__scroll\"", razor, StringComparison.Ordinal);

        var colsAt = razor.IndexOf("class=\"pm-ask__cols\"", StringComparison.Ordinal);
        var scrollAt = razor.IndexOf("class=\"pm-ask__scroll\"", StringComparison.Ordinal);
        var listAt = razor.IndexOf("class=\"pm-ask__list\"", StringComparison.Ordinal);
        var sentinelAt = razor.IndexOf("class=\"pm-ask__more-sentinel\"", StringComparison.Ordinal);

        Assert.True(colsAt >= 0 && scrollAt > colsAt, "단 수 고르개(.pm-ask__cols)가 스크롤 영역 위에 있어야 한다");
        Assert.True(listAt > scrollAt, "목록(.pm-ask__list)이 스크롤 영역(.pm-ask__scroll) 안에 있어야 한다");
        Assert.True(sentinelAt > listAt, "더보기 감지점(.pm-ask__more-sentinel)이 스크롤 영역 안에 있어야 한다");

        Assert.Contains("\"pm-ask__write\"", code, StringComparison.Ordinal);
        Assert.Contains("\"pm-ask__recent\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void projmng_css_가_입력부를_고정하고_아래_목록을_스크롤시킨다()
    {
        var css = ProjMngCss();

        var containerRule = Rule(css, ".jsini-shell__content:has(> .pm-ask:not(:has(> .pm-ask__view))),");
        Assert.Contains("overflow: hidden", containerRule, StringComparison.Ordinal);

        var recentRule = Rule(css, ".pm-ask__recent {");
        Assert.Contains("flex: 1 1 0", recentRule, StringComparison.Ordinal);
        Assert.Contains("min-height: 0", recentRule, StringComparison.Ordinal);

        var scrollRule = Rule(css, ".pm-ask__scroll {");
        Assert.Contains("flex: 1 1 0", scrollRule, StringComparison.Ordinal);
        Assert.Contains("min-height: 0", scrollRule, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto", scrollRule, StringComparison.Ordinal);
    }

    [Fact]
    public void 빠른지시_목록의_카드를_누르면_상세화면_주소로_이동한다()
    {
        var code = PanelCode();

        Assert.Contains("Navigation.NavigateTo($\"/projmng/ai/task/{taskKey}\")", code, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!_isPhone)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void 완료건_일괄확인_버튼과_4단_순환_아이콘이_목록_상단에_제공된다()
    {
        var razor = PanelRazor();
        var code = PanelCode();

        Assert.Contains("class=\"pm-ask__batch-btn\"", razor, StringComparison.Ordinal);
        Assert.Contains("ConfirmCompletedBatchAsync", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__col-btn\"", razor, StringComparison.Ordinal);
        Assert.Contains("CycleCols", razor, StringComparison.Ordinal);

        var batchAt = razor.IndexOf("class=\"pm-ask__batch-btn\"", StringComparison.Ordinal);
        var colsAt = razor.IndexOf("class=\"pm-ask__cols\"", StringComparison.Ordinal);
        var scrollAt = razor.IndexOf("class=\"pm-ask__scroll\"", StringComparison.Ordinal);

        Assert.True(batchAt >= 0 && scrollAt > batchAt, "완료건 일괄확인 버튼이 스크롤 영역 위에 있어야 한다");
        Assert.True(colsAt > batchAt, "완료건 일괄확인 버튼이 단 수 버튼보다 좌측(앞)에 있어야 한다");

        Assert.Contains("ColChoices = [1, 2, 3, 4]", code, StringComparison.Ordinal);
        Assert.Contains("ConfirmCompletedBatchAsync()", code, StringComparison.Ordinal);
        Assert.Contains("CycleCols()", code, StringComparison.Ordinal);
    }

    private static string Rule(string css, string selector)
    {
        var at = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(at >= 0, $"규칙을 못 찾았다: {selector}");

        var end = css.IndexOf('}', at);
        Assert.True(end > at, $"규칙이 안 닫혔다: {selector}");

        return css[at..end];
    }

    private static string PanelRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "AiAskPanel.razor"));

    private static string PanelCode() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "AiAskPanel.razor.cs"));

    private static string ProjMngCss() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "wwwroot", "projmng.css"));

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
