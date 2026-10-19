using System.Runtime.CompilerServices;
using System.Text.Json;
using AIAgentServer.DTOs;

namespace AIAgentServer.Services;

/// <summary>공급자가 돌려준 답. <b>형식이 다른 두 계열을 같은 모양으로 읽게 해 준다.</b></summary>
public abstract class AiResponseBody : IDisposable
{
    /// <summary>공급자가 알려 준 토큰 수. <b>다 읽고 나서야 값이 있다.</b> 사람별 사용량(scom.ai_usage_logs)이 이 값으로 쌓인다.</summary>
    public AiTokenUsage? Usage { get; protected set; }

    /// <summary>완성된 답 전체를 읽는다. 스트리밍이 아닌 호출에서 쓴다.</summary>
    public abstract Task<string> ReadTextAsync();

    /// <summary>답을 조각으로 읽는다. <b>생각 블록을 걷어내기 전의 날 글자</b>를 흘린다 — 거르는 것은 <see cref="ReasoningFilter"/> 의 몫이라 여기서 하지 않는다.</summary>
    public abstract IAsyncEnumerable<string> ReadDeltasAsync(
        CancellationToken cancellationToken = default);

    public virtual void Dispose() { }
}

/// <summary>OpenAI 호환 공급자(로컬 LLM · Groq · OpenRouter)의 답.</summary>
public sealed class OpenAiResponseBody : AiResponseBody
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly HttpResponseMessage _response;
    private readonly ILogger _logger;

    public OpenAiResponseBody(HttpResponseMessage response, ILogger logger)
    {
        _response = response;
        _logger = logger;
    }

    public override async Task<string> ReadTextAsync()
    {
        var body = await _response.Content.ReadAsStringAsync();
        var parsed = JsonSerializer.Deserialize<OpenAIResponse>(body, JsonOptions);

        Usage = AiTokenUsage.From(parsed?.usage);

        return parsed?.choices?.FirstOrDefault()?.message?.content?.Trim() ?? string.Empty;
    }

    public override async IAsyncEnumerable<string> ReadDeltasAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var stream = await _response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // EndOfStream 은 뒤에서 동기로 읽어 버린다(CA2024). 토큰이 한 조각씩 오는
        // 이 고리에서는 조각마다 스레드를 붙잡는 셈이라, 끝 판정을 ReadLineAsync 의
        // null 로 대신한다 — 읽는 횟수도 조각당 두 번에서 한 번으로 준다.
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var json = line[6..].Trim();
            if (json == "[DONE]") break;

            OpenAIStreamResponse? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenAIStreamResponse>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    "스트림 조각을 읽지 못했습니다: {Error}, JSON: {Json}", ex.Message, json);
                continue;
            }

            // 토큰 수는 **맨 마지막 조각**에 실려 온다. 그 조각은 choices 가
            // 비어 있으므로 아래 글자 꺼내기가 어차피 건너뛴다 — 여기서
            // 먼저 줍지 않으면 그대로 버려진다.
            if (chunk?.usage is { } usage)
            {
                Usage = AiTokenUsage.From(usage);
            }

            var content = chunk?.choices?.FirstOrDefault()?.delta?.content;
            if (string.IsNullOrEmpty(content)) continue;

            yield return content;
        }
    }

    public override void Dispose() => _response.Dispose();
}

/// <summary>호출 한 번에 쓴 토큰. <b>공급자가 알려 준 값 그대로</b>다. 우리가 세지 않는다</summary>
/// <param name="PromptTokens">보낸 쪽. 대화가 길어질수록 커진다(문맥을 다시 올린다).</param>
/// <param name="CompletionTokens">답한 쪽.</param>
/// <param name="TotalTokens">둘의 합. 공급자가 준 값을 그대로 쓴다.</param>
public readonly record struct AiTokenUsage(
    int PromptTokens, int CompletionTokens, int TotalTokens)
{
    public static AiTokenUsage? From(OpenAIUsage? usage) =>
        usage is null
            ? null
            : new AiTokenUsage(usage.prompt_tokens, usage.completion_tokens, usage.total_tokens);
}
