using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 휴대폰 확대 잠금(<c>js/zoom.js</c>)이 <b>조용히 풀리지 않게</b> 지킨다.
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 이 기능은 <b>실패해도 화면이 멀쩡하다.</b> 빌드도 통과하고 예외도 안 나고
/// 로그도 안 남는다 — 두 손가락으로 화면이 커질 뿐이다. 그 증상은 휴대폰을
/// 손에 들고 있는 사람만 볼 수 있어서, 여기서 잡지 않으면 다음에 누가
/// 알아챌지 알 수 없다.
/// </para>
///
/// <para>
/// 그리고 어긋날 자리가 <b>파일 셋에 흩어져</b> 있다 — 저장소 열쇠는
/// <c>zoom.js</c> 와 <c>PortalBoot</c> 양쪽에 글자로 적혀 있고, 스크립트를
/// 어떻게 싣는지는 <c>App.razor</c> 가 쥔다. 한쪽만 고치는 날을 위한 검사다.
/// </para>
/// </summary>
public sealed class ZoomLockTests
{
    /// <summary>
    /// 저장소 열쇠가 <b>양쪽에서 같은 글자</b>인가.
    ///
    /// <para>
    /// C# 이 쓰고 JS 가 읽는다 — 부르는 쪽과 읽는 쪽이 다른 언어라 컴파일러가
    /// 대조해 줄 수 없다. 어긋나면 <b>환경설정 스위치가 아무 일도 안 하는
    /// 것으로 보인다</b>(껐는데 새로고침하면 다시 잠겨 있다).
    /// </para>
    /// </summary>
    [Fact]
    public void 확대_잠금_열쇠가_양쪽에서_같다()
    {
        var inCs = Regex.Match(PortalBootSource(),
            @"ZoomUnlockedKey\s*=\s*""(?<key>[^""]+)""");

        var inJs = Regex.Match(ZoomScript(),
            @"KEY\s*=\s*'(?<key>[^']+)'");

        Assert.True(inCs.Success, "PortalBoot 에 ZoomUnlockedKey 가 없다.");
        Assert.True(inJs.Success, "zoom.js 에 KEY 가 없다.");
        Assert.Equal(inCs.Groups["key"].Value, inJs.Groups["key"].Value);
    }

    /// <summary>
    /// 첫 그림 전에 도는가 — <c>defer</c> 도 <c>async</c> 도 붙지 않는다.
    /// </summary>
    /// <remarks>
    /// 붙이면 문서를 다 읽은 뒤에 돌아서, <b>그 사이에 걸린 확대가 그대로
    /// 남는다.</b> 게다가 로그인 화면은 회로가 없는 정적 SSR 이라 C# 쪽에서
    /// 뒤늦게 바로잡을 길도 없다.
    /// </remarks>
    [Fact]
    public void 확대_잠금_스크립트는_미루지_않는다()
    {
        var tag = ZoomScriptTag();

        Assert.DoesNotContain("defer", tag, StringComparison.Ordinal);
        Assert.DoesNotContain("async", tag, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>&lt;meta name="viewport"&gt;</c> <b>뒤</b>에 있는가.
    /// </summary>
    /// <remarks>
    /// 그 태그를 고쳐 쓰는 것이 세 겹 중 하나다. 앞에 두면 스크립트가 아직
    /// 없는 태그를 찾게 되고, 그때는 <b>스스로 만들어 붙이므로 태그가 둘이
    /// 된다</b> — 브라우저가 어느 것을 볼지는 기대지 않는 편이 낫다.
    /// </remarks>
    [Fact]
    public void 확대_잠금_스크립트는_viewport_뒤에_선다()
    {
        var app = App();

        var meta = app.IndexOf("name=\"viewport\"", StringComparison.Ordinal);
        var script = app.IndexOf("js/zoom.js", StringComparison.Ordinal);

        Assert.True(meta >= 0, "App.razor 에 viewport 메타가 없다.");
        Assert.True(script >= 0, "App.razor 가 js/zoom.js 를 싣지 않는다.");
        Assert.True(meta < script, "zoom.js 는 viewport 메타 뒤에 실어야 한다.");
    }

    /// <summary>
    /// 막는 겹이 <b>셋 다</b> 남아 있는가.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 하나만 남아도 「막는 코드가 있다」로 읽히는데 <b>브라우저마다 듣는 것이
    /// 다르다.</b> 크롬은 접근성을 이유로 <c>user-scalable=no</c> 를 무시하므로
    /// 메타만 남으면 안드로이드에서 아무 일도 안 일어나고, 반대로 옛 iOS 는
    /// <c>touch-action</c> 을 안 봐서 그것만 남으면 사파리가 뚫린다.
    /// </para>
    /// <para>
    /// <c>touch-action</c> 은 <b><c>none</c> 이면 안 된다</b> — 그러면 스크롤까지
    /// 죽어서 화면이 굳은 것처럼 보인다.
    /// </para>
    /// </remarks>
    [Fact]
    public void 확대를_세_겹으로_막는다()
    {
        var js = ZoomScript();

        Assert.Contains("user-scalable=no", js, StringComparison.Ordinal);
        Assert.Contains("'pan-x pan-y'", js, StringComparison.Ordinal);
        Assert.Contains("gesturestart", js, StringComparison.Ordinal);

        // 사파리 제스처는 passive 로 붙이면 preventDefault 가 무시된다.
        Assert.Contains("passive: false", js, StringComparison.Ordinal);
    }

    /// <summary>
    /// 넓은 화면은 건드리지 않는가 — <b>굵은 손가락이면서 좁을 때</b>만 건다.
    /// </summary>
    /// <remarks>
    /// 둘 중 하나만 보면 안 된다. 폭만 보면 창을 좁게 줄여 둔 데스크톱이 걸려
    /// Ctrl+＋ 를 쓰던 사람의 길이 막히고, 손가락만 보면 터치 되는 큰 모니터가
    /// 걸린다. 경계값은 <c>MainLayout</c> 의 태블릿 경계(1023px)와 같아야 한다.
    /// </remarks>
    [Fact]
    public void 잠금은_좁고_만지는_화면에서만_건다()
    {
        var js = ZoomScript();

        Assert.Contains("pointer: coarse", js, StringComparison.Ordinal);
        Assert.Contains("max-width: 1023px", js, StringComparison.Ordinal);
    }

    /// <summary>
    /// 스위치를 누르면 <b>새로고침 없이</b> 지금 화면에 반영되는가.
    /// </summary>
    /// <remarks>
    /// <c>zoom.js</c> 는 문서가 열릴 때 한 번 읽으므로, 저장만 하고
    /// <c>jsiniZoom.lock</c> 을 안 부르면 사람 눈에는 <b>스위치가 고장 난
    /// 것</b>으로 보인다 — 껐는데 화면은 그대로 안 커진다.
    /// </remarks>
    [Fact]
    public void 스위치는_지금_화면에도_바른다()
    {
        Assert.Matches(
            @"SetZoomUnlockedAsync[\s\S]*?jsiniZoom\.lock",
            PortalBootSource());
    }

    /// <summary>
    /// 기본이 <b>잠금</b>인가 — 환경설정 스위치가 저장값을 뒤집어 보인다.
    /// </summary>
    /// <remarks>
    /// 저장되는 것은 「풀었나」이고 스위치가 말하는 것은 「잠글까」다. 뒤집기가
    /// 빠지면 <b>스위치가 거꾸로 서서</b>, 켜 두었는데 확대가 되고 꺼 두었는데
    /// 안 된다. 화면과 저장이 따로 보여 눈으로 잡기 어려운 종류다.
    /// </remarks>
    [Fact]
    public void 환경설정_스위치는_잠금을_말한다()
    {
        Assert.Contains("Checked=\"@(!_zoomUnlocked)\"", EnvironmentSettingPage(), StringComparison.Ordinal);
    }

    private static string App() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "Components", "App.razor"));

    /// <summary><c>zoom.js</c> 를 싣는 <c>&lt;script&gt;</c> 태그 한 줄.</summary>
    private static string ZoomScriptTag()
    {
        var match = Regex.Match(App(), @"<script[^>]*js/zoom\.js[^>]*>");

        Assert.True(match.Success, "App.razor 에 zoom.js <script> 가 없다.");
        return match.Value;
    }

    private static string ZoomScript() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shell", "JSini.Web.Shell", "wwwroot", "js", "zoom.js"));

    private static string PortalBootSource() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "PortalBoot.cs"));

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
