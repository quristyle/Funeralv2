using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 띠에 고른 칸들이 <b>넓은 화면 헤더에도 선다</b>(<c>HeaderNavTools</c>).
///
/// <para>
/// [왜 기계가 봐야 하는가]
/// </para>
///
/// <para>
/// 같은 칸 목록을 그리는 자리가 둘이 되었다 — 휴대폰 아래 띠
/// (<c>MobileBottomNav</c>)와 헤더 오른쪽 도구 줄. 둘은 <b>서로 반대쪽
/// 화면에서만 보여서</b>, 한쪽을 열어 보는 것으로는 다른 쪽이 깨진 것을
/// 알 수 없다. 특히 나쁜 갈래가 셋이다.
/// </para>
///
/// <list type="bullet">
///   <item>
///     <b>휴대폰에서 둘 다 선다.</b> 감추는 일을 CSS 가 하는데, 세우는 규칙이
///     이 파일에서 훨씬 뒤에 와서 같은 무게로 적으면 뒤엣것이 이긴다. 그러면
///     360px 짜리 헤더에 단추가 한 벌 더 붙어 줄이 통째로 못 쓰게 된다.
///   </item>
///   <item>
///     <b>끈 사람의 회로에 단추가 살아 있다.</b> CSS 로만 감추면 안 보이는
///     단추들이 그대로 남고, 그중에는 서랍을 여는 진짜 조작이 섞여 있다.
///   </item>
///   <item>
///     <b>휴대폰 띠를 끈 사람의 고르개가 영영 잠긴다.</b> 잠금 판정이 휴대폰
///     스위치 하나면 그렇게 된다 — 헤더만 쓰기로 한 사람이 칸을 못 고친다.
///   </item>
/// </list>
/// </summary>
public sealed class HeaderNavToolsTests
{
    /// <summary>
    /// 휴대폰에서는 CSS 가 감춘다. <b>한 칸 더 짚어 두었는지까지 본다</b> —
    /// 세우는 규칙(<c>display: inline-flex</c>)이 이 파일에서 훨씬 뒤에 오므로
    /// 같은 무게로 적으면 휴대폰에서 그대로 선다.
    /// </summary>
    [Fact]
    public void 휴대폰에서는_헤더_묶음을_감춘다()
    {
        var css = AppCss();
        var hide = css.IndexOf(".jsini-shell .jsini-header__nav { display: none; }", StringComparison.Ordinal);

        Assert.True(hide >= 0,
            "휴대폰에서 헤더의 바로가기 묶음을 감추는 규칙이 없다. "
            + "`.jsini-shell` 을 뗀 채로 적으면 뒤에 오는 `display: inline-flex` 가 이긴다.");

        Assert.Contains("max-width: 767px", EnclosingMedia(css, hide), StringComparison.Ordinal);
    }

    /// <summary>넓은 화면에서는 실제로 선다. 감추는 규칙만 남으면 기능이 통째로 없다.</summary>
    [Fact]
    public void 넓은_화면에서는_헤더에_선다()
    {
        var rule = Rule(AppCss(), @"^\.jsini-header__nav \{(.*?)\}");

        Assert.Contains("display: inline-flex", rule, StringComparison.Ordinal);
    }

    /// <summary>
    /// 쓰지 않기로 한 사람에게는 <b>마크업이 없다.</b> 아래 띠와 같은 규칙이다
    /// (<c>MainLayout</c> 의 <c>_bottomNavHidden</c> 갈래).
    /// </summary>
    [Fact]
    public void 끈_사람에게는_아예_안_그린다()
    {
        var layout = MainLayout();
        var guard = layout.IndexOf("@if (!_bottomNavDesktopHidden)", StringComparison.Ordinal);
        var draw = layout.IndexOf("<HeaderNavTools />", StringComparison.Ordinal);

        Assert.True(guard >= 0 && draw > guard && draw - guard < 200,
            "`<HeaderNavTools />` 가 `@if (!_bottomNavDesktopHidden)` 안에 있지 않다. "
            + "CSS 로만 감추면 끈 사람의 회로에 안 보이는 단추들이 그대로 산다.");
    }

    /// <summary>헤더도 띠와 <b>같은 목록</b>을 읽는다. 갈라 읽으면 둘이 서로 다른 칸을 보여 준다.</summary>
    [Fact]
    public void 띠와_같은_칸_목록을_읽는다()
    {
        var source = HeaderNavToolsSource();

        Assert.Contains("BottomNavItemsJson", source, StringComparison.Ordinal);
        Assert.Contains("BottomNav.Parse", source, StringComparison.Ordinal);
        Assert.Contains("BottomNav.Open", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 환경설정에 <b>따로 선 스위치</b>가 있다. 휴대폰 스위치에 매달면
    /// 「띠는 끄고 헤더만 쓴다」가 불가능해진다.
    /// </summary>
    [Fact]
    public void 환경설정에_데스크탑_스위치가_따로_있다()
    {
        var page = EnvironmentSettingPage();

        Assert.Contains("OnBottomNavDesktopUsedChangedAsync", page, StringComparison.Ordinal);
        Assert.Contains("SetBottomNavDesktopHiddenAsync", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// 고르개는 <b>둘 다 꺼야</b> 잠긴다. 휴대폰 스위치 하나로 잠그면 헤더만
    /// 쓰는 사람이 칸을 영영 못 고친다.
    /// </summary>
    [Fact]
    public void 고르개는_둘_다_꺼야_잠긴다()
    {
        var page = EnvironmentSettingPage();

        Assert.Contains("_bottomNavHidden && _bottomNavDesktopHidden", page, StringComparison.Ordinal);
        Assert.Contains("NavLocked", page, StringComparison.Ordinal);
    }

    /// <summary><paramref name="index"/> 를 감싸고 있는 <c>@media</c> 줄.</summary>
    private static string EnclosingMedia(string css, int index)
    {
        var media = css.LastIndexOf("@media", index, StringComparison.Ordinal);

        Assert.True(media >= 0, "그 규칙이 `@media` 블록 밖에 있다 — 넓은 화면에서도 감춰진다.");

        var eol = css.IndexOf('\n', media);
        return css[media..(eol < 0 ? css.Length : eol)];
    }

    private static string Rule(string css, string pattern)
    {
        var match = Regex.Match(css, pattern, RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(match.Success, $"app.css 에서 `{pattern}` 규칙을 찾지 못했다.");
        return match.Groups[1].Value;
    }

    private static string AppCss() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));

    private static string MainLayout() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor"));

    private static string HeaderNavToolsSource() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "HeaderNavTools.razor"));

    private static string EnvironmentSettingPage() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Funeral", "Components", "Pages",
        "EnvironmentSettingPage.razor"));

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
