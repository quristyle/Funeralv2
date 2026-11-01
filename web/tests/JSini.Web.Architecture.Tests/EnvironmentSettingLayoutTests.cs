using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>환경설정 · 내 알림 설정(.fn-env)이 흘리는 단(CSS columns)으로 되돌아가지 않는가. 그 단 안에서는 DevExpress 부품이 크기를 잴 때마다 판 전체를 다시 나눠
/// 배치 한 번이 100ms 를 넘었고, 화면을 열어 두기만 해도 마우스가 굳었다. 빌드·테스트·화면 모양은 멀쩡해 눈으로는 못 잡는다.</summary>
public sealed class EnvironmentSettingLayoutTests
{
    [Fact]
    public void 흘리는_단을_쓰지_않는다()
    {
        var css = RazorSource.Read(Path.Combine(SolutionRoot(), "src", "Apps", "JSini.Web.Funeral", "wwwroot", "funeral.css"));
        var rules = Regex.Matches(css, @"\.fn-env[^{]*\{([^}]*)\}");

        Assert.NotEmpty(rules);
        Assert.All(rules, m => Assert.DoesNotMatch(@"(^|[\s;])(columns|column-count|column-width)\s*:", m.Groups[1].Value));
    }

    /// <summary>단은 화면의 감싸개 셋이 정한다(funeral.css 가 nth-child 로 자리를 준다). 수가 어긋나면 격자 칸이 비거나 묶음이 엉뚱한 단에 선다.</summary>
    [Fact]
    public void 환경설정은_감싸개_셋으로_단을_가른다()
    {
        var razor = RazorSource.Read(Path.Combine(SolutionRoot(), "src", "Apps", "JSini.Web.Funeral", "Components", "Pages", "EnvironmentSettingPage.razor"));

        Assert.Equal(3, Regex.Matches(razor, @"<div class=""fn-env__col"">").Count);
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
