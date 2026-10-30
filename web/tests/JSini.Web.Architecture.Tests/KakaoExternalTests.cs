using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 카카오톡에서 연 링크를 기본 브라우저로 넘기는 스크립트(<c>js/kakao-external.js</c>)가
/// 조용히 빠지지 않게 지킨다. 빠져도 화면은 멀쩡하고, 카카오톡으로 링크를 받은
/// 사람만 인앱 브라우저에 갇힌다.
/// </summary>
public sealed class KakaoExternalTests
{
    /// <summary>
    /// <c>&lt;head&gt;</c> 에서 <b>defer · async 없이</b> 싣는가. 늦게 돌면 회로와
    /// DevExpress 스크립트를 다 받은 뒤에야 떠나서 넘기는 의미가 없다.
    /// </summary>
    [Fact]
    public void 카카오_넘김_스크립트는_head_에서_바로_돈다()
    {
        var app = App();
        var head = app[..app.IndexOf("</head>", StringComparison.Ordinal)];
        var tag = Regex.Match(head, @"<script[^>]*js/kakao-external\.js[^>]*>");

        Assert.True(tag.Success, "App.razor 의 <head> 가 js/kakao-external.js 를 싣지 않는다.");
        Assert.DoesNotContain("defer", tag.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("async", tag.Value, StringComparison.Ordinal);
    }

    /// <summary>카카오톡이 받는 외부 브라우저 스킴과 탭당 한 번 가드가 남아 있는가.</summary>
    [Fact]
    public void 카카오톡일_때만_한_번_외부_브라우저로_넘긴다()
    {
        var js = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "wwwroot", "js", "kakao-external.js"));

        Assert.Contains("KAKAOTALK", js, StringComparison.Ordinal);
        Assert.Contains("kakaotalk://web/openExternal?url=", js, StringComparison.Ordinal);
        Assert.Contains("encodeURIComponent(location.href)", js, StringComparison.Ordinal);
        Assert.Contains("sessionStorage", js, StringComparison.Ordinal);
    }

    private static string App() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "Components", "App.razor"));

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("web/src 를 찾지 못했다.");
    }
}
