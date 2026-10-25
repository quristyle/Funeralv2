using JSini.Shared.Internal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// 내부 서비스 간 호출만 받아들이는 <b>머리글 자격 검사</b>.
/// </summary>
/// 게이트웨이를 거치지 않는 서버 대 서버 호출(헬프데스크·알림·업무 비서)이 공유하는
/// 길목이라, 비교 방식 하나를 틀리면 셋이 한꺼번에 뚫리거나 막힌다. 네트워크도 설정도
/// 없는 순수 함수라 시험이 싸다.
public class InternalCallGuardTests
{
    [Fact]
    public void 설정값이_비면_언제나_막는다()
    {
        Assert.False(InternalCallGuard.IsAllowed("아무거나", null));
        Assert.False(InternalCallGuard.IsAllowed("아무거나", ""));
        Assert.False(InternalCallGuard.IsAllowed("아무거나", "   "));
    }

    [Fact]
    public void 머리글이_없거나_다르면_막는다()
    {
        Assert.False(InternalCallGuard.IsAllowed(null, "비밀"));
        Assert.False(InternalCallGuard.IsAllowed("비밀 ", "비밀"));
        Assert.False(InternalCallGuard.IsAllowed("Bearer 비밀", "비밀"));
    }

    [Fact]
    public void 똑같을_때만_통과한다()
        => Assert.True(InternalCallGuard.IsAllowed("비밀", "비밀"));
}
