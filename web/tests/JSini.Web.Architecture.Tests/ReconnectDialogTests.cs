using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 연결이 끊겼을 때 뜨는 <b>띠</b>가 일곱 상태를 모두 알고, 화면을 덮지 않고,
/// 하던 일이 서버에 없을 때 <b>묻지 않고 새로 여는가</b>, 그리고 그 손놀림이
/// 향상된 이동 뒤에도 살아 있는가.
///
/// <para>
/// [왜 기계가 세야 하는가 — 이 자리를 여러 번 오갔다]
/// </para>
///
/// <para>
/// 셸이 <c>#components-reconnect-modal</c> 을 직접 선언하면 프레임워크는 자기
/// 상자를 만들지 않고 <b>그 요소에 클래스만 갈아 끼운다</b>
/// (<c>UserSpecifiedDisplay</c>). 그러면 show · retrying · paused · failed ·
/// resume-failed · rejected · hide <b>일곱</b>을 전부 우리가 그려야 한다.
/// 하나라도 빠지면 그 상태에서 띠가 안 뜨거나 안 꺼진다 — <b>빌드도 화면도
/// 멀쩡하다.</b> 연결을 끊어 봐야 드러난다.
/// </para>
///
/// <para>
/// [대화상자를 걷어냈다 — 2026-10-11]
/// </para>
///
/// <para>
/// 한동안 「사람이 골라야 하는 자리」에서 화면을 덮는 창을 띄웠다. 거절당한
/// 자리에서는 <b>「5초 뒤에 화면을 새로 엽니다」를 세고</b> 새로고침·잠시
/// 멈추기를 고르게 했다.
/// </para>
///
/// <para>
/// <b>고를 것이 없는 선택이었다.</b> 거절당했다는 것은 하던 일이 서버에
/// 없다는 뜻이라 어느 단추를 눌러도 끝은 새로고침 하나다. 그 창이 한 일은
/// 죽은 화면 앞에서 다섯을 세는 것뿐이었다. 지금은 <b>세지 않고 그 자리에서
/// 연다</b> — 묻는 창이 없다.
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

    /// <summary>창이 있던 시절의 자취. 하나라도 돌아오면 창도 함께 돌아온 것이다.</summary>
    private static readonly string[] DialogLeftovers =
    [
        "jsini-reconnect__acts",
        "jsini-reconnect__btn",
        "jsini-reconnect__spinner",
        "jsini-reconnect__trying",
        "jsini-reconnect__countdown",
        "jsini-reconnect__reloading",
        "jsini-reconnect-reload-seconds",
        "jsini-reconnect--held",
    ];

    [Fact]
    public void 띠는_셸이_직접_선언한다()
    {
        Assert.Contains("id=\"components-reconnect-modal\"", App(), StringComparison.Ordinal);

        // 누름과 자동 재시도는 회로 없이 도는 JS 가 맡는다.
        Assert.True(File.Exists(ReconnectScriptPath()), "js/reconnect.js 가 없다");
        Assert.Contains("js/reconnect.js", App(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 화면을 덮는 <b>대화상자가 없다.</b>
    ///
    /// <para>
    /// 끊긴 동안 보이는 것은 띠 하나뿐이다. 상자를 되살리면 마크업과 CSS 에
    /// 자취가 함께 돌아오므로 그 이름들을 센다 — 글이나 단추 하나만 되돌아와도
    /// 「5초를 세는 창」이 그 뒤를 따라온다.
    /// </para>
    /// </summary>
    [Fact]
    public void 화면을_덮는_창이_없다()
    {
        var app = App();
        var css = RazorSource.Read(AppCssPath());
        var js = ReconnectScript();

        var back = DialogLeftovers
            .Where(n => app.Contains(n, StringComparison.Ordinal)
                        || css.Contains(n, StringComparison.Ordinal)
                        || js.Contains(n, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            back.Length == 0,
            "걷어낸 대화상자의 자취가 돌아왔다 — 끊긴 동안 보이는 것은 띠 하나여야 한다.\n  "
            + string.Join("\n  ", back));

        // 세는 창이 서던 자리. 초읽기가 돌아오면 창도 함께 돌아온 것이다.
        Assert.DoesNotContain("startReloadCountdown", js, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alertdialog\"", app, StringComparison.Ordinal);
    }

    [Fact]
    public void 일곱_상태가_모두_CSS_에_있다()
    {
        var css = RazorSource.Read(AppCssPath());
        var missing = States.Where(s => !css.Contains(s, StringComparison.Ordinal)).ToArray();

        Assert.True(
            missing.Length == 0,
            "app.css 가 이 상태를 모르면 띠가 안 뜨거나 안 꺼진다.\n  "
            + string.Join("\n  ", missing));
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
    /// 요소를 <b>붙들어 두지 않는다.</b>
    ///
    /// <para>
    /// 향상된 이동(enhanced navigation)은 문서를 다시 파싱하지 않고
    /// <c>&lt;body&gt;</c> 를 기워 맞추면서 이 <c>&lt;div&gt;</c> 를 통째로
    /// 갈아 끼운다. 기동할 때 잡아 둔 요소에 손을 걸어 두면 그 손놀림이
    /// <b>떨어져 나간 옛 요소</b>에 남는다 — 그런데 프레임워크는 끊길 때마다
    /// 요소를 새로 찾으므로 <b>표시는 멀쩡히 뜬다.</b>
    /// </para>
    ///
    /// <para>
    /// 그래서 증상이 「뜨는데 눌리지 않는다」 하나로 보이고, 새로고침하고 나면
    /// 저절로 멀쩡해져서 재현조차 어렵다. 실제로 그렇게 한 판 통째로 죽어 있었다.
    /// </para>
    /// </summary>
    [Fact]
    public void 요소를_붙들어_두지_않는다()
    {
        var js = ReconnectScript();

        // 누름은 문서에 걸어야 요소가 갈려도 안 끊긴다.
        Assert.Contains("document.addEventListener('click'", js, StringComparison.Ordinal);

        // 상태 변화 이벤트는 거품이 일지 않아 그 요소에 직접 걸어야 한다 —
        // 그래서 향상된 이동마다 다시 건다.
        Assert.Contains("'enhancedload'", js, StringComparison.Ordinal);

        // 기동할 때 한 번 잡아 두는 옛 방식으로 되돌아가지 않는다.
        Assert.DoesNotContain(
            "const dialog = document.getElementById", js, StringComparison.Ordinal);
    }

    /// <summary>
    /// 끊긴 동안 <b>화면을 덮지 않는다</b> — 숨죽임 · 귀띔.
    ///
    /// <para>
    /// 한동안 조용한 구간이 <b>0.9초짜리 투명 구간 하나</b>였다. 회선이 한 번
    /// 튀는 것은 그 안에 끝나지만 <b>터널은 0.9초가 아니다</b> — 지하 구간
    /// 하나가 수십 초이고 승강기·주차장도 몇 초씩 끊긴다. 그때마다 화면이
    /// 통째로 어두워지고 상자가 떴다. 서버는 멀쩡하고 곧 저절로 이어지는데도
    /// 사람에게는 매번 사고로 보였다.
    /// </para>
    ///
    /// <para>
    /// 지금은 둘뿐이다 — 숨죽임(2초, 아무것도 안 보인다) · 귀띔(작은 띠 하나,
    /// 화면을 안 덮는다). <b>그 위는 없다.</b> 띠를 누르면 기다리지 않고 그
    /// 자리에서 한 번 더 이어 본다.
    /// </para>
    /// </summary>
    [Fact]
    public void 조용한_구간이_단계로_나뉜다()
    {
        var js = ReconnectScript();
        var css = RazorSource.Read(AppCssPath());
        var app = App();

        foreach (var cls in new[] { "jsini-reconnect--hush", "jsini-reconnect--hint" })
        {
            Assert.Contains(cls, js, StringComparison.Ordinal);
            Assert.Contains(cls, css, StringComparison.Ordinal);
        }

        // 숨죽임은 **시간이 정하고**, 귀띔은 상태가 정한다.
        Assert.Contains("HUSH_MS", js, StringComparison.Ordinal);

        // 띠와 그것을 눌러 다시 이어 보는 길.
        Assert.Contains("jsini-reconnect__band", app, StringComparison.Ordinal);
        Assert.Contains("data-reconnect-action=\"retry\"", app, StringComparison.Ordinal);

        // 0.9초짜리 옛 구간으로 되돌아가지 않는다.
        Assert.DoesNotContain("jsini-reconnect--quiet", js, StringComparison.Ordinal);
        Assert.DoesNotContain("#components-reconnect-modal.jsini-reconnect--quiet", css,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 조용한 동안 <b>누름을 삼킨다.</b>
    ///
    /// <para>
    /// 덮개를 걷었으므로 단추가 그대로 보이고 눌리기까지 한다 — 그런데 회로가
    /// 없어 아무 데도 닿지 않는다. <b>저장을 누르고 저장된 줄 아는 것</b>이
    /// 이 바꿈에서 가장 위험한 자리다. 잡는 단계(capture)에서 끊고 띠를 흔들어
    /// 그 자리에서 알린다.
    /// </para>
    /// </summary>
    [Fact]
    public void 조용한_동안_누름을_삼킨다()
    {
        var js = ReconnectScript();

        // **잡는 단계**여야 한다. 거품 단계에 걸면 화면의 처리기가 먼저 돈다.
        Assert.Contains("document.addEventListener('click', swallow, true)", js,
            StringComparison.Ordinal);
        Assert.Contains("document.addEventListener('submit', swallow, true)", js,
            StringComparison.Ordinal);

        var at = js.IndexOf("function swallow(", StringComparison.Ordinal);
        Assert.True(at >= 0, "swallow 가 없다");

        var body = js[at..(at + 900)];

        // 띠는 통과시킨다 — 그쪽은 회로 없이 도는 우리 손놀림이다.
        Assert.Contains("DIALOG_ID", body, StringComparison.Ordinal);
        Assert.Contains("preventDefault", body, StringComparison.Ordinal);

        // 삼켰으면 **삼켰다고 말한다.**
        Assert.Contains("nudge()", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// 귀띔 띠는 <b>화면을 덮지 않는다.</b>
    ///
    /// <para>
    /// 이 한 줄이 「터널을 지나는 동안 읽던 것을 계속 읽는다」의 전부다.
    /// 바탕을 칠하거나 누름을 막으면 작은 띠만 뜨는 모양새일 뿐 하던 일은
    /// 그대로 끊긴다. <b>끊긴 모든 상태에서 그렇다</b> — 창을 걷어낸 뒤로는
    /// 사람이 골라야 하는 자리에서도 덮지 않는다.
    /// </para>
    /// </summary>
    [Fact]
    public void 귀띔은_화면을_덮지_않는다()
    {
        var css = RazorSource.Read(AppCssPath());

        var at = css.IndexOf("#components-reconnect-modal.components-reconnect-show,",
            StringComparison.Ordinal);
        Assert.True(at >= 0, "끊긴 동안의 규칙이 없다");

        var block = css[at..css.IndexOf('}', at)];

        Assert.Contains("background: transparent", block, StringComparison.Ordinal);
        Assert.Contains("pointer-events: none", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// 띠는 <b>잠금화면보다 위다.</b>
    ///
    /// <para>
    /// 잠긴 화면은 서버가 있어야 풀린다(비밀번호·지문을 서버가 대조한다).
    /// 덮개가 위에 있으면 띠가 비쳐 보이기만 하고 누름은 덮개에 먹혀서
    /// <b>화면을 되찾을 길이 아예 없다.</b> 자리를 비운 사이에 끊기는 것은
    /// 가장 흔한 조합이기도 하다.
    /// </para>
    /// </summary>
    [Fact]
    public void 띠가_잠금화면보다_위다()
    {
        var css = RazorSource.Read(AppCssPath());

        Assert.True(
            ZIndexOf(css, "#components-reconnect-modal.components-reconnect-show") is { } band
            && ZIndexOf(css, ".jsini-lock") is { } lockScreen
            && band > lockScreen,
            "재연결 띠의 z-index 가 잠금화면(.jsini-lock)보다 높아야 한다.");
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
