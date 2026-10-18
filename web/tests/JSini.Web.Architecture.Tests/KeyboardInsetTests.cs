using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 휴대폰 키보드가 공통 팝업의 바닥을 덮지 않게 막는 한 겹
/// (<c>keyboard-inset.js</c> + app.css 의 「키보드가 올라오면」 구역)을 지킨다.
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 여기서 끊기는 자리는 <b>전부 빌드를 통과한다.</b> 브라우저와 CSS 가
/// <b>글자로</b> 맞잡고 있기 때문이다 — 변수 이름(<c>--jsini-vvh</c>) ·
/// 클래스 이름(<c>jsini-kb-open</c>) · 스크립트 경로. 어느 하나가 어긋나도
/// 화면은 멀쩡하고, <b>휴대폰으로 그 창에 글을 적어 보는 사람이 나올 때까지</b>
/// 아무도 모른다. 그때 보이는 것은 「저장 단추가 안 보인다」 하나뿐이다.
/// </para>
/// </summary>
public sealed class KeyboardInsetTests
{
    // ── 실리는가 ────────────────────────────────────────────────

    /// <summary>공통 <c>&lt;head&gt;</c> 가 이 스크립트를 <b>싣는다.</b></summary>
    /// <remarks>
    /// 창마다 모듈로 붙이지 않고 여기 한 번 거는 것이 이 설계의 전부다.
    /// 빠지면 변수가 영영 안 서고 CSS 는 되돌림 값(<c>100dvh</c>)으로 조용히
    /// 지금까지처럼 동작한다 — 고장 난 티가 안 난다.
    /// </remarks>
    [Fact]
    public void 공통_머리가_스크립트를_싣는다()
    {
        Assert.Contains(
            "_content/JSini.Web.Components/keyboard-inset.js",
            Head(),
            StringComparison.Ordinal);

        Assert.True(File.Exists(ScriptPath()), $"스크립트가 없다: {ScriptPath()}");
    }

    // ── 브라우저 ↔ CSS 가 맞잡은 글자들 ─────────────────────────

    /// <summary>JS 가 내려 주는 이름을 CSS 가 <b>그대로</b> 읽는다.</summary>
    [Theory]
    [InlineData("--jsini-vvh")]
    [InlineData("--jsini-vv-top")]
    [InlineData("jsini-kb-open")]
    public void JS_가_내려_주는_이름을_CSS_가_읽는다(string name)
    {
        Assert.Contains(name, Script(), StringComparison.Ordinal);
        Assert.Contains(name, Css(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 변수를 읽는 자리마다 <b>되돌림 값</b>이 있다.
    /// </summary>
    /// <remarks>
    /// <c>visualViewport</c> 가 없는 브라우저와 스크립트가 아직 안 실린 순간에는
    /// 변수가 비어 있다. 되돌림 값을 빼면 그때 <c>height</c> 가 통째로 무효가
    /// 되어 <b>창이 내용만큼만 커진다</b> — 꽉 채움이 조용히 풀린다.
    /// </remarks>
    [Fact]
    public void 변수를_읽는_자리마다_되돌림_값이_있다()
    {
        foreach (Match use in Regex.Matches(Css(), @"var\(--jsini-vvh[^)]*\)"))
        {
            Assert.Equal("var(--jsini-vvh, 100dvh)", use.Value);
        }

        foreach (Match use in Regex.Matches(Css(), @"var\(--jsini-vv-top[^)]*\)"))
        {
            Assert.Equal("var(--jsini-vv-top, 0px)", use.Value);
        }
    }

    /// <summary>
    /// 치수를 바꾸는 규칙은 <b>키보드가 올라와 있을 때만</b> 돈다.
    /// </summary>
    /// <remarks>
    /// 늘 걸어 두면 주소창이 접히고 펴지는 것(56px 안팎)과 손가락 확대까지
    /// 창 크기를 흔든다. 고치려던 것은 키보드 하나다.
    /// </remarks>
    [Fact]
    public void 치수를_바꾸는_규칙은_키보드가_올라왔을_때만_돈다()
    {
        var rules = Regex.Matches(Css(), @"(?<sel>[^{}]*)\{(?<body>[^{}]*)\}");
        var seen = 0;

        foreach (Match rule in rules)
        {
            if (!rule.Groups["body"].Value.Contains("--jsini-vvh", StringComparison.Ordinal))
            {
                continue;
            }

            seen++;

            Assert.Contains(
                "jsini-kb-open",
                rule.Groups["sel"].Value,
                StringComparison.Ordinal);
        }

        Assert.True(seen >= 3, $"키보드 규칙을 {seen}개만 찾았다 — 구역이 통째로 사라졌는지 본다.");
    }

    /// <summary>
    /// 꽉 채운 창을 담는 <b>판부터</b> 줄인다.
    /// </summary>
    /// <remarks>
    /// 판(<c>dxbl-modal-root</c>)은 <c>position: fixed; height: 100%</c> 이고
    /// 창을 <b>가운데 세운다.</b> 그것을 그대로 두고 창만 줄이면 남은 틈이
    /// 위아래로 반씩 생겨 <b>아래 절반은 여전히 키보드 밑</b>이다.
    /// </remarks>
    [Fact]
    public void 창을_담는_판부터_줄인다()
    {
        Assert.Matches(
            new Regex(@"jsini-kb-open[^{]*\.dxbl-modal-root:has\(>\s*\.dxbl-popup\.jsini-popup\)"),
            Css());

        // iOS 는 키보드를 띄우며 쪽 전체를 밀어 올린다. `top` 이 그 어긋남이다.
        Assert.Matches(
            new Regex(@"\.dxbl-modal-root:has[^{]*\{[^}]*top:\s*var\(--jsini-vv-top, 0px\)"),
            Css());
    }

    /// <summary>
    /// CSS 가 겨누는 클래스를 <b>CommPopup 이 실제로 붙인다.</b>
    /// </summary>
    [Fact]
    public void 겨누는_클래스를_CommPopup_이_붙인다()
    {
        Assert.Contains("jsini-popup--full-mobile", Popup(), StringComparison.Ordinal);
        Assert.Contains("jsini-popup--full-mobile", Css(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 모바일 화면에서 키보드가 올라오면 셸도 보이는 높이로 줄어든다.
    /// </summary>
    [Fact]
    public void 키보드가_올라오면_셸도_줄어든다()
    {
        Assert.Matches(
            new Regex(@"html\.jsini-kb-open\s+\.jsini-shell[^{]*\{[^}]*height:\s*var\(--jsini-vvh,\s*100dvh\)"),
            Css());
    }

    // ── 키보드와 주소창을 가른다 ────────────────────────────────

    /// <summary>
    /// 문턱이 <b>접히는 주소창보다 높다.</b>
    /// </summary>
    /// <remarks>
    /// 안드로이드 크롬의 주소창은 56px 안팎이다. 문턱이 그보다 낮으면 그냥
    /// 굴리기만 해도 창이 들썩인다. 키보드는 어떤 기기에서도 200px 을 넘는다.
    /// </remarks>
    [Fact]
    public void 문턱이_주소창보다_높다()
    {
        var threshold = Regex.Match(Script(), @"THRESHOLD\s*=\s*(?<px>\d+)");

        Assert.True(threshold.Success, "`THRESHOLD` 를 찾지 못했다.");

        var px = int.Parse(threshold.Groups["px"].Value);

        Assert.InRange(px, 100, 200);
    }

    /// <summary>
    /// 확대 배율을 <b>되곱해</b> 견준다.
    /// </summary>
    /// <remarks>
    /// 두 배로 키우면 보이는 높이가 절반이 된다. 그대로 견주면 확대한 사람에게는
    /// <b>키보드가 늘 올라와 있는 것</b>으로 보인다.
    /// </remarks>
    [Fact]
    public void 확대_배율을_되곱해_견준다() =>
        Assert.Contains("vv.scale", Script(), StringComparison.Ordinal);

    /// <summary>
    /// iOS 가 쪽을 밀어 올리는 것(<c>scroll</c>)도 듣는다.
    /// </summary>
    /// <remarks>
    /// 그때는 높이가 그대로고 <c>offsetTop</c> 만 바뀐다. 안 들으면 창이
    /// 화면 위로 반쯤 나간 채 남는다.
    /// </remarks>
    [Fact]
    public void 보이는_뷰포트의_굴림도_듣는다()
    {
        Assert.Contains("vv.addEventListener('resize'", Script(), StringComparison.Ordinal);
        Assert.Contains("vv.addEventListener('scroll'", Script(), StringComparison.Ordinal);
    }

    // ── 읽을 거리 ───────────────────────────────────────────────

    private static string Head() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "JSiniHead.razor"));

    private static string ScriptPath() => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "keyboard-inset.js");

    private static string Script() => File.ReadAllText(ScriptPath());

    private static string Css() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));

    private static string Popup() => File.ReadAllText(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "CommPopup.razor.cs"));

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
