using System.Runtime.CompilerServices;
using System.Text.Json;
using AIAgentServer.DTOs;

namespace AIAgentServer.Services;

/// <summary>
/// 공급자가 돌려준 답. <b>형식이 다른 두 계열을 같은 모양으로 읽게 해 준다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 예전에는 <see cref="LLMService"/> 가 <see cref="HttpResponseMessage"/> 를 직접 들고
/// 본문을 <c>choices[0].message.content</c> 로, 스트림을 <c>data: {...}</c> 줄로 읽었다.
/// 공급자가 전부 OpenAI 호환이라 그래도 됐다.
/// </para>
/// <para>
/// <b>Claude 를 붙이면서 그 가정이 깨졌다.</b> Anthropic Messages API 는 응답이
/// <c>content</c> 블록 배열이고 스트리밍 이벤트도 다르다. 게다가 공식 SDK 를 쓰므로
/// <c>HttpResponseMessage</c> 자체가 없다 — 타입이 붙은 객체가 나온다.
/// </para>
/// <para>
/// 그래서 <b>"답을 읽는 방법" 만 추상으로 남긴다.</b> 자동 전환 · 모델 바꿔치기 ·
/// 기록 자르기 · 생각 블록 걷어내기는 전부 위층에 그대로 있고, 이 아래만 갈린다.
/// </para>
/// </remarks>
public abstract class AiResponseBody : IDisposable
{
    /// <summary>완성된 답 전체를 읽는다. 스트리밍이 아닌 호출에서 쓴다.</summary>
    public abstract Task<string> ReadTextAsync();

    /// <summary>
    /// 답을 조각으로 읽는다. <b>생각 블록을 걷어내기 전의 날 글자</b>를 흘린다 —
    /// 거르는 것은 <see cref="ReasoningFilter"/> 의 몫이라 여기서 하지 않는다.
    /// </summary>
    public abstract IAsyncEnumerable<string> ReadDeltasAsync(
        CancellationToken cancellationToken = default);

    public virtual void Dispose() { }
}

/// <summary>
/// OpenAI 호환 공급자(로컬 LLM · Groq · OpenRouter)의 답.
/// </summary>
/// <remarks>
/// <b>내용은 예전 코드 그대로다.</b> <see cref="LLMService"/> 안에 흩어져 있던 파싱을
/// 옮겨 온 것이고 판단은 하나도 바꾸지 않았다.
/// </remarks>
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

            var content = chunk?.choices?.FirstOrDefault()?.delta?.content;
            if (string.IsNullOrEmpty(content)) continue;

            yield return content;
        }
    }

    public override void Dispose() => _response.Dispose();
}
