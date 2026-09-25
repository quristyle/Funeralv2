using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 연결이 끊겼을 때 뜨는 대화상자가 <b>일곱 상태를 모두</b> 그리고, 거절당한
/// 자리에서 <b>스스로 화면을 새로 여는가</b>, 그리고 그 손놀림이 향상된 이동
/// 뒤에도 살아 있는가.
///
/// <para>
/// [왜 기계가 세야 하는가 — 이 자리를 세 번 오갔다]
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
/// [새로고침은 되돌아왔다 — 2026-09-25]
/// </para>
///
/// <para>
/// 한동안 <b>어디에서도 스스로 새로고침하지 않는 것</b>이 이 상자의 존재
/// 이유였다. 기본 상자가 거절당한 자리에서 곧바로 <c>location.reload()</c> 를
/// 하는 것이 거칠어 보였기 때문이다. 그런데 거절당했다는 것은 <b>하던 일이
/// 서버에 없다</b>는 뜻이고, 그때 화면은 이미 죽어 있다 — 말만 하고 사람이
/// 누르기를 기다리는 것은 죽은 화면 앞에 사람을 세워 두는 것이었다.
/// </para>
///
/// <para>
/// 그래서 <b>거절당한 자리에서만</b> 세고 나서 스스로 연다. 서버에 닿지도
/// 못한 상태(failed · resume-failed)에서는 여전히 스스로 열지 않는다 —
/// 새로 열어 봐야 브라우저 오류 화면이 뜨고 하던 것만 잃는다.
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
    /// 거절당하면 <b>스스로 새로 연다.</b>
    ///
    /// <para>
    /// 세는 줄과 멈추는 단추가 함께 있어야 뜻이 선다 — 세기만 하고 멈출 수
    /// 없으면 적어 둔 것을 옮겨 적을 틈이 없고, 멈출 수만 있고 세지 않으면
    /// 예전처럼 죽은 화면 앞에 사람을 세워 둔다.
    /// </para>
    /// </summary>
    [Fact]
    public void 거절당하면_스스로_새로_연다()
    {
        var js = ReconnectScript();
        var app = App();

        Assert.Contains("location.reload(", js, StringComparison.Ordinal);

        // 세는 것을 시작하는 자리가 둘이다 — 프레임워크가 알려 준 rejected 와
        // 우리가 손으로 이어 보다 거절당한 자리.
        var starts = js.Split("startReloadCountdown()").Length - 1;
        Assert.True(starts >= 3, $"startReloadCountdown 을 부르는 곳이 모자란다({starts}곳)");

        Assert.Contains("jsini-reconnect-reload-seconds", js, StringComparison.Ordinal);
        Assert.Contains("jsini-reconnect-reload-seconds", app, StringComparison.Ordinal);
        Assert.Contains("data-reconnect-action=\"hold\"", app, StringComparison.Ordinal);
    }

    /// <summary>
    /// 서버에 <b>닿지도 못한</b> 상태에서는 스스로 열지 않는다.
    ///
    /// <para>
    /// failed · resume-failed 갈래에서 새로고침을 부르면 서버가 죽어 있는
    /// 동안 브라우저 오류 화면으로 넘어가면서 하던 것이 통째로 사라진다.
    /// 그 갈래가 하는 일은 <c>startAuto</c>(조용히 다시 이어 보기)뿐이어야 한다.
    /// </para>
    /// </summary>
    [Fact]
    public void 닿지_못한_상태에서는_스스로_열지_않는다()
    {
        var js = ReconnectScript();

        foreach (var state in new[] { "case 'failed':", "case 'resume-failed':" })
        {
            var at = js.IndexOf(state, StringComparison.Ordinal);
            Assert.True(at >= 0, $"{state} 갈래가 없다");

            var body = js[at..js.IndexOf("break;", at, StringComparison.Ordinal)];

            Assert.DoesNotContain("startReloadCountdown", body, StringComparison.Ordinal);
            Assert.DoesNotContain("reloadNow", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 상자를 <b>붙들어 두지 않는다.</b>
    ///
    /// <para>
    /// 향상된 이동(enhanced navigation)은 문서를 다시 파싱하지 않고
    /// <c>&lt;body&gt;</c> 를 기워 맞추면서 이 <c>&lt;div&gt;</c> 를 통째로
    /// 갈아 끼운다. 기동할 때 잡아 둔 요소에 손을 걸어 두면 그 손놀림이
    /// <b>떨어져 나간 옛 요소</b>에 남는다 — 그런데 프레임워크는 끊길 때마다
    /// 요소를 새로 찾으므로 <b>상자는 멀쩡히 뜬다.</b>
    /// </para>
    ///
    /// <para>
    /// 그래서 증상이 「상자는 뜨는데 단추가 하나도 안 눌린다」 하나로 보이고,
    /// 새로고침하고 나면 저절로 멀쩡해져서 재현조차 어렵다. 실제로 그렇게
    /// 한 판 통째로 죽어 있었다.
    /// </para>
    /// </summary>
    [Fact]
    public void 상자를_붙들어_두지_않는다()
    {
        var js = ReconnectScript();

        // 누름은 문서에 걸어야 상자가 갈려도 안 끊긴다.
        Assert.Contains("document.addEventListener('click'", js, StringComparison.Ordinal);

        // 상태 변화 이벤트는 거품이 일지 않아 상자에 직접 걸어야 한다 —
        // 그래서 향상된 이동마다 다시 건다.
        Assert.Contains("'enhancedload'", js, StringComparison.Ordinal);

        // 기동할 때 한 번 잡아 두는 옛 방식으로 되돌아가지 않는다.
        Assert.DoesNotContain(
            "const dialog = document.getElementById", js, StringComparison.Ordinal);
    }

    /// <summary>
    /// 상자는 <b>잠금화면보다 위다.</b>
    ///
    /// <para>
    /// 잠긴 화면은 서버가 있어야 풀린다(비밀번호·지문을 서버가 대조한다).
    /// 덮개가 위에 있으면 상자가 비쳐 보이기만 하고 단추는 덮개에 먹혀서
    /// <b>화면을 되찾을 길이 아예 없다.</b> 자리를 비운 사이에 끊기는 것은
    /// 가장 흔한 조합이기도 하다.
    /// </para>
    /// </summary>
    [Fact]
    public void 상자가_잠금화면보다_위다()
    {
        var css = File.ReadAllText(AppCssPath());

        Assert.True(
            ZIndexOf(css, "#components-reconnect-modal.components-reconnect-show") is { } dialog
            && ZIndexOf(css, ".jsini-lock") is { } lockScreen
            && dialog > lockScreen,
            "재연결 상자의 z-index 가 잠금화면(.jsini-lock)보다 높아야 한다.");
    }

    /// <summary>선택자가 든 규칙 덩이에서 <c>z-index</c> 를 꺼낸다.</summary>
    private static int? ZIndexOf(string css, string selector)
    {
        var at = css.IndexOf(selector, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var close = css.IndexOf('}', at);
        if (close < 0)
        {
            return null;
        }

        var match = Regex.Match(css[at..close], @"z-index:\s*(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static string App() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "Components", "App.razor"));

    private static string AppCssPath() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css");

    private static string ReconnectScriptPath() => Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "wwwroot", "js", "reconnect.js");

    private static string ReconnectScript() => File.ReadAllText(ReconnectScriptPath());

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
