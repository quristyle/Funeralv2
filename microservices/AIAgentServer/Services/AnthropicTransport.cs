using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using AIAgentServer.DTOs;
using Message = AIAgentServer.DTOs.Message;

namespace AIAgentServer.Services;

/// <summary>
/// Claude(Anthropic Messages API)에게 보내는 길. <b>공식 .NET SDK 를 쓴다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 따로 있는가.</b> 나머지 세 공급자(로컬 LLM · Groq · OpenRouter)는 전부
/// OpenAI 호환이라 <see cref="HttpClient"/> 하나로 같은 본문을 보내면 됐다.
/// Claude 는 형식이 다르다 — 아래 세 가지가 핵심이다.
/// </para>
/// <list type="number">
///   <item>
///     <b>지시(system)가 메시지가 아니다.</b> OpenAI 계열은 <c>messages</c> 배열에
///     <c>role: "system"</c> 을 끼워 넣지만, Claude 는 <b>요청의 별도 항목</b>으로 받는다.
///     그대로 보내면 400 이다. 그래서 여기서 뽑아 옮긴다.
///   </item>
///   <item>
///     <b>답이 블록 배열이다.</b> <c>choices[0].message.content</c> 가 아니라
///     <c>content</c> 안에 글자 블록 · 생각 블록이 섞여 온다.
///   </item>
///   <item>
///     <b>거절이 오류가 아니다.</b> 안전 분류기가 막으면 HTTP 200 에
///     <c>stop_reason: "refusal"</c> 로 온다. 상태 코드만 보면 성공으로 읽힌다.
///   </item>
/// </list>
/// <para>
/// <b>생각(thinking)은 끄지 않는다.</b> Claude Opus 5 는 기본으로 켜져 있고, 끄면
/// 생각 태그가 답에 새는 등 알려진 부작용이 있다. 대신 깊이를
/// <see cref="AiProvider.Effort"/> 로 조절한다 — 비용과 시간이 그쪽에 달려 있다.
/// </para>
/// <para>
/// <b>온도(temperature)는 보내지 않는다.</b> 이 모델 계열에서 빠진 값이라 보내면 400 이다.
/// 위층이 넘겨주는 값을 여기서 조용히 버린다.
/// </para>
/// </remarks>
public sealed class AnthropicTransport
{
    private readonly ILogger<AnthropicTransport> _logger;

    /// <summary>
    /// 공급자별 클라이언트. <b>요청마다 새로 만들지 않는다</b> — 안에 HttpClient 가 있어서
    /// 매번 세우면 소켓이 쌓인다.
    /// </summary>
    private readonly ConcurrentDictionary<string, AnthropicClient> _clients = new();

    public AnthropicTransport(ILogger<AnthropicTransport> logger) => _logger = logger;

    /// <summary>
    /// 한 번 보낸다. <b>실패는 <see cref="AiProviderException"/> 으로 바꿔 던진다</b> —
    /// 위층의 자동 전환과 모델 바꿔치기가 그 분류를 보고 판단하기 때문이다.
    /// </summary>
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

            // [스트리밍은 여기서 '첫 조각까지' 미리 당겨 온다]
            //
            // SDK 의 스트림은 게을러서, 그냥 돌려주면 **위층이 첫 글자를 읽을 때**
            // 비로소 요청이 나간다. 그러면 인증 실패나 한도 초과가
            // '이미 답을 흘리기 시작한 뒤' 에 터지고, 그 시점에는 공급자를 바꿀 수 없다.
            //
            // 자동 전환은 "한 조각도 내보내기 전에 끝난다" 가 약속이므로,
            // 첫 이벤트를 여기서 받아 두어 실패가 이 자리에서 터지게 한다.
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
            // 주소는 설정에 반드시 있다 — Preflight(IsConfigured)가 먼저 확인한다.
            // 경로(/v1/messages)는 SDK 가 붙이므로 호스트까지만 적혀 있다.
            return new AnthropicClient
            {
                ApiKey = provider.ApiKey,
                BaseUrl = provider.ApiBase,
            };
        });
    }

    /// <summary>
    /// OpenAI 식 메시지 목록을 <b>지시 하나 + 대화 차례들</b>로 가른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>지시는 합친다.</b> 목록 어디에 있든 전부 뽑아 줄바꿈으로 잇는다 — Claude 는
    /// 지시를 하나만 받는다.
    /// </para>
    /// <para>
    /// <b>앞뒤를 다듬는다.</b> Claude 는 첫 차례가 사용자여야 하고, 마지막이
    /// 어시스턴트이면 그것을 '이어서 쓰라는 시작 문구' 로 읽는다(이 모델 계열에서는
    /// 아예 400 이다). 대화 기록을 자르다 보면 둘 다 생길 수 있어 여기서 막는다.
    /// </para>
    /// </remarks>
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

            // 첫 차례가 어시스턴트이면 버린다. 문맥을 조금 잃지만, 400 으로 아무 답도
            // 못 받는 것보다는 낫다.
            if (turns.Count == 0 && isAssistant) continue;

            turns.Add(new MessageParam
            {
                Role = isAssistant ? Role.Assistant : Role.User,
                Content = m.content,
            });
        }

        // 마지막이 어시스턴트이면 떼어 낸다(위 설명 참고).
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

    /// <summary>
    /// 안전 분류기가 막은 경우를 <b>성공으로 읽지 않게</b> 한다.
    /// </summary>
    /// <remarks>
    /// HTTP 200 으로 오기 때문에 상태 코드만 보면 정상이다. 답 자리가 비어 있을 뿐이라,
    /// 거르지 않으면 "모델이 답을 만들지 못했습니다" 라는 <b>엉뚱한 안내</b>가 뜬다.
    /// </remarks>
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

    /// <summary>
    /// 멈춘 이유를 글자로 바꾼다. <b>없을 수 있다</b> — 스트리밍 도중 이벤트에는 안 실린다.
    /// </summary>
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

    /// <summary>
    /// SDK 예외를 <b>우리 분류</b>로 옮긴다. 위층은 이 분류만 보고 판단한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>429 를 '계정 전체' 로 본다.</b> Groq · OpenRouter 의 429 는 모델 하나가
    /// 붐비는 경우가 섞여 있어 모델을 바꿔 다시 불러 볼 값어치가 있었다. Anthropic 의
    /// 한도는 <b>조직 단위</b>라 모델을 바꿔도 똑같이 막힌다 — 바꿔 부르는 것은
    /// 요청만 더 태우는 짓이다. 그래서 곧바로 다른 공급자로 넘기게 표시한다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 흘러나오는 답. <b>첫 이벤트는 미리 받아 둔다</b>(<see cref="PrimeAsync"/>).
    /// </summary>
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

        /// <summary>
        /// 첫 이벤트를 당겨 온다. <b>요청이 실제로 나가는 순간이 여기다</b> —
        /// 인증 실패 · 한도 초과 · 접속 실패가 이 자리에서 터져야 위층이 공급자를 바꿀 수 있다.
        /// </summary>
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

            // 거절은 답이 비어서 온다. 여기까지 오면 헤더를 이미 보낸 뒤라 예외로
            // 알릴 수 없으므로 **답 자리에 사실대로 쓴다.**
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
