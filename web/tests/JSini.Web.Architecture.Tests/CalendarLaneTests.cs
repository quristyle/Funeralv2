using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 일정표 월 보기의 <b>막대 배치</b>.
///
/// <para>
/// [왜 테스트로 못 박나 — 틀려도 아무 소리가 안 난다]
/// </para>
///
/// <para>
/// 이 계산이 틀리면 예외가 나지 않는다. 막대가 서로 겹쳐 그려지거나, 한 줄
/// 아래로 밀리거나, 주를 넘는 일이 두 동강 난 것처럼 보일 뿐이다. 빌드도
/// 통과하고 화면도 열린다 — 사람이 달력을 들여다보기 전에는 아무도 모른다.
/// </para>
///
/// <para>
/// 그런데 이것은 <b>순수한 계산</b>이다. 날짜 몇 개를 주면 자리 몇 개가 나온다.
/// 눈으로 확인할 일을 기계에 넘길 수 있는 드문 자리라 넘긴다.
/// </para>
/// </summary>
public sealed class CalendarLaneTests
{
    /// <summary>이 주의 첫날. 2026-09-06 은 일요일이라 9/6 ~ 9/12 가 한 주다.</summary>
    private static readonly DateOnly Sunday = new(2026, 9, 6);

    private static (DateOnly, DateOnly) Span(int startDay, int endDay) =>
        (new DateOnly(2026, 9, startDay), new DateOnly(2026, 9, endDay));

    [Fact]
    public void 하루짜리는_한_칸을_차지한다()
    {
        var slots = CalendarLanes.Pack([Span(8, 8)], Sunday);

        var slot = Assert.Single(slots);

        // 9/6 이 0칸이므로 9/8 은 2칸이다.
        Assert.Equal(2, slot.Col);
        Assert.Equal(1, slot.Span);
        Assert.Equal(0, slot.Lane);
        Assert.False(slot.ClipStart);
        Assert.False(slot.ClipEnd);
    }

    [Fact]
    public void 여러_날에_걸친_것은_칸을_가로지른다()
    {
        var slots = CalendarLanes.Pack([Span(7, 10)], Sunday);

        var slot = Assert.Single(slots);

        Assert.Equal(1, slot.Col);
        Assert.Equal(4, slot.Span);
    }

    [Fact]
    public void 겹치지_않으면_같은_줄에_선다()
    {
        // 월~화 와 목~금. 사이가 비어 있으므로 한 줄에 둘 다 들어간다 —
        // 여기서 줄을 새로 만들면 달력이 쓸데없이 길어진다.
        var slots = CalendarLanes.Pack([Span(7, 8), Span(10, 11)], Sunday);

        Assert.Equal(2, slots.Count);
        Assert.All(slots, s => Assert.Equal(0, s.Lane));
    }

    [Fact]
    public void 하루라도_겹치면_아래_줄로_내려간다()
    {
        var slots = CalendarLanes.Pack([Span(7, 9), Span(9, 11)], Sunday);

        Assert.Equal(0, slots[0].Lane);
        Assert.Equal(1, slots[1].Lane);
    }

    [Fact]
    public void 빈_줄이_생기면_그_줄을_다시_쓴다()
    {
        // 셋이 같은 날 겹쳐 0·1·2 줄을 쓰고, 넷째는 그것들과 안 겹친다.
        // 넷째가 3줄로 가면 달력에 이 빠진 자리가 남는다.
        var slots = CalendarLanes.Pack(
            [Span(7, 7), Span(7, 7), Span(7, 7), Span(9, 9)], Sunday);

        Assert.Equal([0, 1, 2], slots.Take(3).Select(s => s.Lane));
        Assert.Equal(0, slots[3].Lane);
    }

    [Fact]
    public void 앞_주에서_이어져_온_것은_첫_칸에서_시작하고_왼쪽이_잘린다()
    {
        // 9/3(목)에 시작해 9/8(화)에 끝나는 일. 이 주에서는 9/6 부터 보인다.
        var slots = CalendarLanes.Pack([(new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 8))], Sunday);

        var slot = Assert.Single(slots);

        Assert.Equal(0, slot.Col);
        Assert.Equal(3, slot.Span);
        Assert.True(slot.ClipStart);
        Assert.False(slot.ClipEnd);
    }

    [Fact]
    public void 다음_주로_이어지는_것은_마지막_칸에서_끊기고_오른쪽이_잘린다()
    {
        var slots = CalendarLanes.Pack([(new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 20))], Sunday);

        var slot = Assert.Single(slots);

        Assert.Equal(5, slot.Col);
        Assert.Equal(2, slot.Span);
        Assert.False(slot.ClipStart);
        Assert.True(slot.ClipEnd);
    }

    [Fact]
    public void 주를_통째로_덮는_것은_일곱_칸_모두_잘린다()
    {
        var slots = CalendarLanes.Pack([(new DateOnly(2026, 8, 30), new DateOnly(2026, 9, 20))], Sunday);

        var slot = Assert.Single(slots);

        Assert.Equal(0, slot.Col);
        Assert.Equal(7, slot.Span);
        Assert.True(slot.ClipStart);
        Assert.True(slot.ClipEnd);
    }

    [Fact]
    public void 이_주에_걸치지_않는_것은_자리를_받지_않는다()
    {
        // 앞 주와 다음 주. 둘 다 이 주에는 그릴 것이 없다.
        var slots = CalendarLanes.Pack(
            [(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)),
             (new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 15))],
            Sunday);

        Assert.Empty(slots);
    }

    [Fact]
    public void 걸치지_않는_것을_건너뛰어도_원래_차례를_잃지_않는다()
    {
        // **`Index` 가 넣어 준 목록의 자리를 가리킨다.** 건너뛴 것 때문에
        // 한 칸씩 밀리면 화면이 엉뚱한 일정을 그 막대에 묶는다.
        var slots = CalendarLanes.Pack(
            [(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)), Span(8, 8)], Sunday);

        var slot = Assert.Single(slots);

        Assert.Equal(1, slot.Index);
    }

    [Fact]
    public void 거꾸로_들어간_기간은_아무_줄도_만들지_않는다()
    {
        // 화면이 먼저 바로잡지만, 여기까지 흘러와도 음수 길이를 그리지 않는다.
        var slots = CalendarLanes.Pack([(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 7))], Sunday);

        Assert.Empty(slots);
    }
}
