using ProjMngServer.Services;

using Xunit;

namespace ProjMngServer.Tests;

/// <summary>운영 일정표에 실제로 있는 제목들로 휴가 줄 가리기를 묶는다.</summary>
public class WbsLeaveTests
{
    [Theory]
    [InlineData("정일인 휴가", "휴가", "정일인")]
    [InlineData("김원욱 여름휴가", "휴가", "김원욱")]
    [InlineData("이순열 휴가", "휴가", "이순열")]
    [InlineData("순열월차", "월차", "순열")]
    [InlineData("홍길동 오전반차", "반차", "홍길동")]
    [InlineData("[개인] 박경수 연차", "연차", "박경수")]
    [InlineData("연차 - 김지수", "연차", "김지수")]
    public void 제목에서_종류와_사람을_뽑는다(string title, string kind, string person)
    {
        var leave = WbsLeave.Parse(title);

        Assert.NotNull(leave);
        Assert.Equal(kind, leave.Kind);
        Assert.Equal(person, leave.Person);
    }

    /// <summary>휴가반납은 휴가를 물리고 일한 날이다 — 운영에 「[휴가반납] 박민성 출근 불가」가 있다.</summary>
    [Theory]
    [InlineData("[휴가반납] ASIS 컨버젼 프레임워크 개발")]
    [InlineData("[휴가반납] 박민성 출근 불가")]
    [InlineData("오후 4시 안전 회의")]
    [InlineData("")]
    [InlineData(null)]
    public void 휴가가_아닌_줄은_null(string? title) => Assert.Null(WbsLeave.Parse(title));

    [Theory]
    [InlineData("정일인 휴가", "정일인", true)]
    [InlineData("순열월차", "이순열", true)]
    [InlineData("정일인 휴가", "이순열", false)]
    [InlineData("정일인 휴가", null, true)]
    public void 물은_이름이_그_사람인지_가린다(string title, string? name, bool expected)
    {
        var leave = WbsLeave.Parse(title)!;
        Assert.Equal(expected, WbsLeave.IsFor(leave, title, name));
    }

    [Fact]
    public void 제목이_이름을_담으면_일정에도_걸린다()
    {
        Assert.True(InternalQueryService.TitleNames("정일인 휴가", "정일인"));
        Assert.False(InternalQueryService.TitleNames("정일인 휴가", "정"));
        Assert.False(InternalQueryService.TitleNames("정일인 휴가", null));
    }
}
