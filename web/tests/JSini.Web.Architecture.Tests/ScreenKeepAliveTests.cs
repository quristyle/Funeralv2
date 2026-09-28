using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 화면이 <b>갔다 와도 쓰던 모습 그대로</b>인 장치(<see cref="ScreenStateName"/> ·
/// <c>CommGrd.StateKey</c> · <c>grid-scroll.js</c>)가 조용히 끊기지 않게 지킨다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 이 기능은 <b>끊어져도 화면이 멀쩡하다.</b> 빌드도 통과하고 예외도 안 나고
/// 로그도 안 남는다 — 돌아왔을 때 조건이 비어 있고 표가 맨 위에 있을 뿐이다.
/// 그것을 보는 사람은 「원래 그런 화면인가 보다」 하고 조건을 다시 건다
/// (<see cref="RequestDraftTests"/> 와 같은 종류의 함정이다).
/// </para>
///
/// <para>
/// 끊어질 자리가 셋이다 — 화면이 짐을 <b>맡기지 않거나</b>(Dispose), 돌아와서
/// <b>찾지 않거나</b>(OnInitialized), 표에 이름을 <b>안 적거나</b>(StateKey).
/// 셋 다 지우면 그냥 「기능이 없는 화면」이 되고 컴파일러가 막아 주지 않는다.
/// </para>
/// </remarks>
public sealed class ScreenKeepAliveTests
{
    private const string ScreenStateName = "ScreenState";

    /// <summary>
    /// 짐칸이 <b>회로마다 하나</b>인가.
    /// </summary>
    /// <remarks>
    /// 싱글턴으로 두면 남이 걸어 둔 조건과 남이 읽은 목록이 내 화면에 뜬다.
    /// 헬프데스크 요청에는 고객사 이름과 장애 내용이 들어 있다.
    /// </remarks>
    [Fact]
    public void 짐칸은_회로마다_하나다()
    {
        var app = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "JSiniWebApp.cs"));

        Assert.Contains($"AddScoped<{ScreenStateName}>()", app, StringComparison.Ordinal);
        Assert.DoesNotContain($"AddSingleton<{ScreenStateName}>()", app, StringComparison.Ordinal);
    }

    /// <summary>
    /// 표가 부르는 JS 이름이 <b>모듈에 다 있는가</b>.
    /// </summary>
    /// <remarks>
    /// 없는 이름을 부르면 브라우저 콘솔에만 찍히고 화면은 멀쩡하다 —
    /// 구른 자리만 안 돌아온다.
    /// </remarks>
    [Fact]
    public void 구른자리_화면이_부르는_이름이_모듈에_있다()
    {
        var called = Regex.Matches(CommGrd(), @"_scrollJs\.Invoke(?:Void)?Async(?:<[^>]*>)?\(\s*""(?<name>[^""]+)""")
            .Select(m => m.Groups["name"].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(called);

        var exported = Regex.Matches(ScrollScript(), @"export function (?<name>\w+)")
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in called)
        {
            Assert.True(exported.Contains(name),
                $"grid-scroll.js 가 '{name}' 을(를) 내놓지 않는다. 부르면 콘솔에만 찍히고 화면은 멀쩡하다.");
        }
    }

    /// <summary>
    /// 모듈을 <b>실제로 있는 경로</b>에서 가져오는가.
    /// </summary>
    /// <remarks>
    /// 경로가 틀리면 <c>import</c> 가 던지고, 우리는 그것을 삼킨다
    /// (삼키지 않으면 표 하나 때문에 화면이 죽는다). 그래서 여기가 끊기면
    /// <b>아무 흔적도 남지 않는다.</b>
    /// </remarks>
    [Fact]
    public void 구른자리_모듈_경로가_실제로_있다()
    {
        var import = Regex.Match(CommGrd(), @"""import"",\s*""\./_content/(?<rcl>[\w.]+)/(?<path>[^""]+)""");
        Assert.True(import.Success, "CommGrd 가 grid-scroll.js 를 import 하지 않는다.");

        var file = Path.Combine(
            SolutionRoot(), "src", "Shared", import.Groups["rcl"].Value, "wwwroot",
            import.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(file), $"{file} 이 없다. import 가 던지고 우리는 그것을 삼킨다.");
    }

    /// <summary>
    /// 표의 모습을 <b>읽는 쪽과 쓰는 쪽이 짝</b>인가.
    /// </summary>
    /// <remarks>
    /// <c>LayoutAutoSaving</c> 만 걸면 맡기기만 하고 영영 안 찾는다.
    /// <c>LayoutAutoLoading</c> 만 걸면 늘 빈손으로 찾는다. 어느 쪽이든
    /// <b>기능이 없는 것과 같은데 아무 말도 안 나온다.</b>
    /// </remarks>
    [Fact]
    public void 표의_모습은_맡기고_찾는_짝이_다_있다()
    {
        var grid = CommGrd();

        Assert.Contains("LayoutAutoLoading=", grid, StringComparison.Ordinal);
        Assert.Contains("LayoutAutoSaving=", grid, StringComparison.Ordinal);
    }

    /// <summary>
    /// 「요청 처리」가 <b>맡기고 · 찾고 · 표에 이름을 적는</b> 셋을 다 하는가.
    /// </summary>
    /// <remarks>
    /// 지금 <c>StateKey</c> 를 쓰는 화면은 이것 하나다. 여기가 끊기면 기능
    /// 전체가 쓰이는 데 없이 남는다.
    /// </remarks>
    [Fact]
    public void 요청처리_화면이_쓰던_모습을_맡기고_찾는다()
    {
        var page = RequestManage();

        Assert.Matches(@"\[Inject\][^\n]*ScreenState", page);
        Assert.Matches(@"public void Dispose\(\)[\s\S]{0,200}?Screen\.Set\(StateKey", page);
        Assert.Matches(@"protected override void OnInitialized\(\)[\s\S]{0,200}?Screen\.Get<\w+>\(StateKey\)", page);
        Assert.Matches(@"StateKey=""@StateKey""", page);
    }

    private static string CommGrd() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "CommGrd.razor"));

    private static string ScrollScript() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "js", "grid-scroll.js"));

    private static string RequestManage() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.HelpDesk", "Components", "Pages", "RequestManage.razor"));

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
