using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 예보 추이 <b>카드 넘김</b>의 배관을 지킨다 — C# 과 JS 를 잇는 이름,
/// 카드가 놓이는 차례, 붙는 자리를 만드는 css.
///
/// <para>
/// 셋 다 <b>틀려도 아무 일이 일어나지 않는</b> 자리다. 이름이 어긋나면 예외가
/// 브라우저 콘솔에만 찍힌 채 단추가 카드를 안 밀고, 차례가 어긋나면 JS 가
/// 돌려주는 번호가 엉뚱한 단추에 불을 켜고, css 가 빠지면 카드가 아무 데나
/// 멈춰 선 채 그냥 구르는 목록이 된다. <b>셋 중 무엇이 깨져도 화면은
/// 그려진다.</b>
/// </para>
/// </summary>
public sealed class WeatherTrendDeckTests
{
    private const string Page = "WeatherDashboard.razor";

    /// <summary>
    /// 화면이 부르는 이름이 <c>weather-trend.js</c> 에 실제로 있는가.
    ///
    /// <para>
    /// 화면은 JS 호출 실패를 <b>전부 삼킨다</b>(말풍선 판정도, 지켜보기도,
    /// 밀기도 — 그 하나 때문에 화면을 세우지 않는다는 뜻이다). 그래서 이름이
    /// 하나 없어져도 증상은 「단추를 눌러도 카드가 안 넘어간다」뿐이다.
    /// </para>
    /// </summary>
    [Fact]
    public void 화면이_부르는_이름이_weather_trend_js_에_다_있다()
    {
        var exported = Regex.Matches(Js(), @"export\s+function\s+(?<name>\w+)\s*\(")
            .Cast<Match>()
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        // `_module.InvokeVoidAsync("watchDeck", …)` · `InvokeAsync<bool>("isTouchOnly")`
        var called = Regex.Matches(Razor(), @"Invoke(?:Void)?Async(?:<[^>]+>)?\(\s*""(?<fn>[A-Za-z_]\w*)""")
            .Cast<Match>()
            .Select(m => m.Groups["fn"].Value)
            // 모듈을 들여오는 호출(`InvokeAsync<IJSObjectReference>("import", …)`)은
            // 브라우저 것이지 이 모듈이 내보내는 이름이 아니다.
            .Where(fn => fn != "import")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(called);

        var missing = called.Where(fn => !exported.Contains(fn)).ToList();

        Assert.True(missing.Count == 0,
            "weather-trend.js 가 내보내지 않는 이름을 부른다. 예외는 콘솔에만 "
            + "찍히고 화면은 그대로 그려진다.\n" + string.Join('\n', missing));
    }

    /// <summary>
    /// <b>카드 차례와 단추 차례가 같아야 한다.</b> JS 는 「왼쪽에서 몇 번째
    /// 카드가 보인다」를 <b>번호</b>로 돌려주고 화면은 그 번호로
    /// <c>Slides</c> 를 집는다 — 둘이 어긋나면 강수 카드를 보고 있는데 바람
    /// 단추에 불이 켜진다.
    /// </summary>
    [Fact]
    public void 카드_차례와_단추_차례가_같다()
    {
        var razor = Razor();

        var slides = Regex.Match(razor, @"Slides\s*=\s*\[(?<body>[^\]]*)\]");
        Assert.True(slides.Success, $"{Page} 에 Slides 목록이 없다.");

        var order = Regex.Matches(slides.Groups["body"].Value, @"TrendMetric\.(?<m>\w+)")
            .Cast<Match>()
            .Select(m => m.Groups["m"].Value)
            .ToList();

        var buttons = Regex.Matches(razor, @"ShowMetric\(TrendMetric\.(?<m>\w+)\)")
            .Cast<Match>()
            .Select(m => m.Groups["m"].Value)
            .ToList();

        Assert.Equal(4, order.Count);
        Assert.Equal(order, buttons);
    }

    /// <summary>
    /// 카드를 세는 표시(<c>data-slide</c>)가 화면과 JS 양쪽에 있는가.
    /// JS 는 이 표시가 붙은 것만 카드로 세므로, 화면에서 빠지면
    /// <b>카드가 0개</b>가 되어 단추가 아무 데도 못 민다.
    /// </summary>
    [Fact]
    public void 카드는_data_slide_로_센다()
    {
        Assert.Contains("data-slide=", Razor(), StringComparison.Ordinal);
        Assert.Contains("[data-slide]", Js(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 붙는 자리를 만드는 css 가 살아 있는가.
    ///
    /// <para>
    /// 이것이 빠지면 카드가 손을 뗀 자리에 그대로 멈춘다 — 반쯤 걸친 카드 둘이
    /// 보이는 상태이고, JS 가 「가장 가까운 카드」로 세는 번호도 그만큼 헐거워
    /// 진다. <b>오류는 나지 않는다.</b>
    /// </para>
    /// </summary>
    [Fact]
    public void 카드는_제자리에_붙는다()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot(),
            "src", "Apps", "JSini.Web.LifeEnv", "wwwroot", "lifeenv.css"));

        Assert.Contains("scroll-snap-type: x mandatory", css, StringComparison.Ordinal);
        Assert.Contains("scroll-snap-align: start", css, StringComparison.Ordinal);

        // 끝 카드에서 더 민 손짓이 쪽 바깥으로 새면 뒤로가기가 된다.
        Assert.Contains("overscroll-behavior-x: contain", css, StringComparison.Ordinal);
    }

    private static string Razor() => File.ReadAllText(Path.Combine(WebRoot(),
        "src", "Apps", "JSini.Web.LifeEnv", "Components", "Pages", Page));

    private static string Js() => File.ReadAllText(Path.Combine(WebRoot(),
        "src", "Apps", "JSini.Web.LifeEnv", "wwwroot", "js", "weather-trend.js"));

    private static string WebRoot()
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
