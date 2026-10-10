using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 끄는 도중 사이드바를 미리 접어 보이는 일(<c>theme.js</c> 의
/// <c>jsini-shell--snap</c>)이 <b>셸의 구분선에서만</b> 일어나는가.
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 화면 안에도 분할판이 산다 — 할일 목록(<c>pm-splitpane</c>)·권한 지도
/// (<c>ad-vsplit</c>)·쿼리 테스터처럼 좌우로 나뉘는 화면이 다
/// <c>DxSplitter</c> 다. 부품이 같으니 구분선도 같은
/// <c>dxbl-splitter-separator</c> 다. 갈고리를 구분선에만 걸면 <b>화면 안의
/// 판을 끌 때도 사이드바가 접혀 보인다.</b>
/// </para>
///
/// <para>
/// 그 증상은 <b>빌드도 테스트도 통과한 채로</b> 나타난다. 예외도 로그도 없고,
/// 사이드바가 한 번 접혔다가 0.8 초 뒤에 저 혼자 다시 펴질 뿐이다(진짜 접힘이
/// 영영 안 오므로 <c>stop</c> 의 시간 제한이 표시를 거둔다). 끌어 본 사람만
/// 볼 수 있는 자리라 여기서 잡는다.
/// </para>
/// </summary>
public sealed class SidebarSnapScopeTests
{
    /// <summary>
    /// 끌기를 시작하는 갈고리가 <b>셸의 분할판인지 먼저 가른다.</b>
    ///
    /// <para>
    /// 가르는 자리는 <c>drag</c> 를 채우기 전이라야 한다 — 채운 뒤에 걸러
    /// 봐야 이미 <c>pointermove</c> 가 붙어 미리 접어 보이기가 돈다.
    /// </para>
    /// </summary>
    [Fact]
    public void 미리접어보이기는_셸_분할판에서만_시작한다()
    {
        var js = RazorSource.Read(ThemeJs());

        var hook = js.IndexOf(
            "document.addEventListener('pointerdown'", StringComparison.Ordinal);

        Assert.True(hook >= 0, "theme.js 에서 구분선 끌기를 잡는 `pointerdown` 갈고리를 찾지 못했다.");

        var begin = js.IndexOf("drag = {", hook, StringComparison.Ordinal);

        Assert.True(begin > hook, "그 갈고리 안에서 끌기를 시작하는 `drag = {` 를 찾지 못했다.");

        Assert.True(
            js[hook..begin].Contains(ShellSplitClass, StringComparison.Ordinal),
            $"구분선 끌기 갈고리가 `{ShellSplitClass}` 로 거르지 않는다. "
            + "화면 안의 분할판(`pm-splitpane`·`ad-vsplit` 등)을 끄는 동안에도 "
            + "사이드바가 접혀 보인다 — 끌기를 시작하기 전에 "
            + "가장 가까운 분할판이 셸의 것인지 가른다.");
    }

    /// <summary>
    /// 셸의 분할판이 그 이름을 <b>실제로 달고 있는가.</b>
    ///
    /// <para>
    /// JS 가 그 이름으로 거르므로, 레이아웃에서 이름이 바뀌면 <b>미리 접어
    /// 보이기가 통째로 죽는다</b> — 끄는 동안 아무 일도 안 일어나고 놓는
    /// 순간에만 접힌다. 역시 오류가 아니라 「움직임이 없는 것」이다.
    /// </para>
    /// </summary>
    [Fact]
    public void 셸_분할판이_그_이름을_쓴다()
    {
        var layout = RazorSource.Read(MainLayout());

        Assert.True(
            Regex.IsMatch(layout, $@"<DxSplitter\s+CssClass=""{ShellSplitClass}"""),
            $"`MainLayout` 의 `DxSplitter` 가 `{ShellSplitClass}` 를 달고 있지 않다. "
            + "theme.js 가 그 이름으로 셸의 구분선을 가린다 — 함께 고친다.");
    }

    /// <summary>셸의 사이드바 분할판 이름. 두 파일이 이 글자로 만난다.</summary>
    private const string ShellSplitClass = "jsini-shell__split";

    private static string ThemeJs() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "theme.js");

    private static string MainLayout() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor");

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
