using System.Linq;

using AIAgentServer.Services.Portal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// 업무 비서 도구가 조회한 줄들을 모델이 읽을 글로 다듬는 자리 — 몇 건인지, 상한에 잘렸는지를 적는다.
/// </summary>
/// 자료가 없을 때 빈 글자를 돌려주면 모델이 그 빈칸을 다른 도구의 답으로 메워 엉뚱한 소리를 한다 —
/// 그래서 빈 결과도 반드시 "0건" 이라고 적어야 한다. Task 6 의 여러 도구가 이 포맷을 그대로 쓰므로
/// 세 가지 글자 모양(0건·잘림·안잘림)이 계약이다.
public class PortalToolResultTests
{
    [Fact]
    public void 빈_결과도_영건이라고_적는다()
    {
        var s = PortalToolResult.Render("요청글", [], total: 0, cap: 30);
        Assert.Contains("0건", s, System.StringComparison.Ordinal);
    }

    [Fact]
    public void 잘렸으면_총_몇건_중_몇건인지_적는다()
    {
        var s = PortalToolResult.Render("요청글", Enumerable.Repeat("줄", 30).ToList(), total: 312, cap: 30);
        Assert.Contains("총 312건 중 30건", s, System.StringComparison.Ordinal);
    }

    [Fact]
    public void 안_잘렸으면_총_몇건만_적는다()
    {
        var s = PortalToolResult.Render("요청글", Enumerable.Repeat("줄", 4).ToList(), total: 4, cap: 30);
        Assert.Contains("4건", s, System.StringComparison.Ordinal);
        Assert.DoesNotContain("중", s, System.StringComparison.Ordinal);
    }
}
