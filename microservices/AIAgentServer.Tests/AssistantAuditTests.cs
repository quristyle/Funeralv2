using System;

using AIAgentServer.Services.Portal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>
/// 업무 비서가 "권한 밖 자료를 봤다"는 흔적을 남기는 자리. 요약이 아니라 <b>답을 통째로</b> 담는 것이 핵심이라 —
/// 무엇이 나갔는지가 기록의 전부이고, 자르면 바로 그 증거가 없어진다. <see cref="AssistantAudit.Build"/> 는
/// DB 를 건드리지 않는 순수 함수라 여기서만으로도 검증이 끝난다(저장은 Task 8 몫).
/// </summary>
public class AssistantAuditTests
{
    [Fact]
    public void 답을_통째로_담는다()
    {
        var row = AssistantAudit.Build("u1", "질문", null, [], [], new string('가', 50_000), 10, null);
        Assert.Equal(50_000, row.Answer.Length);
    }

    [Fact]
    public void 도구_호출과_근거는_JSON_배열로_담는다()
    {
        var row = AssistantAudit.Build("u1", "질문", "hanju",
            ["find_user({\"name\":\"홍길동\"})"], ["요청글 #1042"], "답", 10, null);

        Assert.Equal("hanju", row.ScopeCategory);
        Assert.StartsWith("[", row.ToolCalls, StringComparison.Ordinal);
        Assert.Contains("1042", row.Sources, StringComparison.Ordinal);
    }

    [Fact]
    public void 시각은_UTC_다()
        => Assert.Equal(DateTimeKind.Utc, AssistantAudit.Build("u1", "q", null, [], [], "a", 1, null).AskedAt.Kind);
}
