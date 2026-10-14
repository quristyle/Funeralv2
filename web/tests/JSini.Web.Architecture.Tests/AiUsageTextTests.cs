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
}
