using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>아래 띠(MobileBottomNav)의 「메뉴」 칸은 여닫이여야 한다. 칸·헤더 ☰·떠다니는 단추·로고는 같은 물건이고, 칸만 MenuReveal 을 거쳐 「펴져 있으면 아무것도 안 한다」 쪽으로 들어가 덮인 판을 다시 눌러도 닫히지 않는다.
/// 브레드크럼은 반대로 접으면 안 되므로 갈래는 BottomNav.MenuPath 로만 좁힌다. 휴대폰 폭에서만 드러나 글자로 지킨다.</summary>
public sealed class BottomNavMenuToggleTests
{
    /// <summary>「메뉴」 칸은 MenuReveal 로 표시를 보낸다. 보는 자리는 MobileBottomNav 가 아니라 BottomNav 다 — 넓은 화면 헤더도 같은 칸 목록을 그리므로 둘 중 하나만 고쳐도 통과하는 것을 막는다.</summary>
    [Fact]
    public void 띠의_메뉴_칸은_표시를_보낸다() =>
        Assert.Contains("reveal.Request(MenuPath)", BottomNavSource(), StringComparison.Ordinal);

    /// <summary>펴져 있을 때 그 표시로 들어오면 접는다. 「펴져 있으면 돌아간다」로만 두면 다시 누르는 것이 죽은 누름이 된다.</summary>
    [Fact]
    public void 펴져_있으면_그_표시로_접는다()
    {
        var body = RevealHandler();

        Assert.Contains("BottomNav.MenuPath", body, StringComparison.Ordinal);
        Assert.Contains("CloseSidebar()", body, StringComparison.Ordinal);
    }

    /// <summary>접는 것은 그 표시일 때뿐이다 — 조건 없이 접으면 브레드크럼으로 메뉴를 보여 달라고 한 사람의 판까지 닫힌다.</summary>
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

    /// <summary>펴는 길은 그대로다 — 접혀 있으면 어느 부탁이든 편다.</summary>
    [Fact]
    public void 접혀_있으면_여전히_편다() =>
        Assert.Contains("OpenSidebarAsync()", RevealHandler(), StringComparison.Ordinal);

    /// <summary><c>OnMenuRevealRequested</c> 의 몸통 글자.</summary>
    private static string RevealHandler()
    {
        var source = MainLayoutRazor();
        var start = source.IndexOf("private void OnMenuRevealRequested", StringComparison.Ordinal);

        Assert.True(start >= 0, "`MainLayout` 에 `OnMenuRevealRequested` 가 없다.");

        // 다음 멤버 선언까지를 몸통으로 본다. 중괄호를 세지 않는다 — 람다와 문자열이 섞여 정규식으로는 정확하지 않고, 필요한 것은 「이 처리기 안에 그 글자가 있나」뿐이다.
        var next = Regex.Match(source[start..], @"\n    (?:///|private|protected|public)", RegexOptions.None);
        var end = next.Success && next.Index > 0 ? start + next.Index : source.Length;

        return source[start..end];
    }

    private static string MainLayoutRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor"));

    private static string BottomNavSource() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "BottomNav.cs"));

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
