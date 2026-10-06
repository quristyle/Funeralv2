using CargoTrustServer.Common;
using CargoTrustServer.Toll;
using Xunit;

namespace CargoTrustServer.Tests;

/// <summary>
/// 심야할인 계산 엔진. 규칙은 <see cref="TollRuleStore.Fallback"/> 과 같은 것을
/// 손으로 만들어 쓴다 — DB 를 타지 않아야 시험이 빠르고, <b>규칙이 바뀌어도
/// 계산이 바뀌지 않는다</b>는 것을 여기서 못 박는다.
/// </summary>
public class NightDiscountEngineTests
{
    private static readonly NightWindow Closed = new(new TimeOnly(21, 0), new TimeOnly(6, 0));
    private static readonly NightWindow Open = new(new TimeOnly(23, 0), new TimeOnly(5, 0));

    private static readonly TollRules Rules = new(
        "TEST",
        Closed, Open,
        [new(0m, 0m), new(20m, 20m), new(50m, 30m), new(80m, 50m)],
        [new(0m, 0m), new(100m, 50m)]);

    private static DateTime At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0);

    // ── 창의 모양 ────────────────────────────────────────────

    [Fact]
    public void 자정을_넘는_창의_길이는_아홉시간이다()
    {
        Assert.Equal(TimeSpan.FromHours(9), Closed.Length);
        Assert.Equal(TimeSpan.FromHours(6), Open.Length);
    }

    // ── 겹침 ─────────────────────────────────────────────────

    [Fact]
    public void 밤에만_달리면_전부_야간이다()
    {
        // 22:00 ~ 02:00 — 창(21~06) 안에 통째로 들어 있다.
        Assert.Equal(240, NightDiscountEngine.OverlapMinutes(At(6, 22), At(7, 2), Closed));
    }

    [Fact]
    public void 낮에만_달리면_야간이_없다()
    {
        Assert.Equal(0, NightDiscountEngine.OverlapMinutes(At(6, 9), At(6, 17), Closed));
    }

    [Fact]
    public void 전날_밤에_열린_창이_새벽까지_이어진다()
    {
        // 새벽 01:00~05:00. 이 창은 **전날** 21시에 열렸다 — 하루 전부터 훑지
        // 않으면 통째로 0 이 되고, 그 틀림은 「할인이 안 되네」로만 보인다.
        Assert.Equal(240, NightDiscountEngine.OverlapMinutes(At(7, 1), At(7, 5), Closed));
    }

    [Fact]
    public void 창의_앞뒤에_걸치면_겹친_만큼만_센다()
    {
        // 20:00 ~ 23:00 — 21시부터가 야간이라 두 시간.
        Assert.Equal(120, NightDiscountEngine.OverlapMinutes(At(6, 20), At(6, 23), Closed));

        // 04:00 ~ 08:00 — 06시까지가 야간이라 두 시간.
        Assert.Equal(120, NightDiscountEngine.OverlapMinutes(At(7, 4), At(7, 8), Closed));
    }

    [Fact]
    public void 여러_날에_걸치면_날마다_센다()
    {
        // 6일 12:00 ~ 8일 12:00 (48시간). 창이 두 번 완전히 들어온다(9시간 × 2).
        Assert.Equal(9 * 60 * 2, NightDiscountEngine.OverlapMinutes(At(6, 12), At(8, 12), Closed));
    }

    [Fact]
    public void 겹친_토막은_겹친_분과_합이_같다()
    {
        var segments = NightDiscountEngine.OverlapSegments(At(6, 20), At(7, 8), Closed);
        var sum = segments.Sum(s => (s.To - s.From).TotalMinutes);
        Assert.Equal(NightDiscountEngine.OverlapMinutes(At(6, 20), At(7, 8), Closed), (int)sum);
    }

    // ── 띠 고르기 ────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(19.99, 0)]
    [InlineData(20, 20)]      // 경계는 「이상」이라 위쪽 띠다
    [InlineData(49.99, 20)]
    [InlineData(50, 30)]
    [InlineData(79.99, 30)]
    [InlineData(80, 50)]      // 가장 자주 다투는 경계
    [InlineData(100, 50)]
    public void 띠의_경계는_이상으로_가른다(decimal ratio, decimal expected)
    {
        Assert.Equal(expected, NightDiscountEngine.BandFor(Rules.ClosedBands, ratio).DiscountPercent);
    }

    // ── 정방향 ───────────────────────────────────────────────

    [Fact]
    public void 폐쇄식_야간_전부면_오십퍼센트다()
    {
        var r = NightDiscountEngine.Evaluate(Rules, SectionType.CLOSED, At(6, 22), At(7, 2));
        Assert.Equal(240, r.TotalMinutes);
        Assert.Equal(240, r.NightMinutes);
        Assert.Equal(100m, r.NightRatio);
        Assert.Equal(50m, r.DiscountPercent);
        Assert.Null(r.NextBandMinRatio);   // 꼭대기라 더 받을 것이 없다
    }

    [Fact]
    public void 폐쇄식_절반쯤이면_삼십퍼센트다()
    {
        // 19:00 ~ 01:00 (6시간) 중 야간은 21:00~01:00 (4시간) → 66.67%
        var r = NightDiscountEngine.Evaluate(Rules, SectionType.CLOSED, At(6, 19), At(7, 1));
        Assert.Equal(360, r.TotalMinutes);
        Assert.Equal(240, r.NightMinutes);
        Assert.Equal(66.67m, r.NightRatio);
        Assert.Equal(30m, r.DiscountPercent);
        Assert.Equal(80m, r.NextBandMinRatio);
        Assert.Equal(50m, r.NextDiscountPercent);
    }

    [Fact]
    public void 낮운행은_할인이_없다()
    {
        var r = NightDiscountEngine.Evaluate(Rules, SectionType.CLOSED, At(6, 9), At(6, 15));
        Assert.Equal(0m, r.NightRatio);
        Assert.Equal(0m, r.DiscountPercent);
    }

    [Fact]
    public void 개방식은_통과시각_한_점으로_본다()
    {
        // 창(23~05) 안 — 진출 시각을 줘도 쓰지 않는다.
        var inside = NightDiscountEngine.Evaluate(Rules, SectionType.OPEN, At(7, 1), At(7, 9));
        Assert.Equal(100m, inside.NightRatio);
        Assert.Equal(50m, inside.DiscountPercent);
        Assert.Equal(0, inside.TotalMinutes);

        // 창 밖(22:00 은 폐쇄식 창에는 들지만 개방식 창에는 안 든다)
        var outside = NightDiscountEngine.Evaluate(Rules, SectionType.OPEN, At(6, 22), null);
        Assert.Equal(0m, outside.NightRatio);
        Assert.Equal(0m, outside.DiscountPercent);
    }

    // ── 역방향 ───────────────────────────────────────────────

    [Fact]
    public void 추천한_시각으로_다시_계산하면_목표를_만족한다()
    {
        // 이 시험이 핵심이다 — 정방향과 역방향이 갈라지면 「추천대로 달렸는데
        // 할인이 안 되는」 일이 생긴다.
        var required = NightDiscountEngine.RequiredRatio(Rules.ClosedBands, 50m);
        Assert.Equal(80m, required);

        var entry = At(6, 20);   // 20:00 진입
        var windows = NightDiscountEngine.Suggest(Closed, entry, anchorIsEntry: true, 180, 720, required!.Value);

        Assert.NotEmpty(windows);
        foreach (var w in windows)
        {
            foreach (var moment in new[] { w.FromKst, w.BestKst, w.ToKst })
            {
                var again = NightDiscountEngine.Evaluate(Rules, SectionType.CLOSED, entry, moment);
                Assert.True(again.DiscountPercent >= 50m,
                    $"추천 {moment:MM-dd HH:mm} 인데 다시 계산하면 {again.DiscountPercent}% (비율 {again.NightRatio}%)");
            }
        }
    }

    [Fact]
    public void 진출이_정해진_경우도_같다()
    {
        var required = NightDiscountEngine.RequiredRatio(Rules.ClosedBands, 30m)!.Value;
        var exit = At(7, 7);     // 07:00 에 도착해야 한다
        var windows = NightDiscountEngine.Suggest(Closed, exit, anchorIsEntry: false, 180, 720, required);

        Assert.NotEmpty(windows);
        foreach (var w in windows)
        {
            var again = NightDiscountEngine.Evaluate(Rules, SectionType.CLOSED, w.BestKst, exit);
            Assert.True(again.DiscountPercent >= 30m);
            // 진출이 붙박이면 추천 시각은 진출보다 **앞**이어야 한다.
            Assert.True(w.BestKst < exit);
        }
    }

    [Fact]
    public void 닿지_못하는_목표는_빈_결과다()
    {
        // 아침 09:00 진입에 세 시간 이상 — 야간창에 닿을 길이 없다.
        var required = NightDiscountEngine.RequiredRatio(Rules.ClosedBands, 50m)!.Value;
        var windows = NightDiscountEngine.Suggest(Closed, At(6, 9), true, 180, 360, required);
        Assert.Empty(windows);

        var best = NightDiscountEngine.BestReachable(Closed, At(6, 9), true, 180, 360);
        Assert.Equal(0m, best.BestRatio);
    }

    [Fact]
    public void 최소_소요시간보다_짧게는_추천하지_않는다()
    {
        // 제한이 없으면 「1분 만에 나가면 100%」가 답이 된다. 분모를 지키는지 본다.
        var required = NightDiscountEngine.RequiredRatio(Rules.ClosedBands, 50m)!.Value;
        var windows = NightDiscountEngine.Suggest(Closed, At(6, 23), true, 300, 600, required);

        Assert.All(windows, w => Assert.True(w.MinDurationMinutes >= 300));
        Assert.All(windows, w => Assert.True(w.MaxDurationMinutes <= 600));
    }

    // ── 다음 띠까지 ──────────────────────────────────────────

    [Fact]
    public void 다음_띠까지_몇_분인지_알려_준다()
    {
        // 19:00 ~ 01:00 → 66.67% (30%). 다음 띠는 80% 다.
        var entry = At(6, 19);
        var exit = At(7, 1);
        var (delayExit, delayEntry) = NightDiscountEngine.StepsToNextBand(Closed, entry, exit, 80m);

        // 진출을 늦추면 야간이 늘어나므로 길이 있다.
        Assert.NotNull(delayExit);
        var later = NightDiscountEngine.Evaluate(Rules, SectionType.CLOSED, entry, exit.AddMinutes(delayExit!.Value));
        Assert.True(later.NightRatio >= 80m);

        // 진입을 늦춰도(낮 부분을 버려도) 비율이 올라간다.
        Assert.NotNull(delayEntry);
        var startLater = NightDiscountEngine.Evaluate(Rules, SectionType.CLOSED, entry.AddMinutes(delayEntry!.Value), exit);
        Assert.True(startLater.NightRatio >= 80m);
    }

    [Fact]
    public void 이미_꼭대기면_더_올릴_곳이_없다()
    {
        var r = NightDiscountEngine.Evaluate(Rules, SectionType.CLOSED, At(6, 22), At(7, 2));
        Assert.Null(r.NextBandMinRatio);
    }

    // ── 개방식 창 ────────────────────────────────────────────

    [Fact]
    public void 다음에_열리는_개방식_창을_찾는다()
    {
        // 저녁 20시 — 다음 창은 그날 23:00~익일 05:00.
        var (from, to) = NightDiscountEngine.NextOpenWindow(Open, At(6, 20));
        Assert.Equal(At(6, 23), from);
        Assert.Equal(At(7, 5), to);

        // 창 안(01시)이면 **지금 열려 있는** 창을 준다.
        var (from2, to2) = NightDiscountEngine.NextOpenWindow(Open, At(7, 1));
        Assert.Equal(At(6, 23), from2);
        Assert.Equal(At(7, 5), to2);
    }
}
