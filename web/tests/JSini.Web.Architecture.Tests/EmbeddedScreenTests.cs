using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>바깥 화면 끼워 넣기(Embedded.razor)의 새 창으로 열기 단추 자리와 생김새를 검사한다. iframe 이 본문 칸을 통째로 쓰고 단추는 제 줄을 깔지 않고 그 위에 겹쳐 앉으므로,
/// 감싸개 안의 여백·테두리·위치를 글자로 지킨다. 어긋나도 화면은 멀쩡히 그려진다.</summary>
public sealed class EmbeddedScreenTests
{
    [Fact]
    public void 새창열기_단추는_iframe_감싸개_안에_겹쳐_둔다()
    {
        var source = EmbeddedSource();

        var clip = source.IndexOf("class=\"jsini-embed-clip\"", StringComparison.Ordinal);
        var frame = source.IndexOf("class=\"jsini-embed\"", StringComparison.Ordinal);
        var bar = source.IndexOf("jsini-embed__toolbar", StringComparison.Ordinal);

        Assert.True(clip >= 0, "감싸개(.jsini-embed-clip)가 없다");
        Assert.True(frame > clip, "iframe 이 감싸개 안에 있어야 한다");

        // 겹쳐 두려면 감싸개 **안**이어야 한다 — 감싸개보다 앞에 있으면 제 줄을 깐다.
        Assert.True(bar > clip, "새 창으로 열기 단추가 감싸개 바깥에 있다(옛 툴바 줄)");

        // 옛 구조의 자취. 되살아나면 iframe 위에 줄이 한 칸 깔린다.
        Assert.DoesNotContain("jsini-toolbar jsini-embed__toolbar", source, StringComparison.Ordinal);
        Assert.DoesNotContain("jsini-toolbar__spacer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void 새창열기_단추는_글자_없이_그림만_보인다()
    {
        var source = EmbeddedSource();

        Assert.Contains("IconCssClass=\"jsini-icon-open\"", source, StringComparison.Ordinal);
        Assert.Contains("Title=\"새 창으로 열기\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"새 창으로 열기\"", source, StringComparison.Ordinal);

        // 테두리·바탕을 안 그리는 갈래여야 「아이콘만」이 된다.
        Assert.Contains("RenderStyle=\"ButtonRenderStyle.Link\"", source, StringComparison.Ordinal);

        // 글자를 달면 그림만 남긴 뜻이 사라진다.
        Assert.DoesNotContain("Text=\"새 창으로 열기\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void app_css_가_iframe_을_본문_칸에_꽉_채우고_단추를_띄운다()
    {
        var css = AppCss();

        Assert.Contains(".jsini-icon-open", css, StringComparison.Ordinal);
        Assert.Contains(".jsini-embed__toolbar", css, StringComparison.Ordinal);

        // 겹치려면 감싸개가 자리를 잡아 주고(relative) 단추가 떠 있어야(absolute) 한다.
        var clip = Rule(css, ".jsini-embed-clip {");
        Assert.Contains("position: relative", clip, StringComparison.Ordinal);

        var bar = Rule(css, ".jsini-embed__toolbar {");
        Assert.Contains("position: absolute", bar, StringComparison.Ordinal);

        // 본문 칸의 여백을 걷어야 iframe 이 끝까지 간다.
        var content = Rule(css, ".jsini-shell__content:has(> .jsini-embed-clip) {");
        Assert.Contains("padding: 0", content, StringComparison.Ordinal);

        // 액자가 둘로 보이던 테두리는 걷었다.
        Assert.DoesNotContain("border: 1px solid var(--jsini-border, #e0e0e0)", clip, StringComparison.Ordinal);
    }

    /// <summary>선택자 다음 중괄호까지를 한 덩어리로 떼어 낸다.</summary>
    private static string Rule(string css, string selector)
    {
        var at = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(at >= 0, $"규칙을 못 찾았다: {selector}");

        var end = css.IndexOf('}', at);
        Assert.True(end > at, $"규칙이 안 닫혔다: {selector}");

        return css[at..end];
    }

    private static string EmbeddedSource() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "Components", "Pages", "Embedded.razor"));

    private static string AppCss() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));

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
