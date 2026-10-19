using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>떠다니는 단추를 오래 눌렀을 때 뜨는 창(FabHoldMenu)이 조용히 어긋나지 않게 지킨다. JS 가 부르는 메서드 이름·갈래 이름(menu·help)·CSS 클래스·모듈 경로가 브라우저와 글자로 맞잡고 있어,
/// 어긋나도 빌드는 통과하고 예외는 콘솔에만 찍힌 채 「길게 눌러도 아무 일이 없다」가 된다.</summary>
public sealed class FabHoldMenuTests
{
    // ── 브라우저 ↔ 서버가 맞잡은 글자들 ─────────────────────────

    /// <summary>JS 가 부르는 메서드가 실제로 있고 [JSInvokable] 이다 — 이름이나 속성이 어긋나면 화면에는 아무것도 안 뜬다.</summary>
    [Fact]
    public void JS_가_부르는_메서드가_실제로_있다()
    {
        var call = Regex.Match(HoldJs(), @"invokeMethodAsync\('(?<name>[^']+)'");

        Assert.True(call.Success, "`fab-hold.js` 가 .NET 을 부르는 자리를 찾지 못했다.");

        var name = call.Groups["name"].Value;

        Assert.Matches(
            new Regex(@"\[JSInvokable\][^}]*?public\s+Task\s+" + Regex.Escape(name) + @"\(",
                RegexOptions.Singleline),
            MenuCode());
    }

    /// <summary>모듈을 들여오는 경로가 실제 파일을 가리킨다 — 어긋나면 import 404 를 코드비하인드가 삼켜 로그조차 안 남는다.</summary>
    [Fact]
    public void 모듈_경로가_실제_파일을_가리킨다()
    {
        var import = Regex.Match(MenuCode(), @"""\./_content/JSini\.Web\.Components/(?<rel>[^""]+)""");

        Assert.True(import.Success, "`FabHoldMenu` 가 모듈을 들여오는 줄을 찾지 못했다.");

        var path = Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot",
            import.Groups["rel"].Value.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"들여오는 모듈이 없다: {import.Groups["rel"].Value}");
    }

    /// <summary>JS 가 갈래를 가리는 클래스 이름이 셸이 붙이는 것과 같다 — 어긋나면 길게 눌러도 아무 일이 없다(짧게 누르는 것은 그대로 되어 더 헷갈린다).</summary>
    [Theory]
    [InlineData("jsini-shell__fab--menu")]
    [InlineData("jsini-shell__fab--help")]
    public void 갈래를_가리는_클래스가_셸의_것과_같다(string css)
    {
        Assert.Contains(css, HoldJs(), StringComparison.Ordinal);
        Assert.Contains(css, MainLayout(), StringComparison.Ordinal);
    }

    /// <summary>JS 가 보내는 갈래 글자를 코드비하인드가 읽어 낸다 — help 를 못 알아보면 요청 단추를 길게 눌렀을 때 메뉴 단추의 설정이 뜬다.</summary>
    [Fact]
    public void 갈래_글자를_코드가_읽어_낸다()
    {
        Assert.Contains("'help'", HoldJs(), StringComparison.Ordinal);
        Assert.Contains("'menu'", HoldJs(), StringComparison.Ordinal);
        Assert.Contains(@"""help""", MenuCode(), StringComparison.Ordinal);
    }

    /// <summary>셸이 이 창을 <b>그린다.</b> 안 그리면 손짓을 거는 자리도 없다.</summary>
    [Fact]
    public void 셸이_이_창을_그린다() =>
        Assert.Contains("<FabHoldMenu />", MainLayout(), StringComparison.Ordinal);

    // ── 뒤따라오는 누름 ─────────────────────────────────────────

    /// <summary>창을 띄운 뒤의 click 은 잡아내기 단계에서 멈춘다 — 그 단추의 원래 일이 동시에 일어나지 않게 한다. Blazor 처리기는 버블 단계라 버블에서 막으면 늦다.</summary>
    [Fact]
    public void 뒤따라오는_누름을_잡아내기에서_멈춘다()
    {
        var handler = Listener("click");

        Assert.Contains("stopPropagation", handler, StringComparison.Ordinal);
        Assert.Contains("preventDefault", handler, StringComparison.Ordinal);

        // 세 번째 인자(`true`)가 잡아내기 단계라는 뜻이다. 빼면 버블이고,
        // 버블에서는 Blazor 가 이미 받은 뒤다.
        Assert.Matches(new Regex(@"\}, true\);"), handler);
    }

    /// <summary>손을 뗄 때 따라오는 가짜 누름을 막는다 — 과녁이 단추가 아니라 덮개라 DevExpress 가 바깥 누름으로 읽어 창을 닫는다. touchend 를 취소해야 하며, 처리기가 passive 가 아니어야 취소가 먹는다.</summary>
    [Fact]
    public void 손을_뗄_때의_가짜_누름을_막는다()
    {
        var handler = Listener("touchend");

        Assert.Contains("preventDefault", handler, StringComparison.Ordinal);
        Assert.Contains("passive: false", handler, StringComparison.Ordinal);
    }

    /// <summary>브라우저의 바로가기 차림표를 막는다 — 안드로이드 크롬은 길게 누른 것을 contextmenu 로도 쏘므로 막지 않으면 우리 창 위로 겹쳐 뜬다.</summary>
    [Fact]
    public void 바로가기_차림표를_막는다() =>
        Assert.Contains("preventDefault", Listener("contextmenu"), StringComparison.Ordinal);

    // ── 값은 한 곳에만 담긴다 ───────────────────────────────────

    /// <summary>담는 일은 PortalBoot 에 맡긴다 — 여기서 localStorage 를 직접 쓰면 환경설정과 열쇠가 둘로 갈라져 설정이 안 먹는 것처럼 보인다.</summary>
    [Fact]
    public void 값을_PortalBoot_한_곳으로만_담는다()
    {
        var code = MenuCode();

        Assert.DoesNotContain("localStorage", code, StringComparison.Ordinal);

        foreach (var setter in new[]
                 {
                     "Boot.SetFabPositionAsync",
                     "Boot.SetHelpDeskFabPositionAsync",
                     "Boot.SetFabHiddenAsync",
                     "Boot.SetHelpDeskFabHiddenAsync",
                 })
        {
            Assert.Contains(setter, code, StringComparison.Ordinal);
        }
    }

    /// <summary>환경설정에서 고친 것도 여기로 온다. ReadAsync 는 회로마다 한 번 읽은 것을 돌려주므로, 알림을 안 들으면 옛 자리에 표시가 붙는다.</summary>
    [Fact]
    public void 바뀜_알림을_듣는다()
    {
        var code = MenuCode();

        Assert.Contains("Boot.FabPositionChanged +=", code, StringComparison.Ordinal);
        Assert.Contains("Boot.HelpDeskFabPositionChanged +=", code, StringComparison.Ordinal);

        // 떼는 것도 함께. 레이아웃은 **업무를 옮길 때마다 새로 생기므로**
        // 떼지 않으면 버려진 창들이 `PortalBoot` 에 계속 매달린다.
        Assert.Contains("Boot.FabPositionChanged -=", code, StringComparison.Ordinal);
        Assert.Contains("Boot.HelpDeskFabPositionChanged -=", code, StringComparison.Ordinal);
    }

    /// <summary>버려진 창을 부르지 않는다 — JS 가 마지막에 건 상대를 들고 있어야 한다. 레이아웃은 업무를 옮길 때마다 새로 생기는데 ES 모듈은 그대로라, 상대를 안 바꾸면 호출이 조용히 떨어진다.</summary>
    [Fact]
    public void 마지막에_건_상대를_부른다()
    {
        var js = HoldJs();

        var attach = js.IndexOf("export function attachFabHold", StringComparison.Ordinal);
        Assert.True(attach >= 0, "`attachFabHold` 를 찾지 못했다.");

        var guard = js.IndexOf("if (attached) return;", attach, StringComparison.Ordinal);
        Assert.True(guard >= 0, "두 번 걸지 않게 막는 줄을 찾지 못했다.");

        // 상대를 바꿔 두는 줄이 **그 빠져나가기보다 앞**이라야 한다.
        var host = js.IndexOf("host = dotnet;", attach, StringComparison.Ordinal);

        Assert.True(host >= 0 && host < guard,
            "건 상대를 바꾸는 줄이 `if (attached) return;` 보다 앞에 없다.");
    }

    /// <summary>고친 값은 읽어 둔 한 벌에도 반영된다 — ReadAsync 는 회로마다 한 번 읽은 것을 돌려줘서, 고친 뒤 새로 생긴 부품은 옛 값을 받고 바뀜 알림은 이미 지나가 들을 수 없다.</summary>
    [Theory]
    [InlineData("FabPositionChanged")]
    [InlineData("FabHiddenChanged")]
    [InlineData("HelpDeskFabPositionChanged")]
    [InlineData("HelpDeskFabHiddenChanged")]
    [InlineData("BottomNavHiddenChanged")]
    [InlineData("BottomNavItemsChanged")]
    [InlineData("ToastPositionChanged")]
    [InlineData("ZoomUnlockedChanged")]
    public void 고친_값이_읽어_둔_한_벌에도_반영된다(string raised)
    {
        var boot = PortalBootSource();
        var at = boot.IndexOf(raised + "?.Invoke(", StringComparison.Ordinal);

        Assert.True(at >= 0, $"`PortalBoot` 에서 `{raised}` 를 알리는 자리를 찾지 못했다.");

        // 알리기 **바로 앞**에서 기억해 둔다. 뒤에 두면 알림을 듣고 곧바로
        // 다시 읽는 부품이 여전히 옛 값을 받는다.
        var before = boot[Math.Max(0, at - 200)..at];

        Assert.Contains("Remember(", before, StringComparison.Ordinal);
    }

    /// <summary>환경설정 화면도 바뀜 알림을 듣는다 — 휴대폰에서는 그 화면을 보는 동안에도 단추가 떠 있어서, 옛 값을 말하면 설정이 안 먹는 것으로 보인다.</summary>
    [Theory]
    [InlineData("FabPositionChanged")]
    [InlineData("FabHiddenChanged")]
    [InlineData("HelpDeskFabPositionChanged")]
    [InlineData("HelpDeskFabHiddenChanged")]
    public void 환경설정_화면도_바뀜_알림을_듣는다(string signal)
    {
        var page = EnvironmentPageCode();

        Assert.Contains($"Boot.{signal} +=", page, StringComparison.Ordinal);
        Assert.Contains($"Boot.{signal} -=", page, StringComparison.Ordinal);
    }

    // ── 되돌리는 길 ─────────────────────────────────────────────

    /// <summary>감출 때 되돌리는 길을 말한다 — 감춘 단추는 그려지지 않아 길게 누를 수도 없으므로, 말해 주지 않으면 영영 사라진 것이 된다.</summary>
    [Fact]
    public void 감출_때_되돌리는_길을_말한다()
    {
        var hide = Member("private async Task HideAsync()");

        Assert.Contains("Toasts.Show", hide, StringComparison.Ordinal);
        Assert.Contains("환경설정", hide, StringComparison.Ordinal);

        // 메뉴 단추 쪽은 **메뉴를 여는 다른 길**까지 말한다. 감추면 휴대폰에서
        // 메뉴를 여는 자리가 왼쪽 위 로고 하나뿐이다(`MainLayout.OnBrandClick`).
        Assert.Contains("로고", hide, StringComparison.Ordinal);
    }

    /// <summary>환경설정으로 가는 줄은 주소를 글자로 박지 않는다 — @page 는 모듈이 소유해 옮겨 갈 수 있고, 사본이 어긋나면 누른 사람이 「준비 중」을 본다.</summary>
    [Fact]
    public void 환경설정_주소를_글자로_박지_않는다()
    {
        var code = MenuCode();

        Assert.DoesNotContain("/funeral/setting/environment", code, StringComparison.Ordinal);
        Assert.Contains("PortalHome.Resolve", code, StringComparison.Ordinal);
    }

    /// <summary>바깥 누름으로 닫지 않는다 — 켜면 뜨자마자 닫힌다. 가짜 누름은 touchend 취소로 막아도 DxPopup 이 window 의 잡아내기 단계에서 바깥 누름을 보므로, 되돌리기 쉬운 한 줄이다.</summary>
    [Fact]
    public void 바깥_누름으로_닫지_않는다() =>
        // 적지 않는 것이 곧 끄는 것이다(`CommPopup` 의 기본값). 머리말에서는
        // 그 이름을 이야기하므로 **값을 주는 자리**만 본다.
        Assert.DoesNotMatch(new Regex(@"CloseOnOutsideClick\s*="), Markup());

    // ── 생김새 ──────────────────────────────────────────────────

    /// <summary>
    /// 오래 누르는 동안 <b>받고 있다는 표시</b>가 있다.
    /// </summary>
    /// <summary>오래 누르는 동안 받고 있다는 표시가 있다 — 0.5초 동안 아무것도 안 뜨면 먹통으로 읽혀 손을 떼 버린다.</summary>
    [Fact]
    public void 누르고_있는_동안의_표시가_있다()
    {
        Assert.Contains("is-held", HoldJs(), StringComparison.Ordinal);
        Assert.Matches(new Regex(@"\.jsini-shell__fab\.is-held\s*\{"), AppCss());
    }

    /// <summary>
    /// 오래 눌러도 <b>브라우저가 끼어들지 않는다.</b>
    /// </summary>
    /// <summary>오래 눌러도 브라우저가 끼어들지 않는다 — 아이폰의 말풍선이 먼저 걸리면 우리 창은 뜨지도 못한다.</summary>
    [Fact]
    public void 아이폰_말풍선과_글자_고르기를_끈다()
    {
        var rule = Rule(@"\.jsini-shell__fab\s*\{");

        Assert.Contains("-webkit-touch-callout: none", rule, StringComparison.Ordinal);
        Assert.Contains("user-select: none", rule, StringComparison.Ordinal);
    }

    /// <summary>
    /// 네 귀퉁이 모두 <b>점이 그 귀퉁이에</b> 찍힌다.
    /// </summary>
    /// <summary>네 귀퉁이 모두 점이 그 귀퉁이에 찍힌다 — 하나만 빠지면 고르개 둘이 같은 그림이 된다.</summary>
    [Theory]
    [InlineData("top-left")]
    [InlineData("top-right")]
    [InlineData("bottom-left")]
    [InlineData("bottom-right")]
    public void 네_귀퉁이_모두_점의_자리가_있다(string corner) =>
        Assert.Contains(
            $".jsini-fabhold__corner--{corner} .jsini-fabhold__dot",
            AppCss(),
            StringComparison.Ordinal);

    // ── 읽는 자리들 ─────────────────────────────────────────────

    /// <summary><c>document.addEventListener('이름', …)</c> 한 벌을 통째로 뽑는다.</summary>
    private static string Listener(string name)
    {
        var js = HoldJs();
        var start = js.IndexOf($"document.addEventListener('{name}'", StringComparison.Ordinal);

        Assert.True(start >= 0, $"`fab-hold.js` 에 `{name}` 처리기가 없다.");

        // 다음 처리기 전까지를 몸통으로 본다 — 중괄호를 세지 않는다(HelpDeskFabTests.Member 와 같은 수법).
        var next = js.IndexOf("document.addEventListener(", start + 1, StringComparison.Ordinal);
        return next > 0 ? js[start..next] : js[start..];
    }

    /// <summary>코드비하인드의 멤버 하나. 다음 빈 줄까지를 몸통으로 본다.</summary>
    private static string Member(string declaration)
    {
        var source = MenuCode();
        var start = source.IndexOf(declaration, StringComparison.Ordinal);

        Assert.True(start >= 0, $"`FabHoldMenu` 에 `{declaration}` 이 없다.");

        var next = Regex.Match(source[start..], @"\n\n    ///");
        return next.Success && next.Index > 0 ? source[start..(start + next.Index)] : source[start..];
    }

    /// <summary>app.css 규칙 하나의 선언 부분.</summary>
    private static string Rule(string selector)
    {
        var match = Regex.Match(AppCss(), selector + @"(?<body>[^}]*)\}");

        Assert.True(match.Success, $"app.css 에서 `{selector}` 규칙을 찾지 못했다.");
        return match.Groups["body"].Value;
    }

    private static string EnvironmentPageCode() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Funeral", "Components", "Pages",
        "EnvironmentSettingPage.razor.cs"));

    private static string PortalBootSource() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "PortalBoot.cs"));

    private static string HoldJs() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "js", "fab-hold.js"));

    private static string MenuCode() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "FabHoldMenu.razor.cs"));

    private static string Markup() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "FabHoldMenu.razor"));

    private static string MainLayout() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor"));

    private static string AppCss() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));

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
