using AIAgentServer.Services.Portal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// 업무 비서가 질문 속 이름을 포털 사용자로 바꿀 때 쓰는 <see cref="UserDirectory.Describe"/>.
/// </summary>
/// 동명이인이 있는데 모델이 임의로 한 명을 골라 버리면 남의 자료가 엉뚱한 이름으로 나간다 —
/// 그래서 이 글은 "누구인지 모른다"를 숨기지 않고 사용자에게 되묻도록 적어야 한다.
/// FindAsync 는 DB 를 끼고 있어 여기서는 시험하지 않는다 — Describe 는 목록 하나만 받는
/// 순수 함수라 그 자체로 시험할 수 있다.
public class UserDirectoryTests
{
    [Fact]
    public void 아무도_없으면_없다고_적는다()
        => Assert.Equal("찾는 이름의 사용자가 없습니다.", UserDirectory.Describe([]));

    [Fact]
    public void 한_명이면_그_사람만_적는다()
    {
        var s = UserDirectory.Describe([new PortalPerson("u1", "홍길동", "진네트웍스", "개발팀")]);
        Assert.Contains("u1", s, StringComparison.Ordinal);
        Assert.Contains("홍길동", s, StringComparison.Ordinal);
        Assert.DoesNotContain("여러", s, StringComparison.Ordinal);
    }

    [Fact]
    public void 동명이인이면_고르지_말고_되물으라고_적는다()
    {
        var s = UserDirectory.Describe([
            new PortalPerson("u1", "홍길동", "진네트웍스", "개발팀"),
            new PortalPerson("u2", "홍길동", "한주", "영업팀"),
        ]);

        Assert.Contains("u1", s, StringComparison.Ordinal);
        Assert.Contains("u2", s, StringComparison.Ordinal);
        Assert.Contains("되물어", s, StringComparison.Ordinal);
    }

    [Fact]
    public void 부서가_없어도_어색하지_않게_적는다()
    {
        var s = UserDirectory.Describe([new PortalPerson("u3", "김지수", "진네트웍스", null)]);
        Assert.DoesNotContain("(부서: )", s, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", s, StringComparison.Ordinal);
    }

    [Fact]
    public void 회사도_부서도_없어도_어색하지_않게_적는다()
    {
        var s = UserDirectory.Describe([new PortalPerson("u4", "김지수", null, null)]);
        Assert.Contains("u4", s, StringComparison.Ordinal);
        Assert.Contains("김지수", s, StringComparison.Ordinal);
        Assert.DoesNotContain("()", s, StringComparison.Ordinal);
    }
}
