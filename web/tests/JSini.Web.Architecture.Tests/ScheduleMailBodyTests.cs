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

    [Theory]
    [InlineData("ISSUE", "이슈")]
    [InlineData("NOTICE", "공지")]
    [InlineData("REQUEST", "요구")]
    [InlineData("DINNER", "회식")]
    [InlineData("VACATION", "휴가")]
    [InlineData("OUTING", "외출")]
    [InlineData("UNDESIGNED", "미설계")]
    [InlineData("OVERTIME", "야근")]
    public void 이슈_공지_요구_회식_휴가_외출_미설계_야근도_추가대상_이름과_딱지가_선다(string code, string name)
    {
        Assert.Equal(name, AddTargets.Name(code));

        var day = new DateTime(2026, 11, 2);
        var html = ScheduleMailBody.Build(new ScheduleMailInput(
            new DateOnly(2026, 10, 24), new DateOnly(2026, 11, 7), new DateOnly(2026, 10, 31),
            [new(day, day, "일정", null, null, code)],
            "견본", "보내는이", null));

        Assert.Contains($">{name}</span>", html);
        Assert.DoesNotContain("#f0f2f5", html[..html.IndexOf($">{name}</span>", StringComparison.Ordinal)][^300..]);
    }
    [Fact]
    public void 계획_종료일이_지났는데_안_끝난_일정에만_지연_딱지와_안내가_선다()
    {
        var oct27 = new DateTime(2026, 10, 27);
        var input = new ScheduleMailInput(
            new DateOnly(2026, 10, 24), new DateOnly(2026, 11, 7), new DateOnly(2026, 10, 31),
            [
                new(oct27, oct27.AddDays(1), "늦은 일정", null, "박담당", "ADD", PlanEnd: new DateOnly(2026, 10, 28)),
                new(oct27, oct27, "끝낸 일정", Completed: true, PlanEnd: new DateOnly(2026, 10, 27)),
                new(oct27, oct27, "버린 일정", Discarded: true, PlanEnd: new DateOnly(2026, 10, 27)),
                new(oct27, oct27, "종료일 없는 일정"),
                new(oct27, new DateTime(2026, 11, 3), "아직 기한 안", PlanEnd: new DateOnly(2026, 11, 3)),
            ],
            "견본", "보내는이", null);

        var html = ScheduleMailBody.Build(input);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, ">지연 \\d+일</span>"));
        Assert.Contains(">지연 3일</span>", html);
        Assert.Contains("지연 일정 1건", html);
        Assert.Equal("1", ScheduleMailBody.Values(input, html: false)["delayCount"]);

        // 지연 딱지는 추가대상 딱지와 같은 칸에 앞서 선다.
        var late = html.IndexOf(">지연 3일</span>", StringComparison.Ordinal);
        Assert.True(late < html.IndexOf(">추가</span>", StringComparison.Ordinal));
    }

    [Fact]
    public void 지연이_없으면_안내_줄도_없다()
    {
        Assert.DoesNotContain("지연 일정", Body());
    }
}
