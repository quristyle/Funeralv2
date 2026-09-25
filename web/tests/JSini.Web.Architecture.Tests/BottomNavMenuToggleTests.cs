using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 아래 띠(<c>MobileBottomNav</c>)의 「메뉴」 칸이 <b>여닫이인가</b>.
///
/// <para>
/// [왜 기계가 봐야 하는가]
/// </para>
///
/// <para>
/// 그 칸은 헤더의 ☰ · 떠다니는 단추 · 로고와 <b>같은 물건</b>이다 —
/// 넷 다 <c>_sidebarOpen</c> 하나를 여닫는다. 그런데 앞의 셋은
/// <c>ToggleSidebarAsync</c> 를 부르고, 띠만 <c>MenuReveal</c> 을 거쳐
/// <c>OnMenuRevealRequested</c> 로 들어온다. 그쪽은 브레드크럼도 쓰는 길이라
/// <b>「펴져 있으면 아무것도 안 한다」가 기본</b>이고, 그 자리에 띠의 표시를
/// 가려내는 갈래가 없으면 <b>판이 덮여 있는 채로 같은 자리를 다시 눌러도
/// 아무 일이 없다.</b>
/// </para>
///
/// <para>
/// 화면은 멀쩡하다 — 띠도 보이고 누름도 들어온다(띠가 z-index 1030 으로
/// 사이드바와 같은 층에 서 있어서 덮개에 먹히지 않는다). 휴대폰 폭으로
/// 열어 봐야 드러나는 종류라 여기서 글자로 지킨다.
/// </para>
///
/// <para>
/// <b>브레드크럼 쪽은 반대로 접으면 안 된다</b> — 「이 메뉴를 보여 달라」에
/// 판을 닫는 것은 부탁의 반대다. 그래서 갈래는 <c>BottomNav.MenuPath</c>
/// 하나로만 좁혀져 있어야 한다.
/// </para>
/// </summary>
public sealed class BottomNavMenuToggleTests
{
    /// <summary>
    /// 띠의 「메뉴」 칸은 여전히 <c>MenuReveal</c> 로 그 표시를 보낸다.
    /// 여기가 바뀌면 아래 두 검사가 지키는 것이 뜻을 잃는다.
    /// </summary>
    [Fact]
    public void 띠의_메뉴_칸은_표시를_보낸다() =>
        Assert.Contains("Reveal.Request(BottomNav.MenuPath)", BottomNavRazor(), StringComparison.Ordinal);

    /// <summary>
    /// 펴져 있을 때 <b>그 표시로 들어오면 접는다.</b> 「펴져 있으면 돌아간다」로만
    /// 두면 다시 누르는 것이 죽은 누름이 된다.
    /// </summary>
    [Fact]
    public void 펴져_있으면_그_표시로_접는다()
    {
        var body = RevealHandler();

        Assert.Contains("BottomNav.MenuPath", body, StringComparison.Ordinal);
        Assert.Contains("CloseSidebar()", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>접는 것은 그 표시일 때뿐이다.</b> 조건 없이 접으면 브레드크럼으로
    /// 메뉴를 보여 달라고 한 사람의 판까지 닫힌다.
    /// </summary>
    [Fact]
    public void 접는_것은_그_표시일_때뿐이다()
    {
        var body = RevealHandler();
        var close = body.IndexOf("CloseSidebar()", StringComparison.Ordinal);
        var guard = body.IndexOf("BottomNav.MenuPath", StringComparison.Ordinal);

        Assert.True(guard >= 0 && guard < close,
            "`CloseSidebar()` 앞에 `BottomNav.MenuPath` 판정이 없다. "
            + "조건 없이 접으면 브레드크럼이 판을 여는 길이 함께 죽는다.");
    }

    /// <summary>
    /// 펴는 길은 그대로다 — 접혀 있으면 어느 부탁이든 편다.
    /// </summary>
    [Fact]
    public void 접혀_있으면_여전히_편다() =>
        Assert.Contains("OpenSidebarAsync()", RevealHandler(), StringComparison.Ordinal);

    /// <summary><c>OnMenuRevealRequested</c> 의 몸통 글자.</summary>
    private static string RevealHandler()
    {
        var source = MainLayoutRazor();
        var start = source.IndexOf("private void OnMenuRevealRequested", StringComparison.Ordinal);

        Assert.True(start >= 0, "`MainLayout` 에 `OnMenuRevealRequested` 가 없다.");

        // 다음 멤버 선언까지를 몸통으로 본다. 중괄호를 세지 않는 것은 그
        // 안에 람다와 문자열이 섞여 있어 정규식으로는 어차피 정확하지 않기
        // 때문이다 — 여기서 필요한 것은 「이 처리기 안에 그 글자가 있나」뿐이다.
        var next = Regex.Match(source[start..], @"\n    (?:///|private|protected|public)", RegexOptions.None);
        var end = next.Success && next.Index > 0 ? start + next.Index : source.Length;

        return source[start..end];
    }

    private static string MainLayoutRazor() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor"));

    private static string BottomNavRazor() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MobileBottomNav.razor"));

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
