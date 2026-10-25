using JSini.Shared.Internal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// 업무 비서가 "왜 이 글이 나왔는지" 를 보여 줄 때 쓰는 <b>걸린 길 표시</b>.
/// </summary>
/// 헬프데스크 내부 조회가 쓴 글·맡은 글·댓글 세 갈래를 OR 로 묶어 찾은 글을 돌려주므로,
/// 그중 무엇으로 걸렸는지를 사람이 읽을 말로 남겨야 한다. 순서가 바뀌면 답이 어색해지고
/// 콤마 위치 하나만 틀려도 포맷 계약(Task 6 의 도구 결과 조립)이 깨진다.
public class PersonMatchTests
{
    [Fact]
    public void 세_갈래로_걸리면_셋_다_적는다()
    {
        var m = PersonMatch.Of(wrote: true, assigned: true, commented: true);
        Assert.Equal("쓴 글,맡은 글,댓글", m);
    }

    [Fact]
    public void 하나도_안_걸리면_빈_글자다()
        => Assert.Equal("", PersonMatch.Of(false, false, false));

    [Fact]
    public void 걸린_것만_차례대로_적는다()
        => Assert.Equal("쓴 글,댓글", PersonMatch.Of(wrote: true, assigned: false, commented: true));
}
