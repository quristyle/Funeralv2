using ProjMngServer.Services;

using Xunit;

namespace ProjMngServer.Tests;

/// <summary>
/// <c>summary_text</c> 평문을 되읽는 규칙. <b>화면과 같은 모양을 약속한다.</b>
/// </summary>
/// <remarks>
/// 담는 쪽(<see cref="AiResultSummary.ToText"/>)과 되읽는 쪽이 짝이고,
/// 포털의 <c>JSini.Web.ProjMng/Components/Shared/AiRunSummary.Parse</c> 가
/// <b>세 번째로</b> 같은 모양을 읽는다. 한쪽만 고치면 같은 실행을 두고
/// 메일과 화면이 다른 말을 한다.
/// </remarks>
public class AiResultSummaryParseTests
{
    [Fact]
    public void 결론과_한일과_확인할것을_갈라_읽는다()
    {
        var summary = AiResultSummary.Parse("""
            메일에 처리 요약을 담았습니다

            - 요약 칸을 머리띠 바로 아래로 올림
            - 라벨 뒤 좁은 칸을 걷어냄

            확인할 것
            - 배포 뒤 휴대폰에서 확인
            """);

        Assert.NotNull(summary);
        Assert.Equal("메일에 처리 요약을 담았습니다", summary!.Headline);
        Assert.Equal(2, summary.Points.Count);
        Assert.Single(summary.Checks);
    }

    /// <summary>
    /// 결론이 <b>두 줄로 나뉘어 와도 잃지 않는다.</b> 버리면 메일에는 앞
    /// 문장만 서고 화면에는 둘 다 서서 같은 실행이 두 말을 한다.
    /// </summary>
    [Fact]
    public void 머리표_없는_둘째_줄은_결론에_이어_붙인다()
    {
        var summary = AiResultSummary.Parse("""
            메일에 처리 요약을 담았습니다
            그리고 자리도 머리띠 밑으로 올렸습니다

            - 요약 칸을 올림
            """);

        Assert.NotNull(summary);
        Assert.Equal(
            "메일에 처리 요약을 담았습니다 그리고 자리도 머리띠 밑으로 올렸습니다",
            summary!.Headline);
        Assert.Single(summary.Points);
    }

    /// <summary>한 일이 시작된 뒤의 맨줄은 결론이 아니다 — 거기까지 붙이면 뒤섞인다.</summary>
    [Fact]
    public void 한일이_시작된_뒤의_맨줄은_결론에_안_붙인다()
    {
        var summary = AiResultSummary.Parse("""
            결론 한 줄

            - 한 일 하나
            뒤에 붙은 맨줄
            """);

        Assert.NotNull(summary);
        Assert.Equal("결론 한 줄", summary!.Headline);
    }

    [Fact]
    public void 적을_것이_없으면_널이다()
    {
        Assert.Null(AiResultSummary.Parse(null));
        Assert.Null(AiResultSummary.Parse("   \n  \n"));
    }

    /// <summary>담고 되읽으면 <b>같은 것이 나온다.</b> 두 벌이 어긋나면 여기서 걸린다.</summary>
    [Fact]
    public void 담은_것을_되읽으면_같다()
    {
        var original = new AiResultSummary(
            "결론 한 문장", ["한 일 하나", "한 일 둘"], ["확인 하나"]);

        var round = AiResultSummary.Parse(original.ToText());

        Assert.NotNull(round);
        Assert.Equal(original.Headline, round!.Headline);
        Assert.Equal(original.Points, round.Points);
        Assert.Equal(original.Checks, round.Checks);
    }
}
