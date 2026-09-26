using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 환경설정의 「아래 띠 고르기」 줄이 <b>휴대폰에서 한 줄</b>인가
/// (<c>funeral.css</c> 의 <c>.fn-bnav__row</c>).
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 줄에 차례 바꾸기 꺾쇠 둘을 더했더니 휴대폰에서 <b>줄이 하나 더</b>
/// 생겼다 — 감싸는 상자(<c>flex-wrap: wrap</c>)는 폭이 모자랄 때 글상자를
/// 줄이는 대신 먼저 뒷것들을 아래로 떨어뜨리기 때문이다. 빌드도 테스트도
/// 통과했고 화면도 열렸다. 사람이 휴대폰으로 열어 보고서야 드러났다.
/// </para>
///
/// <para>
/// 여기서 <b>배치를 잴 수는 없다</b>(테스트는 브라우저가 아니다). 대신
/// 고쳐 둔 두 규칙이 <b>짝으로 남아 있는지</b>만 본다 — 하나만 지워지면
/// 그 자리가 되돌아간다.
/// </para>
/// </summary>
public sealed class BottomNavSettingRowTests
{
    /// <summary>
    /// 기본은 <c>nowrap</c> 이다. 폭이 모자라면 줄을 내리는 대신
    /// 글상자가 줄어든다(이름 칸이 먼저 준다 — <c>flex: 0 3 7rem</c>).
    /// </summary>
    [Fact]
    public void 줄은_감기지_않는다()
    {
        var rule = Rule(@"^\.fn-bnav__row \{(.*?)\}");

        Assert.Contains("flex-wrap: nowrap", rule, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>칸 셋으로 갈리는 넓은 화면(1200px 이상)에서는 다시 감는다.</b>
    /// 그때 이 판이 앉는 자리는 260px 남짓이라 휴대폰(360px)보다 좁고,
    /// 한 줄에 붙이면 메뉴 고르개가 40px 로 눌린다.
    /// </summary>
    [Fact]
    public void 셋으로_갈린_칸에서는_다시_감는다()
    {
        var rule = Rule(@"^  \.fn-env \.fn-bnav__row \{(.*?)\}");

        Assert.Contains("flex-wrap: wrap", rule, StringComparison.Ordinal);
    }

    /// <summary>
    /// 이름 칸에 바닥이 있다. 없으면 320px 에서 20px 까지 눌려 글자가
    /// 한 자도 안 남는다.
    /// </summary>
    [Fact]
    public void 이름_칸이_먼저_줄되_바닥이_있다()
    {
        var rule = Rule(@"^\.fn-bnav__name \{(.*?)\}");

        Assert.Contains("flex: 0 3 7rem", rule, StringComparison.Ordinal);
        Assert.Contains("min-width: 3.25rem", rule, StringComparison.Ordinal);
    }

    private static string Rule(string pattern)
    {
        var match = Regex.Match(FuneralCss(), pattern, RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(match.Success, $"funeral.css 에서 `{pattern}` 규칙을 찾지 못했다.");
        return match.Groups[1].Value;
    }

    private static string FuneralCss() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Funeral", "wwwroot", "funeral.css"));

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
