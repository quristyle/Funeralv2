using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>늘 쓰는 테마 스타일시트 두 장이 공통 head 에 붙박여 첫 그림을 막는지 지킨다. theme.js 가 꽂게 되돌리면 빌드도 화면도 멀쩡한데
/// 새로고침마다 Bootstrap 없는 깨진 화면이 먼저 스친다 — 눈으로는 회선이 느린 곳에서만 보인다.</summary>
public sealed class ThemeStylesheetBlockingTests
{
    public static TheoryData<string> Sheets => new()
    {
        "_content/JSini.Web.Components/bootstrap/bootstrap.min.css",
        "_content/DevExpress.Blazor.Themes/bootstrap-external.bs5.min.css",
    };

    /// <summary>두 장이 마크업에 있고, theme.js 가 받아 쓸 열쇠(<c>data-jsini-theme-src</c>)가 붙어 있고, theme.js 보다 앞이다.</summary>
    [Theory]
    [MemberData(nameof(Sheets))]
    public void 테마_두_장이_theme_js_보다_앞에_붙박여_있다(string href)
    {
        var head = Head();
        var at = head.IndexOf($"data-jsini-theme-src=\"{href}\"", StringComparison.Ordinal);
        var script = head.IndexOf("_content/JSini.Web.Components/theme.js", StringComparison.Ordinal);

        Assert.True(at >= 0, $"JSiniHead 에 붙박은 테마 <link> 가 없다: {href}");
        Assert.True(script > at, "테마 <link> 가 theme.js 보다 뒤에 있다 — 자리표가 그 앞에 서서 순서가 뒤집힌다.");

        // theme.js 가 마크업의 것을 찾는 열쇠와 새로 꽂을 때 쓰는 주소가 같은 글자여야 한다. 어긋나면 같은 파일이 두 번 실린다.
        Assert.Contains($"'{href.Substring(href.LastIndexOf('/') + 1)}'", Theme(), StringComparison.Ordinal);
    }

    /// <summary>Bootstrap 이 DevExpress 다리보다 앞이다 — 뒤집히면 DevExpress 부품만 옛 색으로 남는다.</summary>
    [Fact]
    public void Bootstrap_본체가_다리보다_앞이다()
    {
        var head = Head();
        Assert.True(
            head.IndexOf("bootstrap/bootstrap.min.css", StringComparison.Ordinal)
            < head.IndexOf("bootstrap-external.bs5.min.css", StringComparison.Ordinal));
    }

    /// <summary>theme.js 가 붙박은 것을 받아 쓴다(<c>adopt</c>). 없으면 같은 두 장을 또 꽂는다.</summary>
    [Fact]
    public void theme_js_가_붙박은_것을_받아_쓴다()
    {
        Assert.Contains("data-jsini-theme-src", Theme(), StringComparison.Ordinal);
        Assert.Contains("links[href] || adopt(href)", Theme(), StringComparison.Ordinal);
    }

    private static string Head() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "JSiniHead.razor"));

    private static string Theme() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "theme.js"));

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
