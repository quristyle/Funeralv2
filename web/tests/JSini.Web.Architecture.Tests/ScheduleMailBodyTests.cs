using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>일정 메일 본문의 일자별 타임라인 — 시각 대신 요청자·담당자, 일 옆의 작은 요일, 달 구분선, 추가대상 딱지.</summary>
public sealed class ScheduleMailBodyTests
{
    private static string Body()
    {
        var oct30 = new DateTime(2026, 10, 30, 9, 30, 0);
        var nov2 = new DateTime(2026, 11, 2);

        var input = new ScheduleMailInput(
            new DateOnly(2026, 10, 24), new DateOnly(2026, 11, 7), new DateOnly(2026, 10, 31),
            [
                new(oct30, oct30.AddHours(2), "주간 점검", "김요청", "홍길동", "CHANGE"),
                new(nov2, nov2, "통합 시험", null, "박담당", "ADD"),
                new(nov2, nov2, "중간 보고"),
            ],
            "견본", "보내는이", null);

        return ScheduleMailBody.Build(input);
    }

    [Fact]
    public void 일정에_시각은_없고_요청자_담당자가_적힌다()
    {
        var html = Body();

        Assert.DoesNotContain("09:30", html);
        Assert.DoesNotContain("11:30", html);
        Assert.Contains("요청자 김요청 · 담당자 홍길동", html);
        Assert.Contains("담당자 박담당", html);
    }

    [Fact]
    public void 날짜_상자는_월_없이_일_옆에_작은_요일을_적는다()
    {
        var html = Body();

        Assert.DoesNotContain(">10월</div>", html);
        Assert.DoesNotContain("금요일", html);
        Assert.Contains(">30</span>", html);
        Assert.Contains(">(금)</span>", html);
    }

    [Fact]
    public void 달이_바뀌면_그_달을_적은_구분선이_선다()
    {
        var html = Body();

        Assert.Contains(">10월</td>", html);
        Assert.Contains(">11월</td>", html);
    }

    [Fact]
    public void 추가대상이_있는_일정만_딱지가_붙는다()
    {
        var html = Body();

        Assert.Contains(">변경</span>", html);
        Assert.Contains(">추가</span>", html);
        Assert.DoesNotContain(">이동</span>", html);
    }

    [Fact]
    public void 폐기한_일정은_빠지지_않고_취소선과_폐기_딱지로_흐려진다()
    {
        var nov2 = new DateTime(2026, 11, 2);
        var input = new ScheduleMailInput(
            new DateOnly(2026, 10, 24), new DateOnly(2026, 11, 7), new DateOnly(2026, 10, 31),
            [
                new(nov2, nov2, "살아 있는 일정"),
                new(nov2, nov2, "버린 일정", null, "박담당", "ADD", Discarded: true),
            ],
            "견본", "보내는이", null);

        var html = ScheduleMailBody.Build(input);

        var gone = html.IndexOf("버린 일정", StringComparison.Ordinal);
        var alive = html.IndexOf("살아 있는 일정", StringComparison.Ordinal);
        Assert.True(gone > 0 && alive > 0);

        // 제목 바로 앞의 스타일에 취소선이 있는 것은 폐기한 쪽뿐이다.
        Assert.Contains("text-decoration:line-through;\">버린 일정", html);
        Assert.DoesNotContain("line-through;\">살아 있는 일정", html);
        Assert.Contains(">폐기</span>", html);
        Assert.Contains("dashed", html[..gone][^900..]);
        Assert.Contains(">추가</span>", html);
    }
}
