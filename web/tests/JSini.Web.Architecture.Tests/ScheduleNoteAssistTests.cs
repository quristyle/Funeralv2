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
}
