using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 휴대폰 아래 띠의 <b>이름을 감췄을 때</b> 띠가 실제로 얇아지고 그만큼을
/// 본문이 돌려받는가(<c>app.css</c> 의 <c>--jsini-bottomnav-h</c>).
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 띠는 <c>position: fixed</c> 라 <b>자리를 차지하지 않는다.</b> 본문·사이드바·
/// 떠다니는 단추·오른쪽 서랍이 저마다 띠 높이만큼 비켜서 있고, 그 숫자가
/// 한동안 아홉 자리에 <b>60px 로 베껴 적혀</b> 있었다. 토큰 하나로 모아 두지
/// 않으면 「이름을 감췄더니 띠는 얇아졌는데 본문 아래가 그대로 비어 있다」가
/// 되는데, <b>빌드도 테스트도 화면도 멀쩡하다</b> — 드러나는 조건이
/// 「휴대폰 · 이름 감춤」 둘이 겹칠 때뿐이라 눈으로는 못 지킨다.
/// </para>
///
/// <para>
/// 그래서 여기서 재는 것은 배치가 아니라 <b>그 아홉이 같은 토큰을 보고
/// 있는지</b>다.
/// </para>
/// </summary>
public sealed class BottomNavLabelTests
{
    /// <summary>
    /// 기본은 60px 이고, 이름을 감춘 사람에게만 44px 이다.
    /// </summary>
    /// <remarks>
    /// 44px 보다 더 줄이지 않는 까닭은 app.css 의 같은 자리에 적어 두었다 —
    /// 아이콘만 남으면 28px 까지도 되지만, 칸 여덟이 나란히 선 띠에서
    /// 세로로 좁히는 만큼 손가락이 빗나간다.
    /// </remarks>
    [Fact]
    public void 이름을_감추면_띠_높이_토큰이_줄어든다()
    {
        // `:root` 가 파일에 여덟 벌이라(폭·테마마다 다시 적는다) 묶음을
        // 집어내는 대신 **들여쓰기 두 칸**으로 그 한 줄을 집는다.
        Assert.Contains("\n  --jsini-bottomnav-h: 60px;", AppCss(), StringComparison.Ordinal);

        Assert.Contains("--jsini-bottomnav-h: 44px",
            Rule(@"^\.jsini-shell--bottomnav-nolabel \{(.*?)\}"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 띠 자신의 높이가 그 토큰이다. 여기만 숫자로 두면 토큰을 고쳐도
    /// <b>띠만 안 따라온다</b> — 본문은 비켰는데 띠가 그대로인 꼴이다.
    /// </summary>
    [Fact]
    public void 띠_높이는_그_토큰이다() =>
        Assert.Contains("height: calc(var(--jsini-bottomnav-h) + env(safe-area-inset-bottom))",
            Rule(@"^\.jsini-bottom-nav \{(.*?)\n\}"), StringComparison.Ordinal);

    /// <summary>
    /// <b>띠를 피해 서 있는 자리에 숫자가 남아 있지 않다.</b> 60px(띠 높이) ·
    /// 76px(띠 + 틈 16) · 120px(띠 + 숨 쉴 틈 60) 셋이 그 숫자였다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「띠를 쓰지 않기로 한 사람」(<c>--bottomnav-hidden</c>)의 되돌림 규칙은
    /// 뺀다 — 거기 적힌 60px 은 <b>띠 높이가 아니라</b> 띠가 없는 자리에
    /// 남겨 두는 숨 쉴 틈이라, 띠가 얇아진다고 함께 줄 값이 아니다.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("60px")]
    [InlineData("76px")]
    [InlineData("120px")]
    public void 띠를_피하는_자리에_숫자를_베껴_적지_않는다(string copied)
    {
        var left = Phone()
            .Split('\n')
            .Where(line => !line.Contains("bottomnav-hidden", StringComparison.Ordinal))
            .Where(line => line.Contains($": {copied}", StringComparison.Ordinal)
                || line.Contains($"calc({copied} ", StringComparison.Ordinal))
            .ToList();

        Assert.True(left.Count == 0,
            $"휴대폰 갈래에 {copied} 가 남아 있다. `--jsini-bottomnav-h` 를 봐야 한다:\n"
            + string.Join('\n', left));
    }

    /// <summary>
    /// 이름은 <b>자리만 뺏고 글자는 남긴다.</b> <c>display: none</c> 으로
    /// 지우면 칸(<c>&lt;button&gt;</c>)의 <b>읽을 이름이 통째로 사라진다</b> —
    /// 아이콘은 <c>aria-hidden</c> 이고 얼굴 그림은 <c>alt=""</c> 라, 화면
    /// 낭독기에는 「단추」만 다섯 줄로 남는다. 눈으로 보는 화면은 멀쩡하므로
    /// 이 고장은 눈으로는 안 드러난다.
    /// </summary>
    [Fact]
    public void 이름은_지우지_않고_자리만_뺏는다()
    {
        var rule = Rule(
            @"^\.jsini-shell--bottomnav-nolabel \.jsini-bottom-nav__item > span:last-child \{(.*?)\}");

        Assert.Contains("clip-path: inset(50%)", rule, StringComparison.Ordinal);
        Assert.Contains("position: absolute", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("display: none", rule, StringComparison.Ordinal);
    }

    /// <summary>
    /// 아이콘 아래 틈(4px)도 함께 걷는다. 그 틈은 <b>이름과의 사이</b>를
    /// 벌리려고 둔 것이라, 이름이 없는 띠에서는 아이콘을 4px 위로 밀어
    /// 올리기만 한다.
    /// </summary>
    [Fact]
    public void 아이콘_아래_틈도_함께_걷는다()
    {
        var css = AppCss();

        Assert.Contains(
            ".jsini-shell--bottomnav-nolabel .jsini-bottom-nav__icon,\n"
            + ".jsini-shell--bottomnav-nolabel .jsini-bottom-nav__avatar {\n"
            + "    margin-bottom: 0;",
            css, StringComparison.Ordinal);
    }

    /// <summary>
    /// 셸이 그 표시를 실제로 붙이는가. CSS 만 있고 붙이는 쪽이 없으면
    /// <b>스위치가 조용히 아무 일도 안 한다.</b>
    /// </summary>
    [Fact]
    public void 셸이_그_표시를_붙인다()
    {
        var layout = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor"));

        Assert.Contains("jsini-shell--bottomnav-nolabel", layout, StringComparison.Ordinal);
        Assert.Contains("SetBottomNavLabelsHiddenAsync", RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Apps", "JSini.Web.Funeral",
            "Components", "Pages", "EnvironmentSettingPage.razor")), StringComparison.Ordinal);
    }

    /// <summary>휴대폰 갈래(≤767px)의 마지막 묶음. 띠를 피하는 규칙이 전부 거기다.</summary>
    private static string Phone()
    {
        var css = AppCss();
        var at = css.LastIndexOf("@media (max-width: 767px) {", StringComparison.Ordinal);

        Assert.True(at >= 0, "app.css 에서 휴대폰 갈래를 찾지 못했다.");
        return css[at..];
    }

    private static string Rule(string pattern)
    {
        var match = Regex.Match(AppCss(), pattern,
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(match.Success, $"app.css 에서 `{pattern}` 규칙을 찾지 못했다.");
        return match.Groups[1].Value;
    }

    private static string AppCss() => File.ReadAllText(Path.Combine(
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
