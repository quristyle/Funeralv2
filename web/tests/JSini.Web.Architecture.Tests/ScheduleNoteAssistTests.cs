using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>AI 일정 메모가 모델의 답에서 일정 칸을 꺼내는 길(<see cref="ScheduleNoteAssist.Parse"/>). 모델은 시키지 않아도 코드 울타리나 설명을 붙여 온다.</summary>
public sealed class ScheduleNoteAssistTests
{
    [Fact]
    public void 코드_울타리에_싸여_와도_칸을_읽는다()
    {
        var draft = ScheduleNoteAssist.Parse(
            "```json\n{\"start\":\"2026-11-03\",\"end\":\"2026-11-05\",\"title\":\"울산 점검\",\"summary\":\"한 줄\\n두 줄\"}\n```",
            "m");

        Assert.Equal(new DateOnly(2026, 11, 3), draft.Start);
        Assert.Equal(new DateOnly(2026, 11, 5), draft.End);
        Assert.Equal("울산 점검", draft.Title);
        Assert.Equal("한 줄\n두 줄", draft.Summary);
        Assert.Equal("m", draft.Model);
    }

    [Fact]
    public void 끝이_시작보다_앞이거나_없으면_시작으로_맞춘다()
    {
        Assert.Equal(new DateOnly(2026, 11, 3),
            ScheduleNoteAssist.Parse("{\"start\":\"2026-11-03\",\"end\":\"2026-11-01\"}", null).End);

        Assert.Equal(new DateOnly(2026, 11, 3),
            ScheduleNoteAssist.Parse("{\"start\":\"2026-11-03\"}", null).End);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("모르겠습니다")]
    [InlineData("{ 깨진 json")]
    public void 못_읽는_답은_빈_칸이다(string? answer)
    {
        var draft = ScheduleNoteAssist.Parse(answer, null);

        Assert.Null(draft.Start);
        Assert.Null(draft.Title);
        Assert.Null(draft.Summary);
    }

    [Fact]
    public void 시간_요구자_담당자_정보를_함께_파싱한다()
    {
        var json = """
            {
              "start": "2026-10-29",
              "start_time": "10:00",
              "end": "2026-10-29",
              "end_time": "14:00",
              "title": "현업 시연",
              "summary": "시연 진행 요약",
              "requester": "서성일",
              "assignee": "이순열"
            }
            """;

        var draft = ScheduleNoteAssist.Parse(json, "기본사용자", "gpt");

        Assert.Equal(new DateOnly(2026, 10, 29), draft.Start);
        Assert.Equal(new DateOnly(2026, 10, 29), draft.End);
        Assert.Equal(new TimeOnly(10, 0), draft.StartTime);
        Assert.Equal(new TimeOnly(14, 0), draft.EndTime);
        Assert.Equal("현업 시연", draft.Title);
        Assert.Equal("시연 진행 요약", draft.Summary);
        Assert.Equal("서성일", draft.Requester);
        Assert.Equal("이순열", draft.Assignee);
        Assert.Equal(new DateTime(2026, 10, 29, 10, 0, 0), draft.StartDateTime);
        Assert.Equal(new DateTime(2026, 10, 29, 14, 0, 0), draft.EndDateTime);
    }

    [Fact]
    public void 담당자가_누락되면_기본_담당자로_지정된다()
    {
        var json = """
            {
              "start": "2026-10-29",
              "title": "현업 시연",
              "summary": "시연 진행 요약"
            }
            """;

        var draft = ScheduleNoteAssist.Parse(json, "홍길동", "gpt");

        Assert.Equal("홍길동", draft.Assignee);
    }

    [Fact]
    public void 일정이_여럿이면_여러_건으로_읽는다()
    {
        var json = """
            ```json
            {"schedules":[
              {"start":"2026-11-03","title":"울산 점검","summary":"점검"},
              {"start":"2026-11-05","start_time":"14:00","title":"현업 시연","assignee":"이순열"}
            ]}
            ```
            """;

        var drafts = ScheduleNoteAssist.ParseMany(json, "홍길동", "gpt");

        Assert.Equal(2, drafts.Count);
        Assert.Equal("울산 점검", drafts[0].Title);
        Assert.Equal("홍길동", drafts[0].Assignee);
        Assert.Equal(new DateOnly(2026, 11, 5), drafts[1].Start);
        Assert.Equal(new TimeOnly(14, 0), drafts[1].StartTime);
        Assert.Equal("이순열", drafts[1].Assignee);
    }

    [Fact]
    public void 맨_배열이나_한_건짜리_객체도_받는다()
    {
        Assert.Equal(2, ScheduleNoteAssist.ParseMany(
            "[{\"title\":\"가\"},{\"title\":\"나\"}]", null, null).Count);

        var single = Assert.Single(ScheduleNoteAssist.ParseMany(
            "{\"start\":\"2026-11-03\",\"title\":\"한 건\"}", null, null));
        Assert.Equal("한 건", single.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("모르겠습니다")]
    [InlineData("{ 깨진 json")]
    [InlineData("{\"schedules\":[]}")]
    [InlineData("{\"schedules\":[{}]}")]
    public void 못_읽어도_최소_한_건은_돌려준다(string? answer)
    {
        var draft = Assert.Single(ScheduleNoteAssist.ParseMany(answer, "홍길동", null));

        Assert.Null(draft.Title);
        Assert.Equal("홍길동", draft.Assignee);
    }

    [Fact]
    public void 날짜마다_뽑은_일정은_그날이_계획시작_종료가_된다()
    {
        // "9월1일 홍길동이 방구를 끼었다. 그러고 9/15 에 똥까지 싸더라." 에 로컬 모델이 실제로 준 답
        var json = """
            {"schedules":[{"start":"2026-09-01","start_time":null,"end":"2026-09-01","end_time":null,"title":"홍길동 방구낀날","summary":"홍길동이 방구를 끼었다.","requester":null,"assignee":"홍길동"},{"start":"2026-09-15","start_time":null,"end":"2026-09-15","end_time":null,"title":"홍길동 똥싼날","summary":"홍길동이 똥을 쌌다.","requester":null,"assignee":"홍길동"}]}
            """;

        var drafts = ScheduleNoteAssist.ParseMany(json, "이순신", "qwen");

        Assert.False(ScheduleNoteAssist.IsBlank(drafts));
        Assert.Collection(drafts,
            d =>
            {
                Assert.Equal("홍길동 방구낀날", d.Title);
                Assert.Equal(new DateTime(2026, 9, 1), d.StartDateTime);
                Assert.Equal(new DateTime(2026, 9, 1), d.EndDateTime);
            },
            d =>
            {
                Assert.Equal("홍길동 똥싼날", d.Title);
                Assert.Equal(new DateTime(2026, 9, 15), d.StartDateTime);
                Assert.Equal(new DateTime(2026, 9, 15), d.EndDateTime);
            });
    }

    [Theory]
    [InlineData("{\"schedules\":[]}", true)]
    [InlineData("모르겠습니다", true)]
    [InlineData("{\"schedules\":[{\"title\":\"가\"}]}", false)]
    public void 빈_답이면_다시_물을_대상이다(string answer, bool blank)
    {
        Assert.Equal(blank, ScheduleNoteAssist.IsBlank(ScheduleNoteAssist.ParseMany(answer, "홍길동", null)));
    }

    [Fact]
    public void 너무_많이_쪼개면_상한에서_자른다()
    {
        var items = string.Join(",", Enumerable.Range(1, 30).Select(i => $"{{\"title\":\"{i}\"}}"));

        Assert.Equal(ScheduleNoteAssist.MaxDrafts,
            ScheduleNoteAssist.ParseMany($"{{\"schedules\":[{items}]}}", null, null).Count);
    }

    private static readonly DateOnly Today = new(2026, 11, 2);

    private static ScheduleDraft Pin(string memo, DateOnly start, DateOnly? end = null, TimeOnly? time = null) =>
        Assert.Single(ScheduleNoteAssist.PinRelativeDay(
            [new ScheduleDraft(start, end ?? start, "t", "s", null, "이순열", time, null, null)], memo, Today));

    [Theory]
    [InlineData("오늘 큰정전임 2층 hnix 개발자 환경지원.", 2)]
    [InlineData("내일 서버 점검", 3)]
    [InlineData("어제 회식", 1)]
    public void 오늘_내일만_적힌_메모는_모델이_밀어_적어도_그날로_못박는다(string memo, int day)
    {
        var d = Pin(memo, new DateOnly(2026, 11, 3));

        Assert.Equal(new DateOnly(2026, 11, day), d.Start);
        Assert.Equal(new DateOnly(2026, 11, day), d.End);
    }

    [Fact]
    public void 못박아도_기간과_시각은_그대로다()
    {
        var d = Pin("오늘 14시 설비 반입", new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4), new TimeOnly(14, 0));

        Assert.Equal(Today, d.Start);
        Assert.Equal(Today.AddDays(1), d.End);
        Assert.Equal(new TimeOnly(14, 0), d.StartTime);
    }

    [Theory]
    [InlineData("오늘 정리하고 11월 5일 반영")]
    [InlineData("오늘 말고 다음 주 화요일")]
    [InlineData("오늘 하고 내일 마무리")]
    [InlineData("11/16 운영서버 대상 도출")]
    [InlineData("hnix 개발환경 지원")]
    public void 다른_날짜_말이_섞이거나_날을_안_가리키면_손대지_않는다(string memo)
    {
        var start = new DateOnly(2026, 11, 5);

        Assert.Equal(start, Pin(memo, start).Start);
    }

    [Fact]
    public void 여러_건으로_갈린_일정은_손대지_않는다()
    {
        ScheduleDraft[] drafts =
        [
            new(new DateOnly(2026, 11, 3), null, "a", null, null),
            new(new DateOnly(2026, 11, 4), null, "b", null, null),
        ];

        Assert.Same(drafts, ScheduleNoteAssist.PinRelativeDay(drafts, "오늘 a, b", Today));
    }
}
