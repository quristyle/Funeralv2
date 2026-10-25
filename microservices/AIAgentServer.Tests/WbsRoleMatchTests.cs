using JSini.Shared.Internal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// 업무 비서가 WBS 일정에서 "어느 역할로 걸렸는지" 를 보여 줄 때 쓰는 표시.
/// </summary>
/// WBS 한 줄에는 개발·빌드·검수 세 사람이 함께 걸려 있어, 조회 조건으로 쓴 사람이
/// 그중 어느 역할이었는지 남겨야 한다. PersonMatch 와 같은 모양이지만 역할 이름이
/// 다르므로 포맷을 한 곳(WbsRoleMatch)에서만 관리한다 — 순서나 콤마가 틀리면
/// Task 6 의 도구 결과 조립에서 바로 드러난다.
public class WbsRoleMatchTests
{
    [Fact]
    public void 세_역할_다_걸리면_셋_다_적는다()
    {
        var m = WbsRoleMatch.Of(dev: true, build: true, qc: true);
        Assert.Equal("개발,빌드,검수", m);
    }

    [Fact]
    public void 하나도_안_걸리면_빈_글자다()
        => Assert.Equal("", WbsRoleMatch.Of(false, false, false));

    [Fact]
    public void 걸린_역할만_차례대로_적는다()
        => Assert.Equal("개발,검수", WbsRoleMatch.Of(dev: true, build: false, qc: true));
}
