using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 떠다니는 단추를 <b>오래 눌렀을 때</b> 뜨는 창(<c>FabHoldMenu</c>)이 조용히
/// 어긋나지 않게 지킨다.
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 이 기능이 끊기는 자리가 <b>전부 빌드를 통과한다.</b> 브라우저와 서버가
/// <b>글자로</b> 맞잡고 있기 때문이다 — JS 가 부르는 메서드 이름, JS 가
/// 보내는 갈래 이름(<c>menu</c>·<c>help</c>), 그 갈래를 읽어 내는 CSS 클래스,
/// 모듈 파일의 경로. 어느 하나가 어긋나도 <b>예외는 브라우저 콘솔에만</b>
/// 찍히고 화면은 멀쩡하다. 사람 눈에 보이는 것은 「길게 눌러도 아무 일이
/// 없다」 하나뿐이다(<c>JsIdentifierTests</c> 와 같은 갈래의 함정이다).
/// </para>
/// </summary>
public sealed class FabHoldMenuTests
{
    // ── 브라우저 ↔ 서버가 맞잡은 글자들 ─────────────────────────

    /// <summary>
    /// JS 가 부르는 메서드가 <b>실제로 있고</b> <c>[JSInvokable]</c> 이다.
    /// </summary>
    /// <remarks>
    /// 이름을 바꾸면 <c>Could not find '…' method</c> 가 브라우저 콘솔에만
    /// 찍힌다. 속성을 빼면 <c>…is not marked with [JSInvokable]</c> 이고,
    /// 역시 화면에는 아무것도 안 뜬다.
    /// </remarks>
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

    /// <summary>
    /// 모듈을 들여오는 경로가 <b>실제 파일</b>을 가리킨다.
    /// </summary>
    /// <remarks>
    /// 파일을 옮기거나 이름을 바꾸면 <c>import</c> 가 404 로 떨어지는데,
    /// 그 예외는 코드비하인드가 <b>삼키고 있다</b>(회로가 닫힌 뒤의 호출을
    /// 조용히 넘기려고 한 묶음으로 잡는다). 그래서 로그조차 안 남는다.
    /// </remarks>
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

    /// <summary>
    /// JS 가 갈래를 가리는 <b>클래스 이름이 셸이 붙이는 것과 같다.</b>
    /// </summary>
    /// <remarks>
    /// 어긋나면 <c>kindOf</c> 가 <c>null</c> 을 돌려주고 <b>길게 눌러도
    /// 아무 일도 일어나지 않는다</b> — 그런데 짧게 누르는 것은 그대로 되므로
    /// 「단추는 멀쩡한데 길게 누르기만 안 된다」가 된다.
    /// </remarks>
    [Theory]
    [InlineData("jsini-shell__fab--menu")]
    [InlineData("jsini-shell__fab--help")]
    public void 갈래를_가리는_클래스가_셸의_것과_같다(string css)
    {
        Assert.Contains(css, HoldJs(), StringComparison.Ordinal);
        Assert.Contains(css, MainLayout(), StringComparison.Ordinal);
    }

    /// <summary>
    /// JS 가 보내는 갈래 글자를 <b>코드비하인드가 읽어 낸다.</b>
    /// </summary>
    /// <remarks>
    /// <c>help</c> 를 못 알아보면 요청 등록 단추를 길게 눌렀을 때 <b>메뉴 단추의
    /// 설정이 뜬다</b> — 창은 멀쩡히 뜨므로 「엉뚱한 것이 고쳐진다」로만 보인다.
    /// </remarks>
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

    /// <summary>
    /// 창을 띄운 뒤의 <c>click</c> 을 <b>잡아내기 단계에서</b> 멈춘다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 손가락을 떼면 브라우저가 <c>click</c> 을 쏘고, 그것은 <b>그 단추의
    /// 원래 일</b>이다(메뉴 여닫기 · 요청 등록으로 가기). 안 막으면 설정 창이
    /// 뜨면서 <b>동시에 화면이 넘어간다.</b>
    /// </para>
    /// <para>
    /// Blazor 의 처리기는 버블 단계에 있으므로 <b>잡아내기(<c>true</c>)에서
    /// <c>stopPropagation</c></b> 해야 닿지 않는다. 버블에서 막으면 이미 늦다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 손을 뗄 때 따라오는 <b>가짜 누름</b>을 못 나오게 막는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 터치를 마우스로 옮긴 가짜 이벤트(<c>mousedown</c>·<c>mouseup</c>·
    /// <c>click</c>)의 <b>과녁은 단추가 아니라 그 위를 덮은 창의
    /// 덮개</b>(<c>dxbl-modal-root</c>)다. DevExpress 는 그것을 「바깥을
    /// 눌렀다」로 읽고 <b>방금 띄운 창을 그 자리에서 닫는다</b> — 실제로
    /// 그랬다(손을 떼고 7ms 뒤에 사라졌다).
    /// </para>
    /// <para>
    /// 과녁이 단추가 아니므로 아래 <c>click</c> 삼키기로는 걸리지 않는다.
    /// <b><c>touchend</c> 를 취소해</b> 가짜 이벤트가 아예 안 생기게 하는
    /// 수밖에 없고, 취소하려면 처리기가 <b><c>passive</c> 가 아니어야</b>
    /// 한다 — 그 한 가지가 빠지면 브라우저가 <c>preventDefault</c> 를
    /// <b>조용히 무시한다</b>(콘솔 경고 한 줄이 전부다).
    /// </para>
    /// </remarks>
    [Fact]
    public void 손을_뗄_때의_가짜_누름을_막는다()
    {
        var handler = Listener("touchend");

        Assert.Contains("preventDefault", handler, StringComparison.Ordinal);
        Assert.Contains("passive: false", handler, StringComparison.Ordinal);
    }

    /// <summary>
    /// 브라우저의 바로가기 차림표를 막는다.
    /// </summary>
    /// <remarks>
    /// 안드로이드 크롬은 길게 누른 것을 <c>contextmenu</c> 로도 쏜다. 막지
    /// 않으면 우리 창 위로 「새 탭에서 열기」가 겹쳐 뜬다.
    /// </remarks>
    [Fact]
    public void 바로가기_차림표를_막는다() =>
        Assert.Contains("preventDefault", Listener("contextmenu"), StringComparison.Ordinal);

    // ── 값은 한 곳에만 담긴다 ───────────────────────────────────

    /// <summary>
    /// 담는 일을 <b><c>PortalBoot</c> 에 맡긴다</b> — 제 손으로 저장소를
    /// 건드리지 않는다.
    /// </summary>
    /// <remarks>
    /// 여기서 <c>localStorage</c> 를 직접 쓰면 환경설정 화면과 <b>열쇠가 둘로
    /// 갈라진다.</b> 그러면 길게 눌러 옮긴 자리가 환경설정에는 안 보이고,
    /// 사람 눈에는 <b>설정이 안 먹는 것</b>으로 읽힌다. 알림(<c>…Changed</c>)도
    /// 그 길로만 나가므로 화면이 그 자리에서 안 바뀐다.
    /// </remarks>
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

    /// <summary>
    /// 환경설정에서 고친 것도 <b>여기로 온다.</b>
    /// </summary>
    /// <remarks>
    /// <see cref="PortalBoot.ReadAsync"/> 는 회로마다 <b>한 번</b> 읽어 둔 것을
    /// 돌려주는 자리라(몇 번을 불러도 왕복은 한 번이다) 창을 열 때마다 불러도
    /// <b>처음 그 값</b>이다. 알림을 안 들으면 환경설정에서 옮긴 뒤에 길게
    /// 눌렀을 때 <b>옛 자리에 표시가 붙는다.</b>
    /// </remarks>
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

    /// <summary>
    /// 버려진 창을 부르지 않는다 — JS 가 <b>마지막에 건 상대</b>를 들고 있다.
    /// </summary>
    /// <remarks>
    /// 셸의 레이아웃은 업무를 옮길 때마다 새로 생기는데 ES 모듈은 한 번
    /// 실리면 그대로다. 상대를 안 바꾸면 버려진 창을 계속 부르게 되고, 그
    /// 호출은 <c>There is no tracked object with id…</c> 로 조용히 떨어진다 —
    /// <b>업무를 한 번 옮긴 뒤부터</b> 길게 눌러도 아무 일이 없다.
    /// </remarks>
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

    /// <summary>
    /// 고친 값이 <b>읽어 둔 한 벌에도 반영된다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PortalBoot.ReadAsync</c> 는 회로마다 <b>한 번</b> 읽어 둔 것을
    /// 돌려준다. 고친 뒤에 <b>새로 생긴</b> 부품이 그것을 읽으면 고치기 전
    /// 값을 받고, 바뀜 알림은 그때 이미 지나간 뒤라 들을 수도 없다.
    /// </para>
    /// <para>
    /// 실제로 그랬다 — 길게 눌러 자리를 옮긴 다음 <b>업무를 옮기면</b>
    /// (셸의 레이아웃이 새로 생긴다) 단추가 옛 귀퉁이로 되돌아갔고,
    /// 환경설정 화면의 고르개도 옛 자리를 가리켰다. <b>저장은 멀쩡히
    /// 됐으므로 새로고침하면 나아서</b> 더 헷갈렸다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 환경설정 화면도 <b>바뀜 알림을 듣는다.</b>
    /// </summary>
    /// <remarks>
    /// 휴대폰에서는 그 화면을 보는 <b>동안에도</b> 떠다니는 단추가 떠 있다.
    /// 바로 옆에서 길게 눌러 자리를 옮겼는데 아래 고르개가 옛 값을 말하면,
    /// 사람 눈에는 <b>설정이 안 먹는 것</b>으로 보인다 —
    /// <c>PortalBoot.ReadAsync</c> 는 화면이 그려질 때 한 번만 읽는다.
    /// </remarks>
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

    /// <summary>
    /// 감출 때 <b>되돌리는 길을 말한다.</b>
    /// </summary>
    /// <remarks>
    /// 감춘 단추는 그려지지 않으므로 <b>길게 누를 수도 없다</b> — 이 창으로
    /// 돌아올 길이 없다. 말해 주지 않으면 「감췄더니 영영 사라졌다」가 된다.
    /// </remarks>
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

    /// <summary>
    /// 환경설정으로 가는 줄이 <b>주소를 글자로 박지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// <c>@page</c> 는 모듈이 소유한 값이라 옮겨 갈 수 있고, 그때 사본이
    /// 조용히 어긋나면 누른 사람이 「준비 중」을 본다
    /// (<see cref="PortalHome.SettingsRouteKey"/> 머리말).
    /// </remarks>
    [Fact]
    public void 환경설정_주소를_글자로_박지_않는다()
    {
        var code = MenuCode();

        Assert.DoesNotContain("/funeral/setting/environment", code, StringComparison.Ordinal);
        Assert.Contains("PortalHome.Resolve", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// 바깥 누름으로 <b>닫지 않는다</b> — 켜면 <b>뜨자마자 닫힌다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 손을 떼며 오는 가짜 누름의 과녁이 <b>창의 덮개</b>라, DevExpress 가
    /// 그것을 「바깥을 눌렀다」로 읽고 방금 띄운 창을 닫는다. 가짜 누름
    /// 자체는 <c>touchend</c> 를 취소해 막았는데 <b>그래도 닫혔다</b> —
    /// DxPopup 은 바깥 누름을 <c>window</c> 의 잡아내기 단계에서 보고 있어서
    /// (문서보다 먼저다) 우리가 끼어들 자리가 없다.
    /// </para>
    /// <para>
    /// 되돌리기 쉬운 한 줄이고, <b>되돌려도 빌드와 나머지 시험은 전부
    /// 통과한다.</b>
    /// </para>
    /// </remarks>
    [Fact]
    public void 바깥_누름으로_닫지_않는다() =>
        // 적지 않는 것이 곧 끄는 것이다(`CommPopup` 의 기본값). 머리말에서는
        // 그 이름을 이야기하므로 **값을 주는 자리**만 본다.
        Assert.DoesNotMatch(new Regex(@"CloseOnOutsideClick\s*="), Markup());

    // ── 생김새 ──────────────────────────────────────────────────

    /// <summary>
    /// 오래 누르는 동안 <b>받고 있다는 표시</b>가 있다.
    /// </summary>
    /// <remarks>
    /// 길게 누르기는 0.5초 동안 아무 일도 안 일어나는 손짓이다. 표시가 없으면
    /// 그 사이가 「먹통」으로 읽혀 <b>손을 떼 버린다</b> — 그러면 기능이 있는
    /// 줄도 모른 채 「안 된다」가 된다.
    /// </remarks>
    [Fact]
    public void 누르고_있는_동안의_표시가_있다()
    {
        Assert.Contains("is-held", HoldJs(), StringComparison.Ordinal);
        Assert.Matches(new Regex(@"\.jsini-shell__fab\.is-held\s*\{"), AppCss());
    }

    /// <summary>
    /// 오래 눌러도 <b>브라우저가 끼어들지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// 아이폰은 길게 누른 자리에 말풍선(「복사 · 공유」)을 띄우고 글자를
    /// 고른다. 그것이 먼저 걸리면 우리 창은 뜨지도 못한다.
    /// </remarks>
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
    /// <remarks>
    /// 하나만 빠지면 그 네모의 점이 왼쪽 위에 몰려(<c>position: absolute</c> 의
    /// 기본 자리) <b>고르개 둘이 같은 그림</b>이 된다 — 글자를 읽기 전에는
    /// 어디를 고르는지 알 수 없다.
    /// </remarks>
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

        // 다음 처리기 전까지를 몸통으로 본다 — 중괄호를 세지 않는다
        // (`HelpDeskFabTests.Member` 와 같은 수법).
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
