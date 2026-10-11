using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 연결이 끊겼을 때 <b>화면에 아무것도 뜨지 않는가</b>, 그런데도 다시 잇는
/// 손놀림은 살아 있는가 — 하던 일이 서버에 없을 때 <b>묻지 않고 새로 여는가</b>,
/// 그리고 그 손놀림이 향상된 이동 뒤에도 끊기지 않는가.
///
/// <para>
/// [왜 기계가 세야 하는가 — 이 자리를 여러 번 오갔다]
/// </para>
///
/// <para>
/// 셸이 <c>#components-reconnect-modal</c> 을 직접 선언하면 프레임워크는 자기
/// 상자를 만들지 않고 <b>그 요소에 클래스만 갈아 끼운다</b>
/// (<c>UserSpecifiedDisplay</c>). 그래서 <b>비워 두는 것이 지우는 길</b>이다 —
/// 요소째 걷어내면 프레임워크가 제 상자를 끼워 넣어 화면 한가운데에 영문
/// 안내가 뜬다. <b>빌드도 화면도 멀쩡하다.</b> 연결을 끊어 봐야 드러난다.
/// </para>
///
/// <para>
/// [상자 → 띠 → 아무것도 — 2026-10-11]
/// </para>
///
/// <para>
/// 한동안 「사람이 골라야 하는 자리」에서 화면을 덮는 창을 띄웠다. 거절당한
/// 자리에서는 <b>「5초 뒤에 화면을 새로 엽니다」를 세고</b> 새로고침·잠시
/// 멈추기를 고르게 했다. <b>고를 것이 없는 선택이었다</b> — 거절당했다는 것은
/// 하던 일이 서버에 없다는 뜻이라 어느 단추를 눌러도 끝은 새로고침 하나다.
/// </para>
///
/// <para>
/// 그래서 상자를 걷고 위쪽에 작은 띠 하나만 남겼다가, 그것도 걷었다.
/// <b>보여 주는 값이 없는 알림이었다</b> — 회선이 튀는 것은 1~2초 안에 저절로
/// 이어지고, 오래 끊긴 자리는 이 셸이 조용히 다시 잇고, 되돌릴 수 없는 자리는
/// 묻지 않고 새로 연다. 사람이 할 일이 없는데 띠가 뜨는 것 자체가 「사고가
/// 났다」로 읽혀 터널 몇 초마다 하던 일을 멈추게 했다.
/// </para>
///
/// <para>
/// 서버에 닿지도 못한 상태(failed · resume-failed)에서는 여전히 스스로 열지
/// 않는다 — 새로 열어 봐야 브라우저 오류 화면이 뜨고 하던 것만 잃는다.
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

    /// <summary>
    /// 보여 주던 시절의 자취. 하나라도 돌아오면 끊김이 다시 화면에 뜬 것이다.
    ///
    /// <para>
    /// 앞의 넷은 <b>상자</b>(5초를 세던 창), 뒤의 넷은 <b>띠</b>(위쪽의 작은
    /// 알약과 그것을 숨겼다 보였다 하던 단계)의 것이다.
    /// </para>
    /// </summary>
    private static readonly string[] Leftovers =
    [
        "jsini-reconnect__acts",
        "jsini-reconnect__btn",
        "jsini-reconnect__countdown",
        "jsini-reconnect-reload-seconds",
        "jsini-reconnect__band",
        "jsini-reconnect--hush",
        "jsini-reconnect--hint",
        "jsini-reconnect--nudge",
    ];

    /// <summary>
    /// 빈 자리를 <b>셸이 직접 선언한다.</b>
    ///
    /// <para>
    /// 안 보이는데 왜 두는가 — 이것이 없으면 프레임워크가 자기 상자를 만들어
    /// 끼운다. 비워 두는 것이 지우는 길이다.
    /// </para>
    /// </summary>
    [Fact]
    public void 빈_자리를_셸이_직접_선언한다()
    {
        Assert.Contains("id=\"components-reconnect-modal\"", App(), StringComparison.Ordinal);

        // 자동 재시도는 회로 없이 도는 JS 가 맡는다.
        Assert.True(File.Exists(ReconnectScriptPath()), "js/reconnect.js 가 없다");
        Assert.Contains("js/reconnect.js", App(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 끊겨도 <b>화면에 아무것도 안 뜬다.</b>
    ///
    /// <para>
    /// 상자든 띠든 되살리면 마크업·CSS·JS 에 자취가 함께 돌아오므로 그
    /// 이름들을 센다 — 글이나 단추 하나만 되돌아와도 그 뒤를 나머지가 따라온다.
    /// </para>
    /// </summary>
    [Fact]
    public void 끊겨도_화면에_아무것도_안_뜬다()
    {
        var app = App();
        var css = RazorSource.Read(AppCssPath());
        var js = ReconnectScript();

        var back = Leftovers
            .Where(n => app.Contains(n, StringComparison.Ordinal)
                        || css.Contains(n, StringComparison.Ordinal)
                        || js.Contains(n, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            back.Length == 0,
            "걷어낸 표시의 자취가 돌아왔다 — 끊긴 동안 화면은 끊기기 전 그대로여야 한다.\n  "
            + string.Join("\n  ", back));

        // 세는 창이 서던 자리. 초읽기가 돌아오면 창도 함께 돌아온 것이다.
        Assert.DoesNotContain("startReloadCountdown", js, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alertdialog\"", app, StringComparison.Ordinal);

        // 빈 <div> 다. 안에 무엇이 들어오면 그것이 곧 화면에 뜨는 것이다.
        Assert.Contains("<div id=\"components-reconnect-modal\"></div>", app,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 일곱 상태가 <b>모두</b> 꺼져 있다.
    ///
    /// <para>
    /// 프레임워크는 끊김 상태마다 다른 클래스를 갈아 끼운다. 한 상태만
    /// 빠뜨리면 <b>그 상태에서만 빈 칸이 뜬다</b> — 빌드도 화면도 멀쩡해서
    /// 연결을 그 모양으로 끊어 봐야 드러난다.
    /// </para>
    /// </summary>
    [Fact]
    public void 일곱_상태가_모두_꺼져_있다()
    {
        var css = RazorSource.Read(AppCssPath());

        var at = css.IndexOf("#components-reconnect-modal,", StringComparison.Ordinal);
        Assert.True(at >= 0, "끊김 요소를 끄는 규칙이 없다");

        var close = css.IndexOf('}', at);
        var block = css[at..close];

        var missing = States.Where(s => !block.Contains(s, StringComparison.Ordinal)).ToArray();

        Assert.True(
            missing.Length == 0,
            "이 상태가 규칙에서 빠지면 그 상태에서만 빈 칸이 뜬다.\n  "
            + string.Join("\n  ", missing));

        Assert.Contains("display: none", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// 거절당하면 <b>묻지 않고 새로 연다.</b>
    ///
    /// <para>
    /// 하던 일이 서버에 없다는 뜻이라 화면은 이미 죽어 있다. 세는 줄도,
    /// 고르는 단추도 두지 않는다 — 어느 쪽을 눌러도 끝은 새로고침 하나다.
    /// </para>
    /// </summary>
    [Fact]
    public void 거절당하면_묻지_않고_새로_연다()
    {
        var js = ReconnectScript();

        Assert.Contains("location.reload(", js, StringComparison.Ordinal);

        // 여는 자리가 둘이다 — 프레임워크가 알려 준 rejected 와 우리가 손으로
        // 이어 보다 거절당한 자리.
        var calls = js.Split("reloadNow()").Length - 1;
        Assert.True(calls >= 3, $"reloadNow 를 부르는 곳이 모자란다({calls}곳)");

        var at = js.IndexOf("case 'rejected':", StringComparison.Ordinal);
        Assert.True(at >= 0, "rejected 갈래가 없다");

        var body = js[at..js.IndexOf("break;", at, StringComparison.Ordinal)];
        Assert.Contains("reloadNow", body, StringComparison.Ordinal);
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

            Assert.DoesNotContain("reloadNow", body, StringComparison.Ordinal);
            Assert.Contains("startAuto", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 프레임워크가 포기한 뒤를 <b>이어받는다.</b>
    ///
    /// <para>
    /// 프레임워크의 재시도는 6분 남짓에서 멈추는데 서버는 끊긴 회로를 15분
    /// 붙들고 있다. 지하철을 십 분 타고 나오면 <b>서버에는 하던 일이 그대로
    /// 있는데 브라우저가 먼저 포기한</b> 상태가 된다. 보여 주는 띠가 없어진
    /// 뒤로는 <b>이 길이 유일한 구제책</b>이다 — 눌러서 다시 해 볼 자리조차
    /// 화면에 없다.
    /// </para>
    /// </summary>
    [Fact]
    public void 포기한_뒤를_이어받는다()
    {
        var js = ReconnectScript();

        // 주기적으로 한 번씩.
        Assert.Contains("AUTO_RETRY_MS", js, StringComparison.Ordinal);

        // 그리고 가장 잘 붙는 두 순간 — 화면이 돌아올 때와 망이 돌아올 때.
        Assert.Contains("'visibilitychange'", js, StringComparison.Ordinal);
        Assert.Contains("'online'", js, StringComparison.Ordinal);
    }

    /// <summary>
    /// 요소를 <b>붙들어 두지 않는다.</b>
    ///
    /// <para>
    /// 향상된 이동(enhanced navigation)은 문서를 다시 파싱하지 않고
    /// <c>&lt;body&gt;</c> 를 기워 맞추면서 이 <c>&lt;div&gt;</c> 를 통째로
    /// 갈아 끼운다. 기동할 때 잡아 둔 요소에 손을 걸어 두면 그 손놀림이
    /// <b>떨어져 나간 옛 요소</b>에 남는다 — 상태 변화가 한 번도 안 오므로
    /// <b>거절당해도 화면이 새로 열리지 않는다.</b>
    /// </para>
    ///
    /// <para>
    /// 보여 주는 것이 없어진 뒤로는 증상이 더 조용하다. 예전에는 「뜨는데
    /// 눌리지 않는다」로라도 보였는데, 이제는 <b>죽은 화면이 죽은 채로 남는
    /// 것</b>이 전부다. 그래서 기계가 센다.
    /// </para>
    /// </summary>
    [Fact]
    public void 요소를_붙들어_두지_않는다()
    {
        var js = ReconnectScript();

        // 상태 변화 이벤트는 거품이 일지 않아 그 요소에 직접 걸어야 한다 —
        // 그래서 향상된 이동마다 다시 건다.
        Assert.Contains("'enhancedload'", js, StringComparison.Ordinal);
        Assert.Contains("components-reconnect-state-changed", js, StringComparison.Ordinal);

        // 기동할 때 한 번 잡아 두는 옛 방식으로 되돌아가지 않는다.
        Assert.DoesNotContain(
            "const dialog = document.getElementById", js, StringComparison.Ordinal);
    }

    private static string App() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "Components", "App.razor"));

    private static string AppCssPath() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css");

    private static string ReconnectScriptPath() => Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "wwwroot", "js", "reconnect.js");

    private static string ReconnectScript() => RazorSource.Read(ReconnectScriptPath());

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
