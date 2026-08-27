using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AIAgentServer.DTOs;

namespace AIAgentServer.Services;

/// <summary>
/// AI 공급자에게 실제로 물어보는 곳.
/// </summary>
/// <remarks>
/// 어느 공급자를 쓸지는 <b>요청마다</b> 정해진다. 사용자가 환경설정에서 고른 값이
/// 화면 → 게이트웨이 → 이 서비스로 그대로 넘어온다. 서버가 하나로 고정해 두지 않는
/// 이유는 로컬 LLM 장비가 자주 꺼져 있어서다 — 꺼져 있는 동안 Groq 로 옮겨 쓴다.
/// </remarks>
public interface ILLMService
{
    Task<string> SuggestCommonCodeAsync(
        string koreanName, bool natural = false, string? provider = null, string? model = null);
    Task<string> SuggestI18nTranslationAsync(
        string key, string targetLang, string? provider = null, string? model = null);
    /// <param name="allowFailover">
    /// 접속 실패 시 다른 공급자로 넘길지 (결정 D-A3). 기본은 넘긴다.
    /// <b>진단 목적이면 반드시 <c>false</c> 로 부른다</b> — 상태 화면의 '정밀 확인' 이
    /// 그렇다. 넘겨서 답이 나오면 "이 공급자가 정상" 이라고 잘못 보고하게 된다.
    /// </param>
    /// <param name="model">
    /// 쓸 모델. <b>공급자가 모델 선택을 허용할 때만</b> 쓰인다(지금은 OpenRouter).
    /// 믿을 수 없는 입력이라 서버가 무료 여부를 확인한다.
    /// </param>
    Task<string> ChatAsync(
        List<Message> messages,
        string? provider = null,
        bool allowFailover = true,
        string? model = null);
    IAsyncEnumerable<ChatStreamPart> StreamChatAsync(
        List<Message> messages, string? provider = null, string? model = null);
}

/// <summary>
/// 스트리밍으로 나가는 한 조각. <b>답인지 안내인지 구분해서 보낸다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 예전에는 안내("다른 공급자로 답합니다")도 답 글자와 똑같이 흘려보냈다. 그러면
/// <b>화면이 그것을 답의 일부로 저장하고 다음 턴에 문맥으로 다시 올려보낸다.</b>
/// 안내가 대화 기록에 쌓이고, 모델은 자기가 하지 않은 말을 자기 말로 읽는다.
/// </para>
/// <para>
/// 그래서 갈라 놓는다. 답은 문자열로, 안내는 객체로 나가고
/// 화면은 안내를 <b>말풍선 밖</b>에 따로 보여 준다(기록에 넣지 않는다).
/// </para>
/// </remarks>
public sealed record ChatStreamPart
{
    /// <summary>답 글자. 안내 조각이면 null.</summary>
    public string? Text { get; init; }

    /// <summary>사람에게 보여 줄 안내 한 줄. 답 조각이면 null.</summary>
    public string? Notice { get; init; }

    /// <summary>안내의 종류(<c>provider</c> · <c>model</c> · <c>history</c>). 화면이 아이콘을 가른다.</summary>
    public string? Kind { get; init; }

    public static ChatStreamPart Content(string text) => new() { Text = text };

    public static ChatStreamPart Info(string notice, string kind) =>
        new() { Notice = notice, Kind = kind };
}

public class LLMService : ILLMService
{
    /// <summary>대화 기본 성격. 공급자를 바꿔도 말투가 달라지지 않게 한곳에 둔다.</summary>
    private const string ChatSystemPrompt =
        "당신은 시스템 관리를 돕는 친절하고 전문적인 AI 어시스턴트입니다. 한국어로 자연스럽게 답변해주세요.";

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly AiProviderRegistry _registry;
    private readonly FreeModelGuard _freeModelGuard;
    private readonly ILogger<LLMService> _logger;

    public LLMService(
        HttpClient httpClient,
        AiProviderRegistry registry,
        FreeModelGuard freeModelGuard,
        ILogger<LLMService> logger)
    {
        _httpClient = httpClient;
        _registry = registry;
        _freeModelGuard = freeModelGuard;
        _logger = logger;
    }

    // ============================================================
    // 공개 동작 네 가지
    //
    // 네 개가 예전에는 각자 HttpClient 를 세우고 각자 오류를 처리했다(같은 코드 4벌).
    // 공급자가 둘로 늘면서 그 방식으로는 한도 처리·헤더 읽기를 네 곳에 똑같이
    // 넣어야 해서, 요청을 만드는 부분만 남기고 보내는 부분은 아래 한 곳으로 모았다.
    // ============================================================

    public async Task<string> ChatAsync(
        List<Message> messages,
        string? provider = null,
        bool allowFailover = true,
        string? model = null)
    {
        var target = _registry.Resolve(provider);
        EnsureSystemPrompt(messages, ChatSystemPrompt);

        // 대화는 약간의 창의성을 허용한다.
        var answer = await CompleteAsync(
            target, messages, temperature: 0.7, maxTokenCap: null, allowFailover, model);

        // 생각 블록은 답이 아니다. 스트리밍 쪽과 같은 판단을 여기서도 한다.
        var text = StripReasoning(answer.Text).Trim();

        if (text.Length == 0)
        {
            // 전부 생각이었다. **성공으로 돌려주면 안 된다** — 상태 화면의 '정밀 확인' 이
            // 이 경로를 쓰는데, 빈 답을 정상으로 보고하면 쓸 수 없는 모델이
            // '정상' 으로 보인다.
            throw new AiProviderException(
                $"{answer.Provider.DisplayName}({ShortModel(answer.Model)}) 이 생각 과정만 "
                + "돌려주고 답을 만들지 못했습니다. 환경설정에서 다른 모델을 골라 보세요.",
                answer.Provider.Key,
                model: answer.Model);
        }

        return text;
    }

    public async IAsyncEnumerable<ChatStreamPart> StreamChatAsync(
        List<Message> messages, string? provider = null, string? model = null)
    {
        var target = _registry.Resolve(provider);
        EnsureSystemPrompt(messages, ChatSystemPrompt);

        // 자동 전환은 **한 조각도 내보내기 전에** 끝난다. 접속 실패는 첫 응답을 받기
        // 전에 나므로, 답이 흘러나오기 시작한 뒤에 공급자가 바뀌는 일은 없다.
        var call = await SendWithFailoverAsync(
            target, messages, temperature: 0.7, maxTokenCap: null,
            HttpCompletionOption.ResponseHeadersRead, stream: true, requestedModel: model);

        using var response = call.Response;

        // 전환됐으면 **사용자에게 알린다.** 말없이 다른 모델로 답하면 "왜 말투가
        // 달라졌지" 를 설명할 방법이 없다. 답 앞에 한 줄만 붙인다.
        if (call.FailedOverFrom is { } from)
        {
            yield return ChatStreamPart.Info(
                $"{from.DisplayName} 에 접속할 수 없어 {call.Provider.DisplayName} 로 답합니다.",
                "provider");
        }
        else if (call.SwitchedFromModel is { } blockedModel)
        {
            // 공급자는 그대로인데 모델만 바뀐 경우. 사용자가 환경설정에서 고른 모델과
            // 다른 것이 답하고 있으므로 이것도 반드시 알려야 한다.
            yield return ChatStreamPart.Info(
                $"{ShortModel(blockedModel)} 모델이 한도에 걸려 "
                + $"{ShortModel(call.Model)} 모델로 답합니다.",
                "model");
        }

        // 오래된 대화를 잘라 보낸 경우. **모델이 앞 얘기를 못 본다**는 뜻이라
        // 답의 품질에 직접 영향이 있다 — 조용히 지나가면 안 된다.
        if (call.DroppedMessages > 0)
        {
            yield return ChatStreamPart.Info(
                $"길이 제한으로 오래된 대화 {call.DroppedMessages}개는 보내지 않았습니다.",
                "history");
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        // 생각 블록을 걷어낸다. 조각 경계에서 태그가 잘리므로 상태를 들고 가야 한다.
        var reasoning = new ReasoningFilter();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
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
                _logger.LogWarning("스트림 조각을 읽지 못했습니다: {Error}, JSON: {Json}", ex.Message, json);
                continue;
            }

            var content = chunk?.choices?.FirstOrDefault()?.delta?.content;
            if (string.IsNullOrEmpty(content)) continue;

            var visible = reasoning.Feed(content);
            if (visible.Length > 0) yield return ChatStreamPart.Content(visible);
        }

        var tail = reasoning.Flush();
        if (tail.Length > 0) yield return ChatStreamPart.Content(tail);

        // 전부 생각이었다. 빈 말풍선을 남기는 것보다 무엇을 해야 하는지 알려 주는 편이 낫다.
        //
        // 스트리밍은 헤더를 이미 보냈으므로 예외로 알릴 수 없다. 안내가 아니라
        // **답 자리**에 쓴다 — 사용자가 기다린 자리에 결과가 있어야 한다.
        if (!reasoning.EmittedAnything)
        {
            _logger.LogWarning(
                "{Provider}({Model}) 이 생각 과정만 돌려주었습니다.", call.Provider.Key, call.Model);

            yield return ChatStreamPart.Content(
                $"⚠️ {ShortModel(call.Model)} 모델이 생각 과정만 돌려주고 답을 만들지 못했습니다. "
                + "환경설정에서 다른 모델을 골라 보세요.");
        }
    }

    public async Task<string> SuggestCommonCodeAsync(
        string koreanName, bool natural = false, string? provider = null, string? model = null)
    {
        var target = _registry.Resolve(provider);

        var systemPrompt = natural
            ? "당신은 전문 번역가입니다. 입력된 한글 명칭을 보고, 소프트웨어 UI나 설명에 적합한 자연스럽고 매끄러운 영어 단어 또는 문장으로 번역하세요. 번역 시 적절하게 첫 글자 대문자화(Title Case) 등을 적용하고, 부연 설명 없이 오직 번역된 결과만 한 줄로 출력하세요."
            : "당신은 소프트웨어 엔지니어입니다. 입력된 한글 명칭을 보고, 프로그래밍 변수명으로 적합한 '영어 대문자 스네이크 케이스(SNAKE_CASE)' 코드로 변환하세요. 부연 설명 없이 오직 결과 코드만 한 줄로 출력하세요.";

        var answer = await CompleteAsync(
            target,
            new List<Message>
            {
                new() { role = "system", content = systemPrompt },
                new() { role = "user", content = koreanName },
            },
            // 창의성보다 정확성. 추천이 매번 달라지면 쓸 수가 없다.
            temperature: 0.1,
            // Reasoning 모델은 생각하는 동안에도 토큰을 쓴다. 한 줄 답이라도 넉넉히 준다.
            maxTokenCap: 1000,
            requestedModel: model);

        return CleanOneLiner(answer.Text, answer.Provider);
    }

    public async Task<string> SuggestI18nTranslationAsync(
        string key, string targetLang, string? provider = null, string? model = null)
    {
        var target = _registry.Resolve(provider);

        // 언어 코드는 짧게(ko · en) 관리한다. 예전 값("ko-KR")이 들어와도 받아 준다.
        var systemPrompt = targetLang.StartsWith("ko", StringComparison.OrdinalIgnoreCase)
            ? "당신은 다국어화(i18n) 번역 전문가입니다. 소프트웨어의 번역키를 입력받아, 이에 가장 어울리는 자연스럽고 표준적인 한국어 번역 결과(예: 번역키가 'ui.system.title'이면 '시스템 제목')를 한 줄로 추천하세요. 부연 설명 없이 오직 추천 결과 한 단어/문장만 출력하세요. 마크다운 기호 등을 붙이지 마세요."
            : "당신은 다국어화(i18n) 번역 전문가입니다. 소프트웨어의 번역키를 입력받아, 이에 가장 어울리는 자연스럽고 표준적인 영어 번역 결과(예: 번역키가 'ui.system.title'이면 'System Title')를 한 줄로 추천하세요. 부연 설명 없이 오직 추천 결과 한 단어/문장만 출력하세요. 마크다운 기호 등을 붙이지 마세요.";

        var answer = await CompleteAsync(
            target,
            new List<Message>
            {
                new() { role = "system", content = systemPrompt },
                new() { role = "user", content = key },
            },
            temperature: 0.1,
            maxTokenCap: 1000,
            requestedModel: model);

        return CleanOneLiner(answer.Text, answer.Provider);
    }

    // ============================================================
    // 실제로 보내는 곳
    // ============================================================

    /// <summary>한 번의 호출 결과. 실제로 답한 공급자와 모델을 함께 돌려준다.</summary>
    private readonly record struct AiAnswer(string Text, AiProvider Provider, string Model);

    private async Task<AiAnswer> CompleteAsync(
        AiProvider requested,
        List<Message> messages,
        double temperature,
        int? maxTokenCap,
        bool allowFailover = true,
        string? requestedModel = null)
    {
        var call = await SendWithFailoverAsync(
            requested, messages, temperature, maxTokenCap,
            HttpCompletionOption.ResponseContentRead, stream: false, allowFailover, requestedModel);

        using var response = call.Response;

        var body = await response.Content.ReadAsStringAsync();
        var parsed = JsonSerializer.Deserialize<OpenAIResponse>(body, JsonOptions);
        var reply = parsed?.choices?.FirstOrDefault()?.message?.content?.Trim();

        return new AiAnswer(
            string.IsNullOrEmpty(reply) ? "죄송합니다. 응답을 생성하지 못했습니다." : reply,
            call.Provider,
            call.Model);
    }

    private static OpenAIRequest BuildRequest(
        AiProvider provider,
        string model,
        List<Message> messages,
        double temperature,
        int? maxTokenCap,
        bool stream)
    {
        // 상한은 **공급자마다 다시 계산한다.** 자동 전환으로 공급자가 바뀌면
        // 그쪽의 MaxTokens 를 따라야 한다 — 로컬은 2000, Groq 는 1024 다.
        var maxTokens = maxTokenCap.HasValue
            ? Math.Min(maxTokenCap.Value, provider.MaxTokens)
            : provider.MaxTokens;

        return new OpenAIRequest
        {
            model = model,
            temperature = temperature,
            max_tokens = maxTokens,
            messages = messages,
            stream = stream,

            // [유료 경로 금지 — 두 번째 방어선]
            //
            // 모델 이름을 우리가 검사하는 것(FreeModelGuard)과 **별개**로,
            // 공급자에게도 "돈 드는 경로는 쓰지 마라" 를 본문으로 못 박는다.
            // 우리 판단이 틀렸거나 목록이 최신이 아니어도 여기서 막힌다.
            //
            // `allow_fallbacks` 를 반드시 꺼야 한다 — **OpenRouter 기본값이 true** 라서
            // 고른 경로가 막히면 알아서 다른 제공자로 넘기고, 그쪽이 무료라는 보장이 없다.
            //
            // 이 값을 필요 없는 공급자(로컬 · Groq)에게는 보내지 않는다(null → 직렬화 제외).
            provider = provider.RequireFreeModel ? new OpenRouterProviderPrefs() : null,
        };
    }

    /// <summary>
    /// 이 공급자로 부를 모델을 정한다. <b>과금되는 모델은 절대 통과시키지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 순서 — ① 사용자가 고른 모델 ② 설정의 기본 모델.
    /// 무료 강제가 켜진 공급자(OpenRouter)에서는 각 단계마다 확인한다.
    /// </para>
    /// <para>
    /// <b>사용자가 고른 모델이 무료가 아니면 부르지 않고 기본 모델로 돌린다.</b>
    /// OpenRouter 는 무료 목록을 수시로 바꾸므로, 어제 무료였던 모델이 오늘
    /// 사라지는 일이 정상적으로 일어난다. 그때 오류만 내면 AI 기능이 멈추고,
    /// 그대로 부르면 과금된다 — 둘 다 안 되므로 <b>안전한 기본값으로 바꿔서</b> 답한다.
    /// 바꿨다는 사실은 로그와 화면에 남긴다.
    /// </para>
    /// </remarks>
    private async Task<ModelChoice> ResolveModelAsync(AiProvider provider, string? requestedModel)
    {
        // 모델 선택을 허용하지 않는 공급자는 설정값만 쓴다.
        if (!provider.AllowModelChoice || string.IsNullOrWhiteSpace(requestedModel))
        {
            return await VerifyOrRejectAsync(provider, provider.Model, isFallback: false);
        }

        var wanted = requestedModel.Trim();

        // 설정 기본값과 같으면 따로 확인할 것이 없다.
        if (string.Equals(wanted, provider.Model, StringComparison.OrdinalIgnoreCase))
        {
            return await VerifyOrRejectAsync(provider, provider.Model, isFallback: false);
        }

        var chosen = await VerifyOrRejectAsync(provider, wanted, isFallback: false);
        if (chosen.Model is not null) return chosen;

        // 고른 모델이 무료가 아니다(또는 목록에서 사라졌다). 기본 모델로 돌린다.
        _logger.LogWarning(
            "{Provider}: 고른 모델 '{Wanted}' 을 쓸 수 없어 기본 모델 '{Fallback}' 로 바꿉니다. (사유: {Reason})",
            provider.Key, wanted, provider.Model, chosen.RejectReason);

        var fallback = await VerifyOrRejectAsync(provider, provider.Model, isFallback: true);
        return fallback with { ReplacedModel = wanted, RejectReason = chosen.RejectReason };
    }

    /// <summary>
    /// 이 공급자로 <b>순서대로 시도할 모델들</b>. 첫 번째가 사용자가 고른 것이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>고른 것을 반드시 먼저 쓴다.</b> 두 번째부터는 한도(429)에 걸렸을 때만 쓰인다 —
    /// 첫 모델이 답하면 나머지는 부르지 않는다.
    /// </para>
    /// <para>
    /// <b>지금 쉬는 모델은 뒤로 미룬다</b>(<see cref="AiModelCooldown"/>).
    /// <b>고른 모델도 예외가 아니다</b> — 방금 한도에 걸린 것을 알면서 또 부르는 것은
    /// 왕복 한 번과 하루 요청 한 개를 버리는 짓이다. 쉬는 것이 풀리면 다시 1순위가 된다.
    /// </para>
    /// <para>
    /// 빼지 않고 <b>미루기만</b> 하는 이유 — 쉬는 것은 우리 추측이고, 추측 때문에
    /// 쓸 모델이 하나도 없게 되면 안 된다. 전부 쉬는 중이면 그래도 순서대로 부른다.
    /// </para>
    /// <para>
    /// <b>무료 확인을 통과한 것만 담는다.</b> 설정 목록에 유료 모델이 잘못 적혀 있어도
    /// 여기서 걸러지므로 과금되지 않는다.
    /// </para>
    /// </remarks>
    private async Task<List<string>> ModelAttemptsAsync(AiProvider provider, string primary)
    {
        // 설정에 대체 목록이 없으면 바꿔치기를 하지 않는다. 이 기능은 설정으로 켜진다.
        // 그때는 쉬는 중이어도 고른 모델을 그대로 부른다 — 부를 다른 것이 없다.
        if (provider.FallbackModels.Count == 0 || provider.MaxModelAttempts <= 1)
        {
            return new List<string> { primary };
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ready = new List<string>();
        var resting = new List<string>();

        // 고른 모델이 맨 앞이다. 쉬는 중이 아니면 그대로 1순위가 된다.
        foreach (var candidate in new[] { primary }.Concat(provider.FallbackModels))
        {
            if (!seen.Add(candidate)) continue;

            // 유료 모델이 목록에 적혀 있어도 부르지 않는다.
            var verified = await VerifyOrRejectAsync(provider, candidate, isFallback: true);
            if (verified.Model is null)
            {
                _logger.LogWarning(
                    "{Provider}: 대체 모델 '{Model}' 을 쓸 수 없어 건너뜁니다. (사유: {Reason})",
                    provider.Key, candidate, verified.RejectReason);
                continue;
            }

            (AiModelCooldown.IsResting(provider.Key, verified.Model) ? resting : ready)
                .Add(verified.Model);
        }

        var attempts = ready.Concat(resting).ToList();

        // 하나도 남지 않는 경우(설정 목록이 전부 유료로 바뀐 경우)에도 부를 것은 있어야 한다.
        if (attempts.Count == 0) attempts.Add(primary);

        // 시도 횟수는 상한을 지킨다 — 한 번 물어본 것이 여러 번의 실제 호출이 된다.
        return attempts.Count > provider.MaxModelAttempts
            ? attempts.Take(provider.MaxModelAttempts).ToList()
            : attempts;
    }

    private async Task<ModelChoice> VerifyOrRejectAsync(
        AiProvider provider, string? model, bool isFallback)
    {
        if (!provider.RequireFreeModel)
        {
            return new ModelChoice(model, null, null, true, isFallback);
        }

        var verdict = await _freeModelGuard.VerifyAsync(model, provider.ModelCatalogUrl);

        return verdict.IsFree
            ? new ModelChoice(verdict.Model, null, null, verdict.VerifiedByCatalog, isFallback)
            : new ModelChoice(null, verdict.Reason, null, false, isFallback);
    }

    /// <summary>정해진 모델. 무료 확인 결과와 바꿔치기 여부를 함께 담는다.</summary>
    private readonly record struct ModelChoice(
        string? Model,
        string? RejectReason,
        string? ReplacedModel,
        bool VerifiedFreeByCatalog,
        bool IsFallback);

    /// <summary>
    /// 오래된 대화를 잘라 낸다. <b>공급자의 글자 예산에 맞춘다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 개수가 아니라 글자인가.</b> 화면은 예전에 '최근 20개' 를 보냈다. 그런데
    /// 개수는 비용의 단위가 아니다 — 짧은 20개와 긴 20개는 열 배 이상 차이 난다.
    /// 한도는 글자(토큰)로 걸리므로 재는 단위도 그것이어야 한다.
    /// </para>
    /// <para>
    /// <b>공급자마다 아픈 곳이 다르다.</b> Groq 무료는 <b>분당 토큰</b>으로 막히므로
    /// 기록을 줄이는 것이 바로 효과가 있다. OpenRouter 무료는 <b>하루 요청 수</b>가
    /// 한도이고 문맥은 수십만 토큰이 들어가므로 줄여도 얻는 것이 거의 없다.
    /// 그래서 예산을 공급자별 설정으로 두고, <c>0</c> 이면 자르지 않는다.
    /// </para>
    /// <para>
    /// <b>반드시 남기는 것</b> — 지시(<c>system</c>)와 <b>마지막 사용자 질문</b>이다.
    /// 지시가 빠지면 말투와 언어가 바뀌고, 질문이 빠지면 물어본 것이 없어진다.
    /// 마지막 질문 하나가 예산을 넘더라도 그것만은 보낸다(잘라서 보내면
    /// 질문이 달라진다 — 그것보다는 공급자가 거절하게 두는 편이 낫다).
    /// </para>
    /// <para>
    /// 자른 것은 <b>사용자에게 알린다.</b> 모델이 앞 얘기를 못 본다는 뜻이고,
    /// 그것은 답의 품질에 직접 영향이 있다.
    /// </para>
    /// </remarks>
    /// <returns>보낼 목록과, 버린 메시지 수.</returns>
    internal static (List<Message> Sent, int Dropped) TrimHistory(
        List<Message> messages, int maxChars)
    {
        if (maxChars <= 0 || messages.Count == 0) return (messages, 0);

        var total = messages.Sum(m => m.content?.Length ?? 0);
        if (total <= maxChars) return (messages, 0);

        // 지시는 개수도 적고 반드시 필요하다. 예산에서 먼저 뺀다.
        var systems = messages.Where(m => m.role == "system").ToList();
        var rest = messages.Where(m => m.role != "system").ToList();

        var budget = maxChars - systems.Sum(m => m.content?.Length ?? 0);

        // 최근 것부터 담는다.
        var keptReversed = new List<Message>();
        for (var i = rest.Count - 1; i >= 0; i--)
        {
            var len = rest[i].content?.Length ?? 0;

            // 마지막 하나(= 지금 물어본 것)는 예산을 넘더라도 담는다.
            if (keptReversed.Count == 0)
            {
                keptReversed.Add(rest[i]);
                budget -= len;
                continue;
            }

            if (budget - len < 0) break;

            keptReversed.Add(rest[i]);
            budget -= len;
        }

        keptReversed.Reverse();

        var sent = new List<Message>(systems.Count + keptReversed.Count);
        sent.AddRange(systems);
        sent.AddRange(keptReversed);

        return (sent, rest.Count - keptReversed.Count);
    }

    /// <summary>한 번 보낸 결과. 자동 전환이 있었으면 원래 공급자·모델도 담긴다.</summary>
    private readonly record struct AiCall(
        HttpResponseMessage Response,
        AiProvider Provider,
        AiProvider? FailedOverFrom,
        /// <summary>한도에 걸려 건너뛴 모델. 공급자는 그대로다. 없으면 null.</summary>
        string? SwitchedFromModel,
        /// <summary>실제로 답한 모델.</summary>
        string Model,
        /// <summary>길이 예산 때문에 보내지 않은 오래된 메시지 수. 0 이면 전부 보냈다.</summary>
        int DroppedMessages);

    /// <summary>
    /// 요청한 공급자로 보내고, <b>접속에 실패하면</b> 다른 공급자로 넘긴다 (결정 D-A3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>넘기는 조건은 하나다 — 상대에 아예 닿지 못했을 때.</b> 응답이 오기는 한 경우
    /// (429 한도 초과 · 401 인증 실패 · 5xx)와 생성 시간 초과는 그대로 올린다.
    /// 이유는 <c>AiProviderRegistry.FailoverOnConnectFailure</c> 주석에 적어 두었다.
    /// </para>
    /// <para>
    /// 닿지 못한 경우에는 <b>넘기지 않아도 어차피 실패</b>하므로 잃을 것이 없다.
    /// 접속 대기가 5초라 전환 비용도 그만큼이다.
    /// </para>
    /// </remarks>
    private async Task<AiCall> SendWithFailoverAsync(
        AiProvider requested,
        List<Message> messages,
        double temperature,
        int? maxTokenCap,
        HttpCompletionOption completionOption,
        bool stream,
        bool allowFailover = true,
        string? requestedModel = null)
    {
        AiProviderException? firstFailure = null;

        var candidates = new List<AiProvider> { requested };
        if (allowFailover)
        {
            candidates.AddRange(_registry.FailoverCandidates(requested.Key));
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var provider = candidates[i];

            try
            {
                var call = await SendToProviderAsync(
                    provider, messages, temperature, maxTokenCap, completionOption, stream,
                    // [모델은 후보마다 다시 정한다]
                    //
                    // 사용자가 고른 모델은 **그 공급자의 것**이다. 자동 전환으로 공급자가
                    // 바뀌면 그 이름은 쓸 수 없다(OpenRouter 모델 이름을 Groq 에 보내면 400).
                    // 그래서 전환된 뒤에는 그 공급자의 설정 모델을 쓴다.
                    i == 0 ? requestedModel : null,
                    // 진단(정밀 확인)은 모델도 바꾸지 않는다. 다른 모델이 대신 답하면
                    // 정작 고른 모델이 막혀 있는데 '정상' 으로 보고하게 된다 —
                    // 공급자 전환을 끄는 것과 같은 이유다.
                    allowModelRotation: allowFailover);

                // 첫 후보가 아니면 공급자가 전환된 것이다.
                return i == 0 ? call : call with { FailedOverFrom = requested };
            }
            catch (AiProviderException ex) when (ex.IsConnectFailure && i + 1 < candidates.Count)
            {
                firstFailure ??= ex;
                var next = candidates[i + 1];

                _logger.LogWarning(
                    "{From} 에 접속하지 못해 {To} 로 자동 전환합니다. (사유: {Reason})",
                    provider.Key, next.Key, ex.Message);

                AiUsageTracker.RecordFailover(from: provider.Key, to: next.Key);
            }
        }

        // 여기까지 왔다면 마지막 후보도 실패했고, 그 예외가 이미 밖으로 나갔다.
        // 방어적으로만 둔다 — 후보 목록은 항상 하나 이상이다.
        throw firstFailure ?? new AiProviderException(
            $"{requested.DisplayName} 호출에 실패했습니다.", requested.Key);
    }

    /// <summary>
    /// 공급자 한 곳에 보낸다. <b>모델이 한도에 걸리면 다음 무료 모델로 바꿔 다시 보낸다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>공급자 전환과는 다른 층이다.</b> 공급자 전환은 '상대에 닿지 못했을 때' 만
    /// 하고 한도는 넘기지 않는다(그러면 두 곳의 한도가 같이 준다). 반면
    /// <b>같은 공급자 안에서 모델을 바꾸는 것은 한도에 걸렸을 때가 바로 그 상황이다</b> —
    /// OpenRouter 의 무료 모델은 상류 제공자가 각각 다르므로, 한 모델이 붐빈다고
    /// 다른 모델이 붐빈 것은 아니다.
    /// </para>
    /// <para>
    /// <b>바꾸지 않는 경우</b> —
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>계정 전체 한도</b>(<c>free-models-per-day</c>). 무료 모델이 그 한도를
    ///     공유하므로 바꿔도 똑같이 막힌다. 시도만 더 태운다.
    ///   </item>
    ///   <item>
    ///     <b>접속 실패 · 인증 실패 · 생성 시간 초과 · 그 밖의 오류.</b> 모델을 바꿔서
    ///     해결되는 문제가 아니다. 특히 접속 실패는 <b>공급자</b> 전환의 조건이므로
    ///     여기서 삼키지 않고 그대로 올려야 한다.
    ///   </item>
    /// </list>
    /// <para>
    /// 마지막 모델까지 한도에 걸리면 그 예외를 올린다. 그때도 <b>걸린 모델은 쉬게
    /// 표시해 둔다</b> — 다음 요청이 같은 벽부터 다시 치지 않도록.
    /// </para>
    /// </remarks>
    private async Task<AiCall> SendToProviderAsync(
        AiProvider provider,
        List<Message> messages,
        double temperature,
        int? maxTokenCap,
        HttpCompletionOption completionOption,
        bool stream,
        string? requestedModel,
        bool allowModelRotation)
    {
        var choice = await ResolveModelAsync(provider, requestedModel);

        if (choice.Model is null)
        {
            // 무료 확인을 통과한 모델이 없다. **부르지 않는다.**
            throw new AiProviderException(
                $"{provider.DisplayName} 에서 쓸 수 있는 무료 모델이 없습니다. "
                + choice.RejectReason,
                provider.Key);
        }

        if (choice.ReplacedModel is not null)
        {
            AiUsageTracker.RecordModelSubstitution(
                provider.Key,
                from: choice.ReplacedModel,
                to: choice.Model,
                reason: choice.RejectReason ?? "무료 모델이 아님");
        }

        var attempts = allowModelRotation
            ? await ModelAttemptsAsync(provider, choice.Model)
            : new List<string> { choice.Model };

        // [기록 자르기는 공급자마다 다시 한다]
        //
        // 예산이 공급자별 설정이기 때문이다. 자동 전환으로 로컬(무제한)에서
        // Groq(분당 토큰 제한)로 넘어가면 그쪽 예산에 맞춰 다시 잘라야 한다.
        // **원본을 고치지 않는다** — 위 호출자가 같은 목록을 다음 후보에게도 넘긴다.
        var (toSend, dropped) = TrimHistory(messages, provider.MaxHistoryChars);

        if (dropped > 0)
        {
            _logger.LogInformation(
                "{Provider}: 길이 예산({Budget}자)에 맞춰 오래된 대화 {Dropped}개를 보내지 않습니다.",
                provider.Key, provider.MaxHistoryChars, dropped);
        }

        for (var i = 0; i < attempts.Count; i++)
        {
            var model = attempts[i];
            var payload = BuildRequest(
                provider, model, toSend, temperature, maxTokenCap, stream);

            try
            {
                var response = await SendOnceAsync(provider, payload, completionOption, model);

                return new AiCall(
                    response,
                    provider,
                    FailedOverFrom: null,
                    // [무엇과 비교하는가]
                    //
                    // '시도 목록의 첫 번째' 가 아니라 **원래 쓰려던 모델**과 비교한다.
                    // 이미 쉬는 중이라 처음부터 밀려난 경우에는 첫 시도에서 성공하는데,
                    // 그때도 사용자가 고른 것과는 다른 모델이 답한 것이므로 알려야 한다.
                    SwitchedFromModel:
                        string.Equals(model, choice.Model, StringComparison.OrdinalIgnoreCase)
                            ? null
                            : choice.Model,
                    Model: model,
                    DroppedMessages: dropped);
            }
            catch (AiProviderException ex) when (ex.IsRateLimited && !ex.IsAccountWideLimit)
            {
                // 이 모델은 지금 못 쓴다. 다음 요청이 헛되게 다시 부르지 않도록 표시한다.
                AiModelCooldown.Rest(
                    provider.Key, model, ex.RetryAfterSeconds, ex.Message);

                if (i + 1 >= attempts.Count) throw;

                var next = attempts[i + 1];

                _logger.LogWarning(
                    "{Provider}: 모델 '{From}' 이 한도에 걸려 '{To}' 로 바꿔 시도합니다. (사유: {Reason})",
                    provider.Key, model, next, ex.Message);

                AiUsageTracker.RecordModelRotation(
                    provider.Key, from: model, to: next, reason: ex.Message);
            }
        }

        // 위 루프는 반드시 돌려주거나 던진다. 방어적으로만 둔다.
        throw new AiProviderException(
            $"{provider.DisplayName} 호출에 실패했습니다.", provider.Key);
    }

    /// <summary>
    /// 요청을 보내고, 실패는 원인별로 갈라서 던진다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>다시 시도하지 않는다.</b> 실패했다고 자동으로 재요청하면 무료 한도를 두 배로
    /// 태우고, 429 를 맞은 상태에서는 한도가 풀리는 시각만 뒤로 밀린다.
    /// 사람이 다시 누르게 두는 편이 맞다.
    /// </para>
    /// <para>
    /// <b>남은 한도를 로그에 남긴다.</b> Groq 는 <c>x-ratelimit-remaining-*</c> 헤더로
    /// 잔량을 항상 알려 준다. 이것을 안 보면 "갑자기 안 되네" 하는 순간에야 한도를 알게 된다.
    /// </para>
    /// </remarks>
    private async Task<HttpResponseMessage> SendOnceAsync(
        AiProvider provider,
        OpenAIRequest payload,
        HttpCompletionOption completionOption,
        string model)
    {
        if (!provider.IsConfigured)
        {
            throw new AiProviderException(
                $"{provider.DisplayName} 설정이 완료되지 않았습니다. "
                + "appsettings.Local.json 의 AI:Providers 항목(주소 · 키 · 모델)을 확인하세요.",
                provider.Key);
        }

        // 우리 쪽 하루 상한(기본 꺼짐). 공급자를 부르기 전에 막는다.
        var quotaBlock = AiProviderRegistry.TryConsumeDailyQuota(provider);
        if (quotaBlock is not null)
        {
            throw new AiProviderException(quotaBlock, provider.Key, isRateLimited: true);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, provider.ApiBase);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        request.Content = JsonContent.Create(payload);

        // 응답 대기 시간은 공급자마다 다르다. HttpClient 는 하나라서 요청마다 여기서 건다.
        //
        // **스트리밍은 첫 응답까지만 걸린다.** `ResponseHeadersRead` 로 부르므로
        // SendAsync 는 헤더가 오면 돌아오고, 그 뒤 본문이 흘러나오는 동안에는
        // 이 시계가 관여하지 않는다 — 길게 답하는 것이 잘못은 아니다.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(provider.TimeoutSeconds));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, completionOption, cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            sw.Stop();
            // 닿지도 못한 것도 기록한다 — 상태 화면에서 "이 공급자를 쓰려다 실패했다" 가 보인다.
            AiUsageTracker.Record(
                provider.Key, ok: false, latencyMs: (int)sw.ElapsedMilliseconds,
                null, null, null, null, null, null);

            // 원인을 가른다. 사람이 할 일이 다르다 —
            // 접속 실패는 '장비를 켜라', 시간 초과는 '기다리거나 다른 모델을 골라라'.
            if (cts.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "{Provider} 응답 시간 초과({Timeout}초). 모델: {Model}",
                    provider.Key, provider.TimeoutSeconds, model);

                throw new AiProviderException(
                    $"{provider.DisplayName} 이 {provider.TimeoutSeconds}초 안에 응답하지 않았습니다. "
                    + "잠시 뒤에 다시 시도하거나 환경설정에서 다른 AI 모델을 선택하세요.",
                    provider.Key);
            }

            // 접속 자체가 안 됐다. 로컬 LLM 에서 가장 흔한 경우이고 할 일이 분명하다.
            //
            // ConnectTimeout 이 걸리면 .NET 이 영어 문장을 담아 준다
            // ("A connection could not be established within the configured ConnectTimeout.").
            // 그것을 그대로 올리면 화면에 영어가 뜨고, 무엇을 해야 하는지도 알 수 없다.
            if (ex.GetBaseException() is TimeoutException)
            {
                var seconds = _registry.ConnectTimeout == Timeout.InfiniteTimeSpan
                    ? null
                    : $"{_registry.ConnectTimeout.TotalSeconds:0}초 안에 ";

                throw new AiProviderException(
                    $"{provider.DisplayName} 에 {seconds}접속하지 못했습니다. "
                    + "장비가 꺼져 있거나 주소가 잘못됐을 수 있습니다.",
                    provider.Key,
                    isConnectFailure: true);
            }

            // 그 밖의 연결 실패(주소 오류 · DNS · 인증서 …). 원인 문장을 함께 올린다.
            // 이것도 '닿지 못한' 경우이므로 자동 전환 대상이다.
            throw new AiProviderException(
                $"{provider.DisplayName} 에 연결할 수 없습니다. ({ex.GetBaseException().Message})",
                provider.Key,
                isConnectFailure: true);
        }

        sw.Stop();
        RecordUsage(provider, response, (int)sw.ElapsedMilliseconds);
        LogRemainingQuota(provider, response);

        // 성공하면 **응답을 그대로 넘긴다.** 여기서 using 으로 잡으면 부르는 쪽이
        // 본문을 읽기 전에 닫혀 버린다(스트리밍은 특히 그렇다). 닫는 것은 호출자 몫이다.
        if (response.IsSuccessStatusCode) return response;

        // 실패했다. 응답에서 필요한 것을 **먼저 다 꺼낸 뒤** 닫는다.
        var error = await response.Content.ReadAsStringAsync();
        var status = response.StatusCode;
        var retryAfter = ReadRetryAfterSeconds(response);
        response.Dispose();

        // ── 무료 한도 초과 ──────────────────────────────────
        // Groq 무료 플랜은 한도를 넘으면 429 로 막는다. **넘겨서 과금되는 것이 아니라
        // 차단된다.** 그래서 이것은 고장이 아니라 "잠시 못 쓴다" 는 안내다.
        if (status == HttpStatusCode.TooManyRequests)
        {
            var wait = retryAfter is null ? "잠시" : $"{retryAfter}초";
            var accountWide = LooksAccountWideLimit(error);

            _logger.LogWarning(
                "{Provider}({Model}) 한도 초과(429). 범위: {Scope}. {Wait} 후 재시도 가능. 응답: {Error}",
                provider.Key, model, accountWide ? "계정 전체" : "이 모델", wait, Truncate(error));

            // [공급자가 말한 이유를 함께 올린다]
            //
            // 429 가 늘 "하루 한도를 다 썼다" 는 뜻은 아니다. OpenRouter 의 무료 모델은
            // **상류 제공자가 일시적으로 붐빌 때**도 429 를 준다
            // ("… is temporarily rate-limited upstream. Please retry shortly").
            // 두 경우에 사람이 할 일이 다르다 — 몇 초 뒤 재시도 vs 내일까지 대기.
            // 우리가 한 문장으로 뭉개면 그 구분이 사라진다.
            var providerReason = ExtractProviderError(error);

            // 계정 전체 한도라면 **모델을 바꿔도 소용없다**는 것을 문구에 담는다.
            // 그러지 않으면 사용자는 다른 모델을 골라 보며 남은 몫을 더 태운다.
            var advice = accountWide
                ? $"이 계정의 무료 하루 한도를 다 썼습니다. 모델을 바꿔도 같은 한도를 쓰므로 "
                    + $"{wait} 후에 다시 시도하거나 다른 공급자(Groq · 로컬 LLM)를 고르세요."
                : $"{wait} 후에 다시 시도하거나 환경설정에서 다른 AI 모델을 선택하세요.";

            throw new AiProviderException(
                $"{provider.DisplayName} 이 요청을 받지 않았습니다(한도). {advice}"
                + (providerReason is null ? "" : $" (공급자 설명: {providerReason})"),
                provider.Key,
                statusCode: 429,
                retryAfterSeconds: retryAfter,
                isRateLimited: true,
                isAccountWideLimit: accountWide,
                model: model);
        }

        // ── 인증 실패 ──────────────────────────────────────
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogError(
                "{Provider} 인증 실패({Status}). 응답: {Error}",
                provider.Key, (int)status, Truncate(error));

            throw new AiProviderException(
                $"{provider.DisplayName} 인증에 실패했습니다. API 키를 확인하세요.",
                provider.Key,
                statusCode: (int)status);
        }

        _logger.LogError(
            "{Provider} 호출 실패({Status}). 모델: {Model}, 응답: {Error}",
            provider.Key, (int)status, model, Truncate(error));

        throw new AiProviderException(
            $"{provider.DisplayName} 호출이 실패했습니다. (HTTP {(int)status})",
            provider.Key,
            statusCode: (int)status);
    }

    /// <summary>
    /// 응답 헤더의 한도 정보를 기록해 둔다. 상태 화면이 읽는다.
    /// </summary>
    /// <remarks>
    /// 사용량을 알려고 따로 호출하지 않는다 — <b>그 호출 자체가 한도를 깎는다.</b>
    /// 실제 요청이 오갈 때 지나가는 헤더를 줍는다(<see cref="AiUsageTracker"/>).
    /// </remarks>
    private static void RecordUsage(AiProvider provider, HttpResponseMessage response, int latencyMs)
    {
        AiUsageTracker.Record(
            provider.Key,
            ok: response.IsSuccessStatusCode,
            latencyMs: latencyMs,
            limitRequests: ReadHeader(response, "x-ratelimit-limit-requests"),
            remainingRequests: ReadHeader(response, "x-ratelimit-remaining-requests"),
            limitTokens: ReadHeader(response, "x-ratelimit-limit-tokens"),
            remainingTokens: ReadHeader(response, "x-ratelimit-remaining-tokens"),
            resetRequests: ReadHeader(response, "x-ratelimit-reset-requests"),
            resetTokens: ReadHeader(response, "x-ratelimit-reset-tokens"));
    }

    /// <summary>
    /// 남은 무료 한도를 로그에 남긴다. 잔량이 적을 때만 경고로 올린다.
    /// </summary>
    private void LogRemainingQuota(AiProvider provider, HttpResponseMessage response)
    {
        var remainingRequests = ReadHeader(response, "x-ratelimit-remaining-requests");
        var remainingTokens = ReadHeader(response, "x-ratelimit-remaining-tokens");

        if (remainingRequests is null && remainingTokens is null) return;

        // 요청 잔량이 두 자리로 떨어지면 곧 막힌다. 그때는 눈에 띄게 남긴다.
        var nearlyOut = int.TryParse(remainingRequests, out var left) && left <= 20;

        if (nearlyOut)
        {
            _logger.LogWarning(
                "{Provider} 무료 한도가 얼마 남지 않았습니다. 남은 요청 {Requests}회, 남은 토큰 {Tokens}",
                provider.Key, remainingRequests, remainingTokens ?? "?");
            return;
        }

        _logger.LogDebug(
            "{Provider} 남은 한도 — 요청 {Requests}, 토큰 {Tokens}",
            provider.Key, remainingRequests ?? "?", remainingTokens ?? "?");
    }

    private static int? ReadRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return (int)Math.Ceiling(delta.TotalSeconds);
        }

        // Groq 는 소수(예: "7.66")로도 보낸다. TimeSpan 파싱이 실패하는 값이라 직접 읽는다.
        var raw = ReadHeader(response, "retry-after");
        return double.TryParse(raw, out var seconds)
            ? (int)Math.Ceiling(seconds)
            : null;
    }

    private static string? ReadHeader(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    /// <summary>
    /// 한 줄 추천(공통코드 · 다국어)의 답을 다듬는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 결과는 화면을 지나 DB 에 들어간다.</b> 공통코드 값과 번역문이 된다.
    /// 그래서 모델이 지시를 안 지켰을 때 그것을 그대로 통과시키면 안 된다.
    /// </para>
    /// <para>
    /// <b>추론(reasoning) 모델의 생각 블록을 걷어낸다.</b> 일부 모델은 답 앞에
    /// <c>&lt;think&gt;…&lt;/think&gt;</c> 로 생각을 먼저 쏟아낸다. 그 길이가
    /// <c>max_tokens</c> 를 넘으면 <b>생각이 잘린 채 그것이 답으로 돌아온다</b> —
    /// 실제로 Groq 의 <c>qwen/qwen3.6-27b</c> 로 확인했다. 걸러 내지 않으면
    /// "Here's a thinking process…" 같은 문장이 공통코드로 저장된다.
    /// </para>
    /// <para>
    /// 태그를 지운 뒤 남는 것이 없으면 <b>빈 결과로 다룬다.</b> 잘린 생각을
    /// 억지로 답이라고 내놓기보다, 모델을 바꾸라고 알려 주는 편이 맞다.
    /// </para>
    /// </remarks>
    private static string CleanOneLiner(string answer, AiProvider provider)
    {
        var cleaned = StripReasoning(answer);

        // 혹시 모델이 마크다운(예: `CODE`)을 넣었을 경우를 대비한 후처리.
        cleaned = cleaned.Replace("`", "").Trim();

        // 한 줄만 쓴다. 지시를 어기고 설명을 덧붙인 경우 첫 줄만 취한다.
        var firstLine = cleaned
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);

        if (string.IsNullOrEmpty(firstLine))
        {
            throw new AiProviderException(
                $"{provider.DisplayName}({provider.Model}) 이 쓸 수 있는 결과를 돌려주지 않았습니다. "
                + "생각 과정만 반환하는 모델일 수 있습니다 — 설정의 Model 을 바꿔 보세요.",
                provider.Key);
        }

        return firstLine;
    }

    /// <summary>
    /// <c>&lt;think&gt;</c> 블록을 지운다. 닫히지 않은 것(토큰이 모자라 잘린 경우)은
    /// 그 뒤 전부를 버린다 — 뒤에 남은 것은 답이 아니라 잘린 생각의 조각이다.
    /// </summary>
    private static string StripReasoning(string answer)
    {
        const string open = "<think>";
        const string close = "</think>";

        var result = answer;
        while (true)
        {
            var start = result.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;

            var end = result.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
            result = end < 0
                ? result[..start]
                : result[..start] + result[(end + close.Length)..];
        }

        return result;
    }

    private static void EnsureSystemPrompt(List<Message> messages, string prompt)
    {
        if (messages.Any(m => m.role == "system")) return;
        messages.Insert(0, new Message { role = "system", content = prompt });
    }

    /// <summary>
    /// 오류 본문에서 <b>사람이 읽을 수 있는 이유</b>만 뽑는다. 없으면 null.
    /// </summary>
    /// <remarks>
    /// OpenAI 규격은 <c>{ "error": { "message": "..." } }</c> 다. OpenRouter 는 상류
    /// 제공자가 준 원문을 <c>error.metadata.raw</c> 에 한 겹 더 담아 주는데, 그쪽이
    /// 훨씬 구체적이다("… temporarily rate-limited upstream. Please retry shortly").
    /// 그래서 <c>raw</c> 를 먼저 본다.
    /// </remarks>
    private static string? ExtractProviderError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var err)
                || err.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (err.TryGetProperty("metadata", out var meta)
                && meta.ValueKind == JsonValueKind.Object
                && meta.TryGetProperty("raw", out var raw)
                && raw.GetString() is { Length: > 0 } rawText)
            {
                return Truncate(rawText, 200);
            }

            return err.TryGetProperty("message", out var msg)
                && msg.GetString() is { Length: > 0 } msgText
                    ? Truncate(msgText, 200)
                    : null;
        }
        catch (JsonException)
        {
            // 형식을 모르면 문구를 덧붙이지 않는다. 원문은 이미 로그에 남았다.
            return null;
        }
    }

    /// <summary>공급자 오류 본문이 길 때가 있다. 로그를 덮지 않게 자른다.</summary>
    private static string Truncate(string value, int max = 500)
    {
        return value.Length <= max ? value : value[..max] + "…";
    }

    /// <summary>
    /// 429 가 <b>계정 전체</b> 한도인지, <b>모델 하나</b>가 붐빈 것인지 가른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 구분에 따라 모델을 바꿔 볼지가 갈린다. 무료 모델들은 <b>계정의 하루 한도를
    /// 공유</b>하므로, 그 한도에 걸린 상태에서 모델을 바꿔 시도하는 것은
    /// 남은 몫을 태우기만 한다.
    /// </para>
    /// <para>
    /// <b>문구로 판단한다.</b> HTTP 상태는 둘 다 429 이고 헤더로도 구분되지 않는다.
    /// OpenRouter 가 쓰는 표현 —
    /// </para>
    /// <list type="table">
    ///   <item><term>계정 전체</term><description>
    ///     "Rate limit exceeded: free-models-per-day" · 크레딧을 넣으라는 안내
    ///   </description></item>
    ///   <item><term>모델 하나</term><description>
    ///     "… is temporarily rate-limited upstream. Please retry shortly"
    ///   </description></item>
    /// </list>
    /// <para>
    /// <b>모르면 '모델 하나' 로 본다.</b> 그쪽으로 틀리면 요청 몇 개를 더 쓰는 데 그치지만,
    /// 반대로 틀리면 바꿔 부르면 답이 나왔을 상황을 그냥 실패로 끝낸다.
    /// 문구가 바뀌면 이 목록만 손보면 된다.
    /// </para>
    /// </remarks>
    private static bool LooksAccountWideLimit(string errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody)) return false;

        string[] markers =
        {
            "free-models-per-day",
            "requests-per-day",
            "daily limit",
            "per day",
            "add 10 credits",
        };

        return markers.Any(m => errorBody.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 모델 이름을 짧게. 사용자에게 보이는 한 줄 안내에 쓴다.
    /// </summary>
    /// <remarks>
    /// <c>google/gemma-4-31b-it:free</c> 처럼 제공사와 접미사가 붙어 길다.
    /// 환경설정의 선택 목록도 같은 방식으로 줄여 보여 주므로 표기가 맞는다.
    /// </remarks>
    private static string ShortModel(string model)
    {
        var trimmed = model.Replace(":free", "", StringComparison.OrdinalIgnoreCase);
        var slash = trimmed.LastIndexOf('/');
        return slash >= 0 && slash + 1 < trimmed.Length ? trimmed[(slash + 1)..] : trimmed;
    }
}
