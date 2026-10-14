using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 바깥 화면 끼워 넣기(<c>Embedded.razor</c>) 화면의 <b>새 창으로 열기 단추</b> 배치와 아이콘을 검사한다.
///
/// <para>
/// [왜 기계가 봐야 하는가]
/// </para>
///
/// <para>
/// `/embed/nums_prod` 나 `/embed/projmng/external/funeral-monitor` 처럼
/// 타시스템의 frame 을 보여주는 공용 화면(<c>Embedded.razor</c>)은
/// 상단 오른쪽에 아이콘 형태의 「새 창으로 열기」 단추를 둔다.
/// </para>
/// </summary>
public sealed class EmbeddedScreenTests
{
    [Fact]
    public void Embedded_화면의_새창열기_단추는_아이콘으로_상단_오른쪽에_배치된다()
    {
        var source = EmbeddedSource();

        // 상단 오른쪽에 배치하기 위한 툴바와 스페이서
        Assert.Contains("jsini-embed__toolbar", source, StringComparison.Ordinal);
        Assert.Contains("jsini-toolbar__spacer", source, StringComparison.Ordinal);

        // 아이콘 형태로 표현
        Assert.Contains("IconCssClass=\"jsini-icon-open\"", source, StringComparison.Ordinal);
        Assert.Contains("Title=\"새 창으로 열기\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void app_css_에_새창열기_아이콘과_툴바_스타일이_정의되어_있다()
    {
        var css = AppCss();

        Assert.Contains(".jsini-icon-open", css, StringComparison.Ordinal);
        Assert.Contains(".jsini-embed__toolbar", css, StringComparison.Ordinal);
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
