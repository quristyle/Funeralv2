using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 연결이 끊겼을 때 뜨는 대화상자가 <b>일곱 상태를 모두</b> 그리는가.
///
/// <para>
/// [왜 기계가 세야 하는가 — 이 자리를 두 번 오갔다]
/// </para>
///
/// <para>
/// 셸이 <c>#components-reconnect-modal</c> 을 직접 선언하면 프레임워크는 자기
/// 상자를 만들지 않고 <b>그 요소에 클래스만 갈아 끼운다</b>
/// (<c>UserSpecifiedDisplay</c>). 그러면 show · retrying · paused · failed ·
/// resume-failed · rejected · hide <b>일곱</b>을 전부 우리가 그려야 한다.
/// </para>
///
/// <para>
/// 처음에는 넷만 알고 적었다. 빠진 상태가 오면 상자는 <b>아무 글도 없는 빈
/// 껍데기</b>가 되거나, 「거절됨」에서 멈춘 채 다시 붙은 뒤에도 화면을 덮고
/// 남는다. <b>빌드도 화면도 멀쩡하다</b> — 연결을 끊어 봐야 드러난다.
/// </para>
///
/// <para>
/// [되돌아가면 무엇을 잃는가]
/// </para>
///
/// <para>
/// 상자를 지우면 프레임워크의 기본 상자가 돌아오는데, 그것은 거절당한 자리에서
/// 곧바로 <c>location.reload()</c> 를 한다 — 사용자에게는 「다시 붙자마자
/// 화면이 통째로 새로 뜨는 것」이고, 서버에 닿지 못한 입력은 그때 사라진다.
/// 그것을 없애려고 상자를 도로 가져왔으므로, <b>상자가 사라지는 것 자체</b>도
/// 여기서 막는다.
/// </para>
/// </summary>
public sealed class ReconnectDialogTests
{
    /// <summary>프레임워크가 갈아 끼우는 상태 클래스 일곱.</summary>
    private static readonly string[] States =
    [
        "components-reconnect-show",
        "components-reconnect-retrying",
        "components-reconnect-paused",
        "components-reconnect-failed",
        "components-reconnect-resume-failed",
        "components-reconnect-rejected",
        "components-reconnect-hide",
    ];

    /// <summary>상태마다 하나씩 보여 주는 문단(<c>App.razor</c>).</summary>
    private static readonly string[] Paragraphs =
    [
        "jsini-reconnect__trying",
        "jsini-reconnect__countdown",
        "jsini-reconnect__paused",
        "jsini-reconnect__failed",
        "jsini-reconnect__resume-failed",
        "jsini-reconnect__rejected",
    ];

    [Fact]
    public void 상자는_셸이_직접_선언한다()
    {
        Assert.Contains("id=\"components-reconnect-modal\"", App(), StringComparison.Ordinal);

        // 단추와 자동 재시도는 회로 없이 도는 JS 가 맡는다.
        Assert.True(File.Exists(ReconnectScriptPath()), "js/reconnect.js 가 없다");
        Assert.Contains("js/reconnect.js", App(), StringComparison.Ordinal);
    }

    [Fact]
    public void 상태마다_보여_줄_문단이_있다()
    {
        var app = App();
        var missing = Paragraphs.Where(p => !app.Contains(p, StringComparison.Ordinal)).ToArray();

        Assert.True(
            missing.Length == 0,
            "App.razor 의 재연결 상자에 이 문단이 없다 — 그 상태가 오면 빈 상자가 뜬다.\n  "
            + string.Join("\n  ", missing));
    }

    [Fact]
    public void 일곱_상태가_모두_CSS_에_있다()
    {
        var css = File.ReadAllText(AppCssPath());
        var missing = States.Where(s => !css.Contains(s, StringComparison.Ordinal)).ToArray();

        Assert.True(
            missing.Length == 0,
            "app.css 가 이 상태를 모르면 상자가 빈 채 뜨거나 꺼지지 않는다.\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>
    /// 새로고침은 <b>사람이 고를 때만</b> 한다.
    ///
    /// <para>
    /// 이 상자를 만든 이유가 그것이라, 어딘가에서 다시 자동으로 부르면
    /// 기본 상자를 쓰던 때로 되돌아간다. <c>reconnect.js</c> 안의
    /// <c>location.reload()</c> 는 「새로고침」 단추를 받는 자리 하나뿐이다.
    /// </para>
    /// </summary>
    [Fact]
    public void 스스로_새로고침하지_않는다()
    {
        var js = File.ReadAllText(ReconnectScriptPath());

        var reloads = js.Split("location.reload(").Length - 1;

        Assert.True(reloads == 1, $"reconnect.js 의 location.reload() 가 {reloads}곳이다 — 한 곳이어야 한다");
        Assert.Contains("data-reconnect-action", App(), StringComparison.Ordinal);
    }

    private static string App() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "Components", "App.razor"));

    private static string AppCssPath() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css");

    private static string ReconnectScriptPath() => Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "wwwroot", "js", "reconnect.js");

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
