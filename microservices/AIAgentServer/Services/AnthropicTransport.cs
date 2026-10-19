using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using AIAgentServer.DTOs;
using Message = AIAgentServer.DTOs.Message;

namespace AIAgentServer.Services;

/// <summary>Claude(Anthropic Messages API)에게 보내는 길. 공식 .NET SDK 를 쓴다. 나머지 공급자는 OpenAI 호환이라 HttpClient 로 충분하지만,
/// Claude 는 지시가 별도 항목이고 답이 블록 배열이며 거절이 HTTP 200 으로 온다. 생각(thinking)은 끄지 않고 Effort 로 조절하며, temperature 는 보내지 않는다(400).</summary>
public sealed class AnthropicTransport
{
    private readonly ILogger<AnthropicTransport> _logger;

    /// <summary>공급자별 클라이언트. 요청마다 새로 만들면 안의 HttpClient 로 소켓이 쌓인다.</summary>
    private readonly ConcurrentDictionary<string, AnthropicClient> _clients = new();

    public AnthropicTransport(ILogger<AnthropicTransport> logger) => _logger = logger;

    /// <summary>한 번 보낸다. 실패는 AiProviderException 으로 바꿔 던진다 — 위층의 자동 전환과 모델 바꿔치기가 그 분류로 판단한다.</summary>
    public async Task<AiResponseBody> SendAsync(
        AiProvider provider,
        string model,
        List<Message> messages,
        int maxTokens,
        bool stream,
        CancellationToken cancellationToken)
    {
        var client = GetClient(provider);
        var (system, turns) = Split(messages);

        var effort = ParseEffort(provider.Effort);
        var hasSystem = !string.IsNullOrWhiteSpace(system);

        // 이 값들은 만들 때만 넣을 수 있다(init 전용). 그래서 먼저 정하고 한 번에 세운다.
        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = maxTokens,
            Messages = turns,

            // 지시가 있을 때만 넣는다. 빈 문자열도 하나의 지시로 읽힌다.
            System = hasSystem ? (MessageCreateParamsSystem)system : null,

            // 비우면 공급자 기본값(high)을 따른다.
            OutputConfig = effort is null ? null : new OutputConfig { Effort = effort },
        };

        try
        {
            if (!stream)
            {
                var reply = await client.Messages.Create(parameters, cancellationToken);
                EnsureNotRefused(provider, model, AsText(reply.StopReason), reply.StopDetails);
                return new AnthropicResponseBody(ExtractText(reply));
            }

            // 스트림은 게을러서 그냥 돌려주면 위층이 첫 글자를 읽을 때 요청이 나간다. 인증·한도 실패가 답을 흘린 뒤에 터지면 공급자를 못 바꾸므로,
            // 첫 이벤트를 여기서 받아 실패를 이 자리에서 터뜨린다.
            var body = new AnthropicStreamBody(client, parameters, provider, model, _logger);
            await body.PrimeAsync(cancellationToken);
            return body;
        }
        catch (AiProviderException)
        {
            throw; // 이미 분류가 끝난 것은 그대로 올린다.
        }
        catch (Exception ex)
        {
            throw Translate(ex, provider, model);
        }
    }

    private AnthropicClient GetClient(AiProvider provider)
    {
        // 키가 바뀌면 다른 클라이언트다. 설정을 고치고 재기동하면 어차피 새로 만들어진다.
        var cacheKey = $"{provider.Key}|{provider.ApiBase}|{provider.ApiKey.GetHashCode()}";

        return _clients.GetOrAdd(cacheKey, _ =>
        {
            // 주소는 설정에 반드시 있다(Preflight 가 확인). 경로는 SDK 가 붙이므로 호스트까지만 적는다.
            return new AnthropicClient
            {
                ApiKey = provider.ApiKey,
                BaseUrl = provider.ApiBase,
            };
        });
    }

    /// <summary>OpenAI 식 메시지 목록을 지시 하나와 대화 차례들로 가른다. 지시는 전부 모아 줄바꿈으로 잇고(Claude 는 하나만 받는다),
    /// 대화 기록을 자르면 생기는 첫 어시스턴트 차례와 마지막 어시스턴트 차례를 떼어 낸다(이어 쓰기 시작 문구로 읽혀 400).</summary>
    private static (string System, List<MessageParam> Turns) Split(List<Message> messages)
    {
        var systems = messages
            .Where(m => string.Equals(m.role, "system", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.content?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c));

        var turns = new List<MessageParam>();
        foreach (var m in messages)
        {
            if (string.Equals(m.role, "system", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrWhiteSpace(m.content)) continue;

            var isAssistant =
                string.Equals(m.role, "assistant", StringComparison.OrdinalIgnoreCase);

            // 첫 차례가 어시스턴트면 버린다. 문맥을 조금 잃어도 400 으로 아무 답도 못 받는 것보다 낫다.
            if (turns.Count == 0 && isAssistant) continue;

            turns.Add(new MessageParam
            {
                Role = isAssistant ? Role.Assistant : Role.User,
                Content = m.content,
            });
        }

        // 마지막이 어시스턴트이면 떼어 낸다.
        while (turns.Count > 0 && turns[^1].Role == Role.Assistant)
        {
            turns.RemoveAt(turns.Count - 1);
        }

        return (string.Join("\n\n", systems), turns);
    }

    /// <summary>답 블록 중 <b>글자만</b> 모은다. 생각 블록은 답이 아니라 버린다.</summary>
    internal static string ExtractText(Anthropic.Models.Messages.Message reply)
    {
        return string.Concat(
            reply.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
    }

    /// <summary>안전 분류기가 막은 답을 성공으로 읽지 않게 한다. HTTP 200 이라 상태 코드로는 정상이고, 거르지 않으면 「모델이 답을 만들지 못했습니다」라는 엉뚱한 안내가 뜬다.</summary>
    private static void EnsureNotRefused(
        AiProvider provider, string model, string? stopReason, object? stopDetails)
    {
        if (!string.Equals(stopReason, "refusal", StringComparison.OrdinalIgnoreCase)) return;

        throw new AiProviderException(
            $"{provider.DisplayName} 이 이 요청에 답하지 않기로 했습니다(안전 정책). "
            + "질문을 바꾸거나 환경설정에서 다른 AI 를 골라 보세요."
            + (stopDetails is null ? "" : $" (분류: {stopDetails})"),
            provider.Key,
            model: model);
    }

    /// <summary>멈춘 이유를 글자로 바꾼다. 스트리밍 도중 이벤트에는 없을 수 있다.</summary>
    private static string? AsText(ApiEnum<string, StopReason>? stopReason) =>
        stopReason is { } value ? (string)value : null;

    /// <summary>설정의 글자를 SDK 값으로 바꾼다. 모르는 값이면 공급자 기본값에 맡긴다.</summary>
    private static Effort? ParseEffort(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => null,
    };

    /// <summary>SDK 예외를 우리 분류로 옮긴다. 429 는 계정 전체 한도로 본다 — Anthropic 한도는 조직 단위라 모델을 바꿔도 막히므로 곧바로 다른 공급자로 넘긴다.</summary>
    private AiProviderException Translate(Exception ex, AiProvider provider, string model)
    {
        switch (ex)
        {
            case AnthropicRateLimitException rate:
                _logger.LogWarning(
                    "{Provider}({Model}) 한도 초과(429). 응답: {Error}",
                    provider.Key, model, rate.Message);

                return new AiProviderException(
                    $"{provider.DisplayName} 이 요청을 받지 않았습니다(한도). "
                    + "이 한도는 계정 전체에 걸리므로 모델을 바꿔도 같습니다. "
                    + "잠시 뒤에 다시 시도하거나 다른 공급자를 고르세요.",
                    provider.Key,
                    statusCode: 429,
                    isRateLimited: true,
                    isAccountWideLimit: true,
                    model: model);

            case AnthropicUnauthorizedException:
            case AnthropicForbiddenException:
                _logger.LogError("{Provider} 인증 실패. 응답: {Error}", provider.Key, ex.Message);

                return new AiProviderException(
                    $"{provider.DisplayName} 인증에 실패했습니다. API 키를 확인하세요.",
                    provider.Key,
                    statusCode: 401);

            // 상대에 닿지 못한 경우. 자동 전환 대상이다.
            case AnthropicIOException:
            case HttpRequestException:
                return new AiProviderException(
                    $"{provider.DisplayName} 에 연결할 수 없습니다. ({ex.GetBaseException().Message})",
                    provider.Key,
                    isConnectFailure: true);

            // 시간 초과는 **넘기지 않는다** — 느린 답 하나가 양쪽 예산을 다 쓴다.
            case OperationCanceledException:
            case TimeoutException:
                return new AiProviderException(
                    $"{provider.DisplayName} 이 {provider.TimeoutSeconds}초 안에 응답하지 않았습니다. "
                    + "잠시 뒤에 다시 시도하거나 환경설정에서 다른 AI 를 선택하세요.",
                    provider.Key);

            case AnthropicApiException api:
                // 529 Overloaded 는 고장이 아니라 지금 붐빈다. 한도(429)와 달리 내 몫을 다 쓴 게 아니므로 예비 모델·다른 공급자로 넘긴다(AiProviderException).
                if (LooksOverloaded(api.Message))
                {
                    _logger.LogWarning(
                        "{Provider}({Model}) 이 지금 붐빕니다. 응답: {Error}",
                        provider.Key, model, api.Message);

                    return new AiProviderException(
                        $"{provider.DisplayName} 이 지금 붐벼 답하지 못했습니다. "
                        + "잠시 뒤에 다시 시도하거나 환경설정에서 다른 AI 를 선택하세요.",
                        provider.Key,
                        isTransientOverload: true,
                        model: model);
                }

                _logger.LogError(
                    "{Provider} 호출 실패. 모델: {Model}, 응답: {Error}",
                    provider.Key, model, api.Message);

                return new AiProviderException(
                    $"{provider.DisplayName} 호출이 실패했습니다. ({api.Message})",
                    provider.Key);

            default:
                _logger.LogError(ex, "{Provider} 호출 중 예상 못한 오류", provider.Key);

                return new AiProviderException(
                    $"{provider.DisplayName} 호출이 실패했습니다. ({ex.GetBaseException().Message})",
                    provider.Key);
        }
    }

    /// <summary>오류 문구가 지금 붐빈다고 말하는지(529 의 Overloaded). LLMService.LooksOverloaded 와 같은 판단이다.</summary>
    private static bool LooksOverloaded(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        string[] markers = { "overload", "high demand", "capacity", "529" };

        return markers.Any(m => message.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>이미 다 받아 둔 답. 스트리밍이 아닌 호출에서 쓴다.</summary>
    private sealed class AnthropicResponseBody : AiResponseBody
    {
        private readonly string _text;

        public AnthropicResponseBody(string text) => _text = text;

        public override Task<string> ReadTextAsync() => Task.FromResult(_text);

        public override async IAsyncEnumerable<string> ReadDeltasAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            if (_text.Length > 0) yield return _text;
        }
    }

    /// <summary>흘러나오는 답. 첫 이벤트는 미리 받아 둔다(PrimeAsync).</summary>
    private sealed class AnthropicStreamBody : AiResponseBody
    {
        private readonly AnthropicClient _client;
        private readonly MessageCreateParams _parameters;
        private readonly AiProvider _provider;
        private readonly string _model;
        private readonly ILogger _logger;

        private IAsyncEnumerator<RawMessageStreamEvent>? _events;
        private bool _exhausted;
        private string? _refusal;

        public AnthropicStreamBody(
            AnthropicClient client,
            MessageCreateParams parameters,
            AiProvider provider,
            string model,
            ILogger logger)
        {
            _client = client;
            _parameters = parameters;
            _provider = provider;
            _model = model;
            _logger = logger;
        }

        /// <summary>첫 이벤트를 당겨 온다. 요청이 실제로 나가는 순간이라, 인증·한도·접속 실패가 여기서 터져야 위층이 공급자를 바꿀 수 있다.</summary>
        public async Task PrimeAsync(CancellationToken cancellationToken)
        {
            _events = _client.Messages
                .CreateStreaming(_parameters, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            _exhausted = !await _events.MoveNextAsync();
        }

        public override async Task<string> ReadTextAsync()
        {
            var buffer = new System.Text.StringBuilder();
            await foreach (var part in ReadDeltasAsync())
            {
                buffer.Append(part);
            }

            return buffer.ToString().Trim();
        }

        public override async IAsyncEnumerable<string> ReadDeltasAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_events is null)
            {
                throw new InvalidOperationException(
                    "PrimeAsync 를 먼저 불러야 한다. 그러지 않으면 실패가 답을 흘린 뒤에 터진다.");
            }

            // 미리 받아 둔 첫 이벤트부터 처리하고, 그 다음부터 이어 읽는다.
            var more = !_exhausted;
            while (more)
            {
                foreach (var text in Read(_events.Current))
                {
                    yield return text;
                }

                more = await _events.MoveNextAsync();
            }

            // 거절은 답이 비어서 온다. 헤더를 이미 보낸 뒤라 예외로 알릴 수 없으므로 답 자리에 사실대로 쓴다.
            if (_refusal is not null)
            {
                _logger.LogWarning(
                    "{Provider}({Model}) 이 안전 정책으로 답하지 않았습니다. 분류: {Category}",
                    _provider.Key, _model, _refusal);

                yield return $"⚠️ {_provider.DisplayName} 이 이 요청에 답하지 않기로 "
                    + "했습니다(안전 정책). 질문을 바꾸거나 다른 AI 를 골라 보세요.";
            }
        }

        /// <summary>이벤트 하나에서 꺼낼 글자. 생각 블록과 그 밖의 이벤트는 흘려보낸다.</summary>
        private IEnumerable<string> Read(RawMessageStreamEvent current)
        {
            if (current.TryPickContentBlockDelta(out var block)
                && block!.Delta.TryPickText(out var text)
                && !string.IsNullOrEmpty(text!.Text))
            {
                yield return text.Text;
            }
            else if (current.TryPickDelta(out var messageDelta)
                && string.Equals(
                    AsText(messageDelta!.Delta.StopReason),
                    "refusal",
                    StringComparison.OrdinalIgnoreCase))
            {
                _refusal = "refusal";
            }
        }

        public override void Dispose()
        {
            // IAsyncEnumerator 는 동기로 닫을 수 없다. 끝까지 읽었으면 이미 닫혀 있고,
            // 중간에 그만둔 경우에만 정리가 필요하다.
            if (_events is null) return;
            _ = _events.DisposeAsync().AsTask();
            _events = null;
        }
    }
}
