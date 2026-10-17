using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// AI CLI 사용량 및 한도 표기(<see cref="AiUsageText"/>) 검증.
/// 5 단위 사용량 배경색 수식어와 주간 사용량 갱신까지 남은 시간 표기를 검증한다.
/// </summary>
public sealed class AiUsageTextTests
{
    [Theory]
    [InlineData(0, "jsini-badge--usage-0")]
    [InlineData(4.9, "jsini-badge--usage-0")]
    [InlineData(5.0, "jsini-badge--usage-5")]
    [InlineData(7.2, "jsini-badge--usage-5")]
    [InlineData(10.0, "jsini-badge--usage-10")]
    [InlineData(25.0, "jsini-badge--usage-25")]
    [InlineData(50.0, "jsini-badge--usage-50")]
    [InlineData(74.9, "jsini-badge--usage-70")]
    [InlineData(75.0, "jsini-badge--usage-75")]
    [InlineData(89.9, "jsini-badge--usage-85")]
    [InlineData(90.0, "jsini-badge--usage-90")]
    [InlineData(95.0, "jsini-badge--usage-95")]
    [InlineData(99.9, "jsini-badge--usage-95")]
    [InlineData(100.0, "jsini-badge--usage-100")]
    [InlineData(105.0, "jsini-badge--usage-100")]
    public void 사용량_5단위_수치마다_배경색_수식어가_달라진다(decimal pct, string expectedCss)
    {
        var css = AiUsageText.UsageCss(pct);
        Assert.Equal(expectedCss, css);
    }

    [Fact]
    public void 주간_사용량이_있고_갱신시각이_있으면_남은_시간과_분이_표시된다()
    {
        var resetAt = AppTime.UtcNow.AddHours(25).AddMinutes(30);
        var snapshot = new AiUsageSnapshot
        {
            Ok = true,
            RunnerKind = "claude",
            WeekPct = 35.5m,
            WeekResetAt = resetAt,
            ObservedAt = AppTime.UtcNow,
        };

        var badge = AiUsageText.Of([snapshot]);

        Assert.Equal("jsini-badge--usage-35", badge.Css);
        Assert.Contains("주간 <strong class=\"pm-usage-week-val\">35.5%</strong>", badge.Text);
        Assert.Contains("갱신까지", badge.Text);
        Assert.Contains("시간", badge.Text);
        Assert.Contains("분", badge.Text);
        Assert.Contains("남음", badge.Text);
    }

    [Fact]
    public void 갱신시각이_지난_경우_곧_갱신으로_표시된다()
    {
        var resetAt = AppTime.UtcNow.AddMinutes(-5);
        var snapshot = new AiUsageSnapshot
        {
            Ok = true,
            RunnerKind = "claude",
            WeekPct = 80m,
            WeekResetAt = resetAt,
            ObservedAt = AppTime.UtcNow,
        };

        var badge = AiUsageText.Of([snapshot]);

        Assert.Equal("jsini-badge--usage-80", badge.Css);
        Assert.Contains("곧 갱신", badge.Text);
    }

    [Fact]
    public void 오래된_값은_회색_배지를_쓴다()
    {
        var snapshot = new AiUsageSnapshot
        {
            Ok = true,
            RunnerKind = "claude",
            WeekPct = 10m,
            ObservedAt = AppTime.UtcNow.AddHours(-2),
        };

        var badge = AiUsageText.Of([snapshot]);

        Assert.Equal("jsini-badge--off", badge.Css);
    }

    /// <summary>
    /// 갱신까지 남은 시간(<see cref="AiUsageText.Remain"/>).
    /// </summary>
    /// <remarks>
    /// <b>날짜를 세는지가 요점이다.</b> 월간 한도는 갱신이 보름 뒤라, 시간으로만
    /// 적으면 「355시간 남음」이 되어 아무도 못 읽는다.
    /// </remarks>
    [Fact]
    public void 하루가_넘게_남으면_날짜부터_적는다()
    {
        var text = AiUsageText.Remain(AppTime.UtcNow.AddDays(14).AddHours(19).AddMinutes(22).AddSeconds(1));

        Assert.StartsWith("14일 ", text);
        Assert.Contains("19시간", text);
        Assert.EndsWith("남음", text);
    }

    [Fact]
    public void 하루가_안_남으면_시간과_분만_적는다()
    {
        var text = AiUsageText.Remain(AppTime.UtcNow.AddHours(9).AddMinutes(49).AddSeconds(1));

        Assert.DoesNotContain("일", text);
        Assert.Contains("9시간", text);
        Assert.Contains("49분", text);
    }

    /// <summary>0 인 칸은 적지 않는다 — 「2일 0시간 0분」은 읽는 품만 늘린다.</summary>
    [Fact]
    public void 떨어지는_칸은_빼고_적는다()
    {
        var text = AiUsageText.Remain(AppTime.UtcNow.AddDays(2).AddSeconds(1));

        Assert.Equal("2일 남음", text);
    }

    [Fact]
    public void 갱신시각이_지났으면_곧_갱신이다()
    {
        Assert.Equal("곧 갱신", AiUsageText.Remain(AppTime.UtcNow.AddMinutes(-5)));
    }

    /// <summary>
    /// 1분이 안 남았을 때 「0분 남음」이라고 적지 않는다 — 떨어지는 칸을 빼고
    /// 적는 규칙 때문에 그 자리는 글자가 통째로 비어 「 남음」이 된다.
    /// </summary>
    [Fact]
    public void 일분이_안_남으면_일분_미만이다()
    {
        Assert.Equal("1분 미만 남음", AiUsageText.Remain(AppTime.UtcNow.AddSeconds(30)));
    }

    // ── 로그인이 끊긴 상태 ─────────────────────────────────

    private static AiUsageSnapshot Failed(string? error) => new()
    {
        Ok = false,
        RunnerKind = "claude",
        ErrorText = error,
        ObservedAt = AppTime.UtcNow,
    };

    /// <summary>
    /// 출력 형식이 바뀐 것과 로그인이 끊긴 것은 사람이 할 일이 다르다 —
    /// 한 덩이로 「읽지 못함」이라 적으면 늘 파서부터 뒤진다.
    /// </summary>
    [Fact]
    public void 로그인이_끊기면_읽지_못함이_아니라_재로그인_필요다()
    {
        var badge = AiUsageText.Of([Failed($"{AiUsageText.AuthMark} 비용 요약이 돌아왔습니다.")]);

        Assert.Equal(AiUsageText.AuthText, badge.Text);
        Assert.Equal("jsini-badge--err", badge.Css);
    }

    [Fact]
    public void 여느_실패는_그대로_읽지_못함이다()
    {
        var badge = AiUsageText.Of([Failed("출력에서 한도를 찾지 못했습니다.")]);

        Assert.Equal("읽지 못함", badge.Text);
    }

    [Fact]
    public void 닫힌_칸에서도_로그인_필요를_가른다()
    {
        Assert.Equal("로그인필요",
            AiUsageText.Mini([Failed($"{AiUsageText.AuthMark} 끊겼습니다.")]).Text);
        Assert.Equal("읽지못함",
            AiUsageText.Mini([Failed("형식이 바뀌었습니다.")]).Text);
    }

    /// <summary>기계가 보는 표는 사람 눈에 닿지 않는다.</summary>
    [Fact]
    public void 까닭을_적을_때_표는_떼어_낸다()
    {
        var usage = Failed($"{AiUsageText.AuthMark} 다시 로그인하십시오.");

        Assert.Equal("다시 로그인하십시오.", AiUsageText.Why(usage));
        Assert.DoesNotContain(AiUsageText.AuthMark, AiUsageText.Row(usage));
    }

    [Fact]
    public void 표가_없는_까닭은_그대로_둔다()
    {
        Assert.Equal("형식이 바뀌었습니다.", AiUsageText.Why(Failed("형식이 바뀌었습니다.")));
        Assert.Equal(string.Empty, AiUsageText.Why(Failed(null)));
    }

    // ── 로그인 만료일 ───────────────────────────────────────

    private static AiUsageSnapshot WithAuth(DateTime? expiresAt) => new()
    {
        Ok = true,
        RunnerKind = "claude",
        WeekPct = 30m,
        AuthExpiresAt = expiresAt,
        ObservedAt = AppTime.UtcNow,
    };

    [Fact]
    public void 로그인_만료일이_없으면_아무것도_안_적는다()
    {
        Assert.Equal(string.Empty, AiUsageText.AuthRemain(WithAuth(null)));
        Assert.False(AiUsageText.AuthExpiring(WithAuth(null)));
    }

    /// <summary>날짜만 적으면 가까운지 먼지를 머릿속에서 빼야 안다.</summary>
    [Fact]
    public void 로그인_만료일은_남은_기간과_함께_적는다()
    {
        var text = AiUsageText.AuthRemain(WithAuth(AppTime.UtcNow.AddDays(27).AddMinutes(1)));

        Assert.StartsWith("로그인 ", text);
        Assert.Contains("만료 · ", text);
        Assert.Contains("27일", text);
        Assert.EndsWith("남음", text);
    }

    [Fact]
    public void 이미_지난_로그인_만료일은_만료됨으로_적는다()
    {
        Assert.EndsWith("만료됨", AiUsageText.AuthRemain(WithAuth(AppTime.UtcNow.AddDays(-1))));
    }

    [Theory]
    [InlineData(40, false)]
    [InlineData(15, false)]
    [InlineData(13, true)]
    [InlineData(-1, true)]
    public void 두_주_안쪽이면_곧_끊긴다고_본다(int days, bool expected)
    {
        Assert.Equal(expected, AiUsageText.AuthExpiring(WithAuth(AppTime.UtcNow.AddDays(days))));
    }
}
