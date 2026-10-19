using System.Collections.Concurrent;

namespace AIAgentServer.Services;

/// <summary>AI 공급자 한 곳의 설정. 공급자는 사용자가 요청마다 고르며, 로컬 장비가 자주 꺼져 있어 여러 개를 둔다.</summary>
public sealed class AiProvider
{
    /// <summary>고르는 값. 화면에서 넘어오는 문자열과 같다(<c>jsini</c> · <c>groq</c>).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>사람에게 보여 줄 이름. 화면의 선택 목록과 상태 표시에 쓴다.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>대화 요청 주소. 경로까지 포함한다(<c>.../v1/chat/completions</c>).</summary>
    public string ApiBase { get; init; } = string.Empty;

    /// <summary>인증 키. <b>추적 파일에 넣지 않는다</b> — appsettings.Local.json 에만 둔다.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>쓸 모델 이름.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>말을 거는 형식. openai(기본) · anthropic. Anthropic 은 메시지·응답·스트리밍 형식이 달라 AnthropicTransport 로 가른다.</summary>
    public string Protocol { get; init; } = OpenAiProtocol;

    /// <summary>OpenAI 호환 공급자인지(로컬 LLM · Groq · OpenRouter).</summary>
    public bool IsAnthropic =>
        string.Equals(Protocol, AnthropicProtocol, StringComparison.OrdinalIgnoreCase);

    public const string OpenAiProtocol = "openai";
    public const string AnthropicProtocol = "anthropic";

    /// <summary>생각 깊이(low ~ max). Anthropic 전용, 비우면 공급자 기본값. 생각도 MaxTokens 를 같이 쓰므로 대화 기본은 medium.</summary>
    public string Effort { get; init; } = string.Empty;

    /// <summary>다른 공급자가 실패했을 때 이곳으로 넘겨도 되는지. 기본 허용. 유료(Anthropic)는 사람이 직접 고른 때만 부르도록 끈다.</summary>
    public bool AllowAutoFailover { get; init; } = true;

    /// <summary>한 번의 응답에 허용하는 최대 토큰. 넉넉히 잡으면 Groq 분당 토큰 한도를 한 번에 태워 다음 요청이 곧 막힌다.</summary>
    public int MaxTokens { get; init; } = 2000;

    /// <summary>응답 대기 시간(초, 생성 시간). 장비 꺼짐 감지는 AI:ConnectTimeoutSeconds 가 맡는다. 스트리밍은 첫 응답까지만 건다.</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>무료 모델만 쓰도록 강제할지(OpenRouter 처럼 무료·유료가 섞인 곳). 사용자가 고른 모델은 믿을 수 없는 입력이라 서버가 확인한다.</summary>
    public bool RequireFreeModel { get; init; }

    /// <summary>모델 목록 주소. RequireFreeModel 이 켜진 경우에만 쓴다.</summary>
    public string ModelCatalogUrl { get; init; } = string.Empty;

    /// <summary>사용자가 이 공급자의 모델을 골라도 되는지. OpenRouter 만 켠다(잘못된 이름이 들어올 자리만 늘어난다).</summary>
    public bool AllowModelChoice { get; init; }

    /// <summary>지난 대화를 몇 글자까지 함께 보낼지. 0 이면 자르지 않는다. 기록 전송 자체는 뺄 수 없다(세 공급자 모두 상태를 저장하지 않는다).
    /// Groq 는 분당 토큰이라 줄이면 효과가 있고, OpenRouter 는 하루 요청 수라 줄여도 얻는 것이 거의 없다.</summary>
    public int MaxHistoryChars { get; init; }

    /// <summary>한도에 걸렸을 때 대신 쓸 무료 모델들(적힌 순서대로). 카탈로그를 훑지 않는다 — 무료 모델 중에는 코드·안전 분류·음악 생성처럼
    /// 대체가 안 되는 것이 섞여 있어 쓸 수 없는 답이 나온다. 비우면 바꿔치기 없이 한도 초과로 끝난다.</summary>
    public IReadOnlyList<string> FallbackModels { get; init; } = Array.Empty<string>();

    /// <summary>한 요청에서 시도할 모델 수 상한(첫 모델 포함). 시도마다 실제 요청이 나가 하루 한도를 태우므로 3으로 둔다.</summary>
    public int MaxModelAttempts { get; init; } = 3;

    /// <summary>하루 허용 요청 수. 0 이면 우리 쪽에서는 세지 않는다. 과금을 막는 장치가 아니라 무료 한도를 다 태우기 전에 우리가 멈추는 장치다.</summary>
    public int MaxRequestsPerDay { get; init; }

    /// <summary>설정이 채워져 있어 실제로 부를 수 있는 상태인지.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiBase)
        && !string.IsNullOrWhiteSpace(Model)
        && !IsPlaceholder(ApiKey);

    /// <summary>키 자리에 자리표시자(__SET_IN_appsettings.Local.json__)가 남아 있는지. 그대로 보내면 401 이 나 「인증 실패」로 잘못 짚으므로 부르기 전에 거른다.</summary>
    private static bool IsPlaceholder(string? key)
    {
        return string.IsNullOrWhiteSpace(key)
            || key.StartsWith("__SET_IN_", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>설정의 AI 공급자들을 읽어 두고 요청이 고른 공급자를 찾는다. AI:Providers:&lt;키&gt; 를 읽고, 비어 있으면 옛 LLM:* 를 jsini 공급자로 받는다.</summary>
public sealed class AiProviderRegistry
{
    /// <summary>로컬 LLM. 이 저장소에서 원래 쓰던 것.</summary>
    public const string JsiniKey = "jsini";

    /// <summary>Groq 무료 플랜.</summary>
    public const string GroqKey = "groq";

    /// <summary>접속(TCP 연결) 대기 시간. 기본 5초. 장비 꺼짐을 빨리 알아채는 값이다(없으면 운영체제 재시도에 맡겨져 21초). 연결 뒤에는 적용되지 않는다.</summary>
    public TimeSpan ConnectTimeout { get; }

    /// <summary>접속에 실패했을 때만 다른 공급자로 자동 전환할지(결정 D-A3). 기본 켜짐. 생성 시간 초과 · 모델 하나의 붐빔 429 · 인증 실패 · 그 밖의 HTTP 오류는 넘기지 않는다.
    /// 계정 전체 하루 한도(429)는 넘긴다 — 기다려도 날짜가 바뀌어야 풀리고 모델을 바꿔도 소용없다. 확실하지 않으면 모델 한 개로 본다.</summary>
    public bool FailoverOnConnectFailure { get; }

    private readonly Dictionary<string, AiProvider> _providers;
    private readonly string _defaultKey;

    /// <summary>공급자별 오늘 호출 수. 프로세스 메모리에만 둔다 — 재기동하면 0, 서버를 늘리면 대수만큼 곱해진다. 폭주를 막는 용도라 이 정도로 충분하다.</summary>
    private static readonly ConcurrentDictionary<string, DailyCounter> Counters = new();

    public AiProviderRegistry(IConfiguration config)
    {
        _providers = new Dictionary<string, AiProvider>(StringComparer.OrdinalIgnoreCase);

        var section = config.GetSection("AI:Providers");
        foreach (var child in section.GetChildren())
        {
            _providers[child.Key] = ReadProvider(child.Key, child);
        }

        // 옛 LLM:* 설정을 jsini 공급자로 받는다.
        if (!_providers.TryGetValue(JsiniKey, out var jsini) || !jsini.IsConfigured)
        {
            var legacy = ReadLegacyLlmSection(config);
            if (legacy is not null) _providers[JsiniKey] = legacy;
        }

        var configuredDefault = config["AI:DefaultProvider"];
        _defaultKey = !string.IsNullOrWhiteSpace(configuredDefault)
            && _providers.ContainsKey(configuredDefault)
                ? configuredDefault
                : JsiniKey;

        // 0 이나 음수를 넣으면 끄는 것으로 본다(운영체제 기본에 맡긴다).
        var connectSeconds = ReadInt(config["AI:ConnectTimeoutSeconds"], 5);
        ConnectTimeout = connectSeconds > 0
            ? TimeSpan.FromSeconds(connectSeconds)
            : System.Threading.Timeout.InfiniteTimeSpan;

        FailoverOnConnectFailure =
            !bool.TryParse(config["AI:FailoverOnConnectFailure"], out var failover) || failover;
    }

    /// <summary>자동 전환 때 대신 시도할 공급자들(순서대로). 설정이 끝난 것만 주고, 방금 실패한 것과 AllowAutoFailover 가 꺼진 것은 뺀다.</summary>
    public IReadOnlyList<AiProvider> FailoverCandidates(string failedKey)
    {
        if (!FailoverOnConnectFailure) return Array.Empty<AiProvider>();

        return All
            .Where(p => p.IsConfigured
                && p.AllowAutoFailover
                && !string.Equals(p.Key, failedKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>화면에 보여 줄 공급자 목록. <b>키는 절대 내보내지 않는다.</b></summary>
    public IReadOnlyList<AiProvider> All => _providers.Values
        .OrderBy(p => p.Key == JsiniKey ? 0 : 1)
        .ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>설정에서 정한 기본 공급자의 키.</summary>
    public string DefaultKey => _defaultKey;

    /// <summary>요청이 고른 공급자를 찾는다. 모르는 값이면 기본으로 되돌린다 — 브라우저에 남은 옛 이름 때문에 AI 기능이 멈추면 안 된다.</summary>
    public AiProvider Resolve(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested)
            && _providers.TryGetValue(requested.Trim(), out var found))
        {
            return found;
        }

        return _providers.TryGetValue(_defaultKey, out var fallback)
            ? fallback
            : new AiProvider { Key = _defaultKey, DisplayName = _defaultKey };
    }

    /// <summary>하루 상한을 확인하고 한 번 쓴 것으로 센다.</summary>
    /// <returns>불러도 되면 <c>null</c>, 막아야 하면 사람에게 보여 줄 이유.</returns>
    public static string? TryConsumeDailyQuota(AiProvider provider)
    {
        if (provider.MaxRequestsPerDay <= 0) return null;

        var counter = Counters.GetOrAdd(provider.Key, _ => new DailyCounter());
        var used = counter.Increment(DateOnly.FromDateTime(DateTime.UtcNow));

        if (used > provider.MaxRequestsPerDay)
        {
            return $"{provider.DisplayName} 에 설정한 하루 요청 한도"
                + $"({provider.MaxRequestsPerDay}회)를 넘었습니다. 자정(UTC)에 초기화됩니다.";
        }

        return null;
    }

    /// <summary>오늘 몇 번 썼는지. 상태 화면이 보여 준다.</summary>
    public static int UsedToday(string providerKey)
    {
        return Counters.TryGetValue(providerKey, out var counter)
            ? counter.UsedOn(DateOnly.FromDateTime(DateTime.UtcNow))
            : 0;
    }

    private static AiProvider ReadProvider(string key, IConfiguration section)
    {
        return new AiProvider
        {
            Key = key,
            DisplayName = section["DisplayName"] ?? key,
            ApiBase = section["ApiBase"] ?? string.Empty,
            ApiKey = section["ApiKey"] ?? string.Empty,
            Model = section["Model"] ?? string.Empty,
            Protocol = section["Protocol"] ?? AiProvider.OpenAiProtocol,
            Effort = section["Effort"] ?? string.Empty,
            // 안 적으면 허용이다. 기존 공급자 설정을 고치지 않아도 지금과 똑같이 돈다.
            AllowAutoFailover =
                !bool.TryParse(section["AllowAutoFailover"], out var autoFailover) || autoFailover,
            MaxTokens = ReadInt(section["MaxTokens"], 2000),
            TimeoutSeconds = ReadPositiveInt(section["TimeoutSeconds"], 120),
            MaxRequestsPerDay = ReadInt(section["MaxRequestsPerDay"], 0),
            RequireFreeModel = bool.TryParse(section["RequireFreeModel"], out var free) && free,
            ModelCatalogUrl = section["ModelCatalogUrl"] ?? string.Empty,
            AllowModelChoice = bool.TryParse(section["AllowModelChoice"], out var choice) && choice,
            FallbackModels = ReadStringList(section.GetSection("FallbackModels")),
            MaxModelAttempts = ReadPositiveInt(section["MaxModelAttempts"], 3),
            MaxHistoryChars = ReadInt(section["MaxHistoryChars"], 0),
        };
    }

    /// <summary>설정 배열을 읽는다. 빈 항목은 버린다 — 쉼표 실수로 생긴 빈 문자열이 모델 이름이 되면 엉뚱한 오류가 난다.</summary>
    private static IReadOnlyList<string> ReadStringList(IConfiguration section)
    {
        return section.GetChildren()
            .Select(c => c.Value?.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToList();
    }

    private static AiProvider? ReadLegacyLlmSection(IConfiguration config)
    {
        var apiBase = config["LLM:ApiBase"];
        if (string.IsNullOrWhiteSpace(apiBase)) return null;

        return new AiProvider
        {
            Key = JsiniKey,
            DisplayName = "JSINI (로컬 LLM)",
            ApiBase = apiBase,
            ApiKey = config["LLM:ApiKey"] ?? string.Empty,
            Model = config["LLM:Model"] ?? string.Empty,
            MaxTokens = 2000,
        };
    }

    private static int ReadInt(string? raw, int fallback)
    {
        return int.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : fallback;
    }

    /// <summary>0 을 받지 않는 값에 쓴다. 대기 시간 0 이면 모든 요청이 즉시 실패하므로 기본값으로 돌린다.</summary>
    private static int ReadPositiveInt(string? raw, int fallback)
    {
        return int.TryParse(raw, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    /// <summary>날짜가 바뀌면 스스로 0 으로 돌아가는 카운터.</summary>
    private sealed class DailyCounter
    {
        private readonly object _gate = new();
        private DateOnly _day;
        private int _count;

        public int Increment(DateOnly today)
        {
            lock (_gate)
            {
                if (_day != today)
                {
                    _day = today;
                    _count = 0;
                }

                return ++_count;
            }
        }

        public int UsedOn(DateOnly today)
        {
            lock (_gate)
            {
                return _day == today ? _count : 0;
            }
        }
    }
}
