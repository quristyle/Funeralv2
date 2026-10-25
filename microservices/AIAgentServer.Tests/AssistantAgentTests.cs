using System.Text.Json.Nodes;

using AIAgentServer.Services.Portal;
using AIAgentServer.Services.Rag;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>업무 비서 시스템 프롬프트가 반드시 담아야 하는 것들 — 오늘 날짜(한국) ·
/// 자료 없으면 모른다고 하기 · 고른 범위 이름.</summary>
public class AssistantPromptTests
{
    [Fact]
    public void 오늘_한국_날짜를_프롬프트에_박는다()
    {
        var p = AssistantAgent.SystemPrompt(new DateTime(2026, 10, 24, 16, 0, 0, DateTimeKind.Utc), null);
        Assert.Contains("2026-10-25", p, StringComparison.Ordinal); // UTC 16시는 한국의 다음 날
    }

    [Fact]
    public void 자료가_없으면_모른다고_하라고_이른다()
    {
        var p = AssistantAgent.SystemPrompt(DateTime.UtcNow, null);
        Assert.Contains("없습니다", p, StringComparison.Ordinal);
        Assert.Contains("지어내지", p, StringComparison.Ordinal);
    }

    [Fact]
    public void 고른_카테고리를_프롬프트에_적는다()
    {
        var p = AssistantAgent.SystemPrompt(DateTime.UtcNow, new RagScope("hanju", null, "한주 › 규정"));
        Assert.Contains("한주 › 규정", p, StringComparison.Ordinal);
    }
}

/// <summary>도구 루프 자체 — 라운드 소진 · 도구 미호출 · 도구 예외 — 를 가짜 모델로 시험한다.
/// 실제 모델을 거치면 이 경로들을 운영에서나 확인하게 된다.</summary>
public class AssistantLoopTests
{
    // 늘 도구를 부르겠다고만 하는 모델. 라운드를 다 쓰게 만든다.
    private static JsonObject WantsTool(int i) => JsonNode.Parse(
        """{"tool_calls":[{"id":"c@I","function":{"name":"search_todos","arguments":"{}"}}]}"""
            .Replace("@I", i.ToString()))!.AsObject();

    private static JsonObject Says(string text) => JsonNode.Parse(
        """{"content":"@T"}""".Replace("@T", text))!.AsObject();

    [Fact]
    public async Task 라운드를_다_써도_답이_나온다()
    {
        var calls = 0;

        var (answer, called) = await AssistantAgent.RunAsync(
            // 도구를 안 줬다는 것은 마지막 정리 호출이라는 뜻이다.
            call: (msgs, tools, ct) => Task.FromResult<JsonObject?>(
                tools is null ? Says("찾은 것만 정리했습니다.") : WantsTool(++calls)),
            tool: (name, args, ct) => Task.FromResult("0건"),
            messages: [], toolSpec: [], maxRounds: 3, ct: default);

        Assert.Equal("찾은 것만 정리했습니다.", answer);
        Assert.Equal(3, called.Count);
    }

    [Fact]
    public async Task 도구를_안_부르면_그_답으로_끝낸다()
    {
        var (answer, called) = await AssistantAgent.RunAsync(
            call: (m, t, ct) => Task.FromResult<JsonObject?>(Says("없습니다.")),
            tool: (n, a, ct) => Task.FromResult(""),
            messages: [], toolSpec: [], maxRounds: 6, ct: default);

        Assert.Equal("없습니다.", answer);
        Assert.Empty(called);
    }

    [Fact]
    public async Task 도구가_터져도_루프는_멈추지_않는다()
    {
        // 세 시스템 가운데 하나가 죽어도 나머지로 답해야 한다.
        var (answer, _) = await AssistantAgent.RunAsync(
            call: (m, t, ct) => Task.FromResult<JsonObject?>(
                t is null ? Says("정리") : WantsTool(1)),
            tool: (n, a, ct) => throw new HttpRequestException("헬프데스크가 죽었다"),
            messages: [], toolSpec: [], maxRounds: 2, ct: default);

        Assert.Equal("정리", answer);
    }
}
