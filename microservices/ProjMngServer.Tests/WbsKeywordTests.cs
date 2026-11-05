using ProjMngServer.Services;

using Xunit;

namespace ProjMngServer.Tests;

/// <summary>일정 낱말 찾기. 운영 일정표의 「똥싸기」(prj 8, 2026-11-10)를 「똥싼날」로 물으면 못 찾던 것을 묶는다.</summary>
public class WbsKeywordTests
{
    private static InternalQueryService.WbsScheduleRow Row(string title, string? comm = null, string? summary = null) =>
        new(title, null, null, null, null, null, null, null, comm, "READY", 8, "수빅자재모듈", "Public", null, summary);

    private static readonly List<InternalQueryService.WbsScheduleRow> Rows =
    [
        Row("정상준 전임 병원진료반차", "정상준 전임이 병원진료 반차를 사용한다."),
        Row("똥싸기", "똥을 쌘다."),
        Row("오후 4시 안전 회의"),
    ];

    [Theory]
    [InlineData("똥싼날", "똥싸기")]
    [InlineData("똥싼", "똥싸기")]
    [InlineData("똥", "똥싸기")]
    [InlineData("똥싸기", "똥싸기")]
    [InlineData("병원 진료", "정상준 전임 병원진료반차")]
    [InlineData("회의", "오후 4시 안전 회의")]
    public void 물은_낱말로_그_일정을_찾는다(string keyword, string title)
    {
        var hit = Assert.Single(InternalQueryService.FilterKeyword(Rows, keyword));
        Assert.Equal(title, hit.ProcNm);
    }

    /// <summary>그대로 든 줄이 있으면 넓히지 않는다 — 넓힌 줄이 섞이면 「회의」가 「회식」까지 끌어온다.</summary>
    [Fact]
    public void 그대로_걸리면_넓혔다는_표시가_없다()
    {
        var hit = Assert.Single(InternalQueryService.FilterKeyword(Rows, "똥싸기"));
        Assert.Null(hit.KeywordMatch);
    }

    [Fact]
    public void 넓혀_걸리면_무엇으로_걸렸는지_적는다()
    {
        var hit = Assert.Single(InternalQueryService.FilterKeyword(Rows, "똥싼날"));
        Assert.Equal("똥싸", hit.KeywordMatch);
    }

    [Theory]
    [InlineData("출장간날")]  // 「장」의 ㅇ 은 낱말 몸이라 떼지 않는다 — 「출자」로 찾지 않는다
    [InlineData("똥 언제")]   // 낱말이 모두 걸려야 한다
    public void 없는_것은_넓혀도_없다(string keyword) =>
        Assert.Empty(InternalQueryService.FilterKeyword(Rows, keyword));

    [Fact]
    public void 낱말이_없으면_거르지_않는다() =>
        Assert.Equal(3, InternalQueryService.FilterKeyword(Rows, " ").Count);

    [Theory]
    [InlineData("똥싼날", new[] { "똥싼", "똥싸" })]
    [InlineData("휴가간날", new[] { "휴가" })]
    [InlineData("ERP화면", new[] { "ERP화면" })]
    [InlineData("똥", new[] { "똥" })]
    public void 앞_두_글자와_끝받침_뗀_꼴로_넓힌다(string token, string[] variants) =>
        Assert.Equal(variants, Assert.Single(WbsKeyword.Relax(token)));
}
