using System.Text.Json.Nodes;
using System.Threading.Channels;

using AIAgentServer.Services;
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
        var p = AssistantAgent.SystemPrompt(DateTime.UtcNow, new RagScope(["hanju"], null, "한주 › 규정"));
        Assert.Contains("한주 › 규정", p, StringComparison.Ordinal);
    }

    [Fact]
    public void 묻는_사람과_말투로_거절하지_말라는_것을_적는다()
    {
        var p = AssistantAgent.SystemPrompt(DateTime.UtcNow, null, "als900915");
        Assert.Contains("als900915", p, StringComparison.Ordinal);
        Assert.Contains("말투를 가려 거절하지 마십시오", p, StringComparison.Ordinal);
        Assert.DoesNotContain("포털 아이디는", AssistantAgent.SystemPrompt(DateTime.UtcNow, null), StringComparison.Ordinal);
    }

    /// <summary>프로젝트만 고르고 문서 범위는 안 좁혔으면 「문서 범위가 좁혀졌다」고 말하지 않는다.</summary>
    [Fact]
    public void 고른_프로젝트는_일정_범위로_적는다()
    {
        var p = AssistantAgent.SystemPrompt(DateTime.UtcNow, new RagScope(null, null, "수빅자재모듈", [8]));
        Assert.Contains("프로젝트 일정·휴가는 고른 프로젝트 「수빅자재모듈」", p, StringComparison.Ordinal);
        Assert.DoesNotContain("문서 검색 범위는", p, StringComparison.Ordinal);
        Assert.Contains("search_leaves", p, StringComparison.Ordinal);
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
            call: (msgs, tools, must, ct) => Task.FromResult<JsonObject?>(
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
            call: (m, t, must, ct) => Task.FromResult<JsonObject?>(Says("없습니다.")),
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
            call: (m, t, must, ct) => Task.FromResult<JsonObject?>(
                t is null ? Says("정리") : WantsTool(1)),
            tool: (n, a, ct) => throw new HttpRequestException("헬프데스크가 죽었다"),
            messages: [], toolSpec: [], maxRounds: 2, ct: default);

        Assert.Equal("정리", answer);
    }

    /// <summary>첫 차례에만 도구를 강제한다 — 「똥 언제 쌌지」를 업무 밖이라며 도구 없이 거절하던 것을 막는다.
    /// 둘째 차례부터와 마지막 정리 호출은 강제하지 않는다(강제하면 끝없이 찾는다).</summary>
    [Fact]
    public async Task 첫_차례에만_도구를_강제한다()
    {
        var seen = new List<bool>();
        JsonArray spec = [new JsonObject { ["type"] = "function" }];

        await AssistantAgent.RunAsync(
            call: (m, t, must, ct) =>
            {
                seen.Add(must);
                return Task.FromResult<JsonObject?>(t is null ? Says("정리") : WantsTool(seen.Count));
            },
            tool: (n, a, ct) => Task.FromResult("0건"),
            messages: [], toolSpec: spec, maxRounds: 2, ct: default);

        Assert.Equal([true, false, false], seen);
    }

    [Fact]
    public async Task 도구가_없으면_강제하지_않는다()
    {
        var seen = new List<bool>();

        await AssistantAgent.RunAsync(
            call: (m, t, must, ct) => { seen.Add(must); return Task.FromResult<JsonObject?>(Says("없습니다.")); },
            tool: (n, a, ct) => Task.FromResult(""),
            messages: [], toolSpec: [], maxRounds: 6, ct: default);

        Assert.Equal([false], seen);
    }

    /// <summary>사람이 창을 닫아 ct 가 끊긴 것은 도구가 죽은 것과 다르다 — 그 까닭을 글로 바꿔 삼키면
    /// 이미 끊긴 요청인데도 모델을 한 라운드 더 불러 보고서야 멈춘다(그 호출도 결국 같은 ct 로 터진다).
    /// 리뷰 1라운드 Finding 2: ct 로 인한 OperationCanceledException 은 도구 실패 글로 바뀌지 않고
    /// 루프 밖으로 그대로 나가야, 모델을 다시 부르지 않고 바로 멈춘다.</summary>
    [Fact]
    public async Task 취소된_토큰으로_도구가_터지면_모델을_다시_부르지_않고_그대로_나간다()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var modelCalls = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() => AssistantAgent.RunAsync(
            call: (m, t, must, ct) =>
            {
                modelCalls++;
                return Task.FromResult<JsonObject?>(WantsTool(1));
            },
            tool: (n, a, ct) => throw new OperationCanceledException(cts.Token),
            messages: [], toolSpec: [], maxRounds: 5, ct: cts.Token));

        Assert.Equal(1, modelCalls);
    }
}

/// <summary>리뷰 1라운드 Finding 1: 사람이 창을 닫아 StreamAsync 의 ct 가 끊기면, 답 스트리밍은
/// 멈춰도 감사 저장과 채널 닫기는 반드시 일어나야 한다 — 그 순간까지 이미 남의 요청글·일정·메일을
/// 읽은 뒤이기 때문이다. StreamAsync 자체는 AiProviderRegistry·PortalToolRunner·AiUsageDbContext
/// 를 전부 DI 로 받아 직접 시험하기 번거로워, 마무리 로직만 떼어 낸 AssistantAgent.FinalizeAsync 를
/// DB 없이(저장 대리자로) 시험한다.</summary>
public class AssistantFinalizeTests
{
    [Fact]
    public async Task 취소돼도_감사저장과_채널닫기는_일어난다()
    {
        var channel = Channel.CreateUnbounded<ChatStreamPart>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var saveCalls = 0;
        CancellationToken? savedWith = null;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AssistantAgent.FinalizeAsync(
            channel.Writer,
            "답",
            ct =>
            {
                saveCalls++;
                savedWith = ct;
                return Task.CompletedTask;
            },
            cts.Token));

        Assert.Equal(1, saveCalls);
        // 감사 저장은 끊긴 ct 가 아니라 CancellationToken.None 으로 불러야 저장 자체가
        // 끊기지 않는다 — 같은 토큰을 그대로 넘겼다면 이 값이 cts.Token 이었을 것이다.
        Assert.Equal(CancellationToken.None, savedWith);
        Assert.True(channel.Reader.Completion.IsCompleted);
    }

    [Fact]
    public async Task 정상_종료에도_답이_담기고_저장과_닫기가_한번씩_일어난다()
    {
        var channel = Channel.CreateUnbounded<ChatStreamPart>();
        var saveCalls = 0;

        await AssistantAgent.FinalizeAsync(
            channel.Writer, "답", ct => { saveCalls++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(1, saveCalls);
        // Completion 은 쓰기가 끝났다는 것만으론 안 끝난다 — 남은 조각을 다 읽어야 끝난다.
        Assert.True(channel.Reader.TryRead(out var part));
        Assert.Equal("답", part!.Text);
        Assert.True(channel.Reader.Completion.IsCompleted);
    }
}
