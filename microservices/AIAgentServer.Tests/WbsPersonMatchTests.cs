using JSini.Shared.Internal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// WBS 의 개발·빌드·검수 담당 칸은 로그인 아이디가 아니라 사람이 손으로 적은 글이라
/// "박경수/완료" 처럼 군더더기가 붙는다. 업무 비서가 그런 칸도 그 사람으로 찾아내되,
/// "박경수영" 같은 <b>다른 사람</b>까지 집지는 않아야 한다 — 그 경계를 이 시험이 지킨다.
/// </summary>
public class WbsPersonMatchTests
{
    [Fact]
    public void 완전히_같으면_가리킨_것이다()
        => Assert.True(WbsPersonMatch.Matches("quristyle", "quristyle"));

    [Fact]
    public void 앞뒤_공백과_대소문자는_가리지_않는다()
        => Assert.True(WbsPersonMatch.Matches(" Quristyle ", "quristyle"));

    [Fact]
    public void 뒤에_군더더기가_붙어도_구분자가_있으면_가리킨_것이다()
    {
        Assert.True(WbsPersonMatch.Matches("박경수/완료", "박경수"));
        Assert.True(WbsPersonMatch.Matches("박경수/", "박경수"));
    }

    [Fact]
    public void 글자가_이어지면_다른_사람이라_안_가리킨다()
        => Assert.False(WbsPersonMatch.Matches("박경수영", "박경수"));

    [Fact]
    public void 아예_다른_글자면_안_가리킨다()
        => Assert.False(WbsPersonMatch.Matches("김병만", "박경수"));

    [Fact]
    public void 칸이나_후보가_비면_안_가리킨다()
    {
        Assert.False(WbsPersonMatch.Matches(null, "박경수"));
        Assert.False(WbsPersonMatch.Matches("", "박경수"));
        Assert.False(WbsPersonMatch.Matches("박경수", null));
        Assert.False(WbsPersonMatch.Matches("박경수", "  "));
    }
}
