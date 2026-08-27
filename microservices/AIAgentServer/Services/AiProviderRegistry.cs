using System.Collections.Concurrent;

namespace AIAgentServer.Services;

/// <summary>
/// AI 공급자 한 곳의 설정.
/// </summary>
/// <remarks>
/// 예전에는 설정이 <c>LLM:ApiBase</c> 하나뿐이라 <b>서버가 쓰는 LLM 이 한 개</b>였다.
/// 로컬 LLM 장비가 자주 꺼져 있어서 그때마다 AI 기능 전체가 멈췄다.
/// 그래서 공급자를 여러 개 두고 <b>사용자가 환경설정에서 고르게</b> 바꿨다.
/// </remarks>
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

    /// <summary>
    /// 한 번의 응답에 허용하는 최대 토큰.
    /// </summary>
    /// <remarks>
    /// 공급자마다 다르게 두는 이유가 있다. Groq 무료 한도는 <b>분당 토큰(TPM)</b> 으로도 걸린다.
    /// 넉넉하게 잡아 두면 답 한 번에 한도를 다 써 버려 다음 요청이 곧바로 막힌다.
    /// </remarks>
    public int MaxTokens { get; init; } = 2000;

    /// <summary>
    /// 이 공급자에게 허용하는 <b>응답 대기 시간</b>(초).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>생성에 걸리는 시간이다.</b> '장비가 꺼져 있어 못 붙는' 경우와 구분해야 한다 —
    /// 그쪽은 <c>AI:ConnectTimeoutSeconds</c> 가 담당한다. 이 값을 짧게 잡으면
    /// <b>정상적으로 오래 걸리는 생성이 끊긴다.</b> 로컬 LLM 은 모델을 메모리에
    /// 올리는 데만 수십 초가 걸릴 수 있어 넉넉해야 한다.
    /// </para>
    /// <para>
    /// 스트리밍은 <b>첫 응답까지</b>에만 걸린다. 답이 흘러나오기 시작한 뒤에는
    /// 총 시간으로 끊지 않는다 — 길게 답하는 것이 잘못은 아니다.
    /// </para>
    /// </remarks>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// <b>무료 모델만 쓰도록 강제할지.</b> OpenRouter 처럼 한 주소에 무료·유료가
    /// 섞여 있는 공급자에 켠다.
    /// </summary>
    /// <remarks>
    /// 켜면 <see cref="FreeModelGuard"/> 가 통과시킨 모델만 부르고, 요청 본문에도
    /// 유료 경로를 금지하는 값을 함께 보낸다. 사용자가 모델을 고르는 구조라
    /// 그 값은 <b>믿을 수 없는 입력</b>이므로 서버가 반드시 확인해야 한다.
    /// </remarks>
    public bool RequireFreeModel { get; init; }

    /// <summary>
    /// 모델 목록 주소. <see cref="RequireFreeModel"/> 이 켜진 경우에만 쓴다.
    /// </summary>
    public string ModelCatalogUrl { get; init; } = string.Empty;

    /// <summary>
    /// 사용자가 이 공급자의 모델을 골라도 되는지.
    /// </summary>
    /// <remarks>
    /// OpenRouter 만 켠다. 로컬 LLM 과 Groq 는 서버 설정이 정한 모델을 쓴다 —
    /// 그쪽은 고를 수 있게 해도 얻는 것이 없고, 잘못된 이름이 들어올 자리만 늘어난다.
    /// </remarks>
    public bool AllowModelChoice { get; init; }

    /// <summary>
    /// 지난 대화를 <b>몇 글자까지</b> 함께 보낼지. <c>0</c> 이면 자르지 않는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>기록을 보내는 것 자체는 뺄 수 없다.</b> <c>/v1/chat/completions</c> 는 세 공급자
    /// 모두 상태를 저장하지 않는다 — 서버가 이전 턴을 기억하는 것이 아니라
    /// <c>messages</c> 배열이 문맥의 전부다. 보내지 않으면 대화가 이어지지 않는다.
    /// </para>
    /// <para>
    /// <b>공급자마다 아픈 곳이 다르다.</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><b>Groq 무료</b> — 분당 토큰(TPM)으로 막힌다. 줄이면 바로 효과가 있다.</item>
    ///   <item>
    ///     <b>OpenRouter 무료</b> — 하루 요청 수가 한도다. 문맥은 수십만 토큰이
    ///     들어가므로 줄여도 얻는 것이 거의 없다.
    ///   </item>
    ///   <item><b>로컬 LLM</b> — 한도가 없다. 속도만 영향을 받는다.</item>
    /// </list>
    /// <para>
    /// 그래서 기본값을 <b>Groq 만 걸고 나머지는 0(무제한)</b> 으로 둔다.
    /// 글자를 재는 것은 토큰의 어림값이다. 언어마다 비율이 달라 정확하지 않지만,
    /// <b>개수(메시지 20개)로 재는 것보다는 훨씬 실제 비용에 가깝다.</b>
    /// </para>
    /// </remarks>
    public int MaxHistoryChars { get; init; }

    /// <summary>
    /// <b>한도에 걸렸을 때 대신 쓸 무료 모델들.</b> 적힌 순서대로 시도한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 카탈로그를 그냥 훑지 않는가.</b> OpenRouter 의 무료 모델은
    /// 지금 17개인데 <b>서로 대체 가능한 것이 아니다</b> — 코드 전용 모델,
    /// 안전성 분류 모델, 음악 생성 모델이 섞여 있다. 한도에 걸렸다고
    /// 분류 모델로 넘어가면 답은 나오지만 <b>쓸 수 없는 답</b>이 나온다.
    /// 조용히 그렇게 되는 것이 그냥 "한도 초과" 라고 말하는 것보다 나쁘다.
    /// </para>
    /// <para>
    /// 그래서 <b>사람이 고른 순서</b>를 설정에 둔다. 이 목록이 비면 바꿔치기는
    /// 일어나지 않고 예전처럼 한도 초과로 끝난다 — 즉 <b>이 기능은 설정으로 켜진다.</b>
    /// </para>
    /// <para>
    /// 여기 적혀 있어도 <see cref="RequireFreeModel"/> 이 켜진 공급자에서는
    /// <b>무료 확인을 다시 통과해야</b> 부른다. 목록에 유료 모델을 잘못 적어도
    /// 과금되지 않는다.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> FallbackModels { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 한 번의 요청에서 시도할 <b>모델 수의 상한</b>(첫 모델을 포함한다).
    /// </summary>
    /// <remarks>
    /// <b>시도마다 실제 요청이 나간다.</b> OpenRouter 무료는 하루 요청 수로도 한도가
    /// 걸리므로, 한 번 물어본 것이 5번의 호출이 되면 하루 몫을 다섯 배로 태운다.
    /// 사람은 기다리기도 한다 — 막힌 모델마다 왕복 시간이 쌓인다. 그래서 3으로 둔다.
    /// </remarks>
    public int MaxModelAttempts { get; init; } = 3;

    /// <summary>
    /// 하루에 허용하는 요청 수. <c>0</c> 이면 우리 쪽에서는 세지 않는다(기본).
    /// </summary>
    /// <remarks>
    /// <b>과금을 막는 장치가 아니다.</b> Groq 무료 플랜은 결제 수단이 없으면 애초에 과금되지
    /// 않고, 한도를 넘으면 공급자가 429 로 막는다. 이 값은 그보다 앞에서
    /// <b>우리가 스스로 멈추기 위한</b> 것이다 — 무료 한도를 다 태워 버려서
    /// 정작 필요할 때 못 쓰는 상황을 줄인다. 기본은 꺼져 있어 켜기 전과 똑같이 동작한다.
    /// </remarks>
    public int MaxRequestsPerDay { get; init; }

    /// <summary>설정이 채워져 있어 실제로 부를 수 있는 상태인지.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiBase)
        && !string.IsNullOrWhiteSpace(Model)
        && !IsPlaceholder(ApiKey);

    /// <summary>
    /// 키 자리에 자리표시자가 그대로 남아 있는지.
    /// </summary>
    /// <remarks>
    /// 추적 파일에는 <c>__SET_IN_appsettings.Local.json__</c> 를 넣어 둔다(결정 D1-B).
    /// 그것을 그대로 들고 요청하면 공급자가 401 을 주는데, 그러면 "키를 안 넣었다" 가 아니라
    /// "인증 실패" 로 보여서 원인을 잘못 짚게 된다. 부르기 전에 걸러 낸다.
    /// </remarks>
    private static bool IsPlaceholder(string? key)
    {
        return string.IsNullOrWhiteSpace(key)
            || key.StartsWith("__SET_IN_", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 설정에 적힌 AI 공급자들을 읽어 두고, 요청이 고른 공급자를 찾아 준다.
/// </summary>
/// <remarks>
/// <para>
/// <b>설정 모양.</b> <c>AI:DefaultProvider</c> 와 <c>AI:Providers:&lt;키&gt;:*</c> 를 읽는다.
/// </para>
/// <code>
/// "AI": {
///   "DefaultProvider": "jsini",
///   "Providers": {
///     "jsini": { "DisplayName": "JSINI (로컬 LLM)", "ApiBase": "...", "Model": "..." },
///     "groq":  { "DisplayName": "Groq Free",       "ApiBase": "...", "Model": "..." }
///   }
/// }
/// </code>
///
/// <para>
/// <b>옛 설정을 그대로 받아 준다.</b> 이 서비스는 원래 <c>LLM:ApiBase</c> · <c>LLM:ApiKey</c> ·
/// <c>LLM:Model</c> 세 개만 봤다. 그 값들은 각 개발 PC 의 <c>appsettings.Local.json</c>
/// (git 제외)에 들어 있어 저장소에서 한꺼번에 고칠 수가 없다. 그래서
/// <c>AI:Providers:jsini</c> 가 비어 있으면 <b>옛 <c>LLM:*</c> 를 jsini 공급자로 읽는다.</b>
/// 덕분에 이 변경으로 기존 개발 환경이 깨지지 않는다.
/// </para>
/// </remarks>
public sealed class AiProviderRegistry
{
    /// <summary>로컬 LLM. 이 저장소에서 원래 쓰던 것.</summary>
    public const string JsiniKey = "jsini";

    /// <summary>Groq 무료 플랜.</summary>
    public const string GroqKey = "groq";

    /// <summary>
    /// 접속(TCP 연결)까지 기다리는 시간. 기본 5초.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이것이 '장비 꺼짐' 을 빨리 알아채는 값이다.</b> 예전에는 이 설정이 없어
    /// 운영체제의 재시도에 맡겨졌고, 로컬 LLM 이 꺼져 있으면 <b>21초를 기다린 뒤</b>
    /// 실패했다. AI 기능을 쓰려던 사람은 그동안 아무 것도 못 한다 —
    /// 이 기능을 만든 이유가 "로컬이 자주 꺼진다" 인데 꺼진 상태의 체감이 가장 나빴다.
    /// </para>
    /// <para>
    /// <b>생성 시간과는 무관하다.</b> 연결이 맺어진 뒤에는 이 값이 적용되지 않으므로,
    /// 짧게 잡아도 오래 걸리는 생성이 끊기지 않는다. 그래서 공급자별로 나누지 않고
    /// 하나로 둔다 — 살아 있는 상대는 로컬이든 Groq 든 1초 안에 붙는다.
    /// </para>
    /// </remarks>
    public TimeSpan ConnectTimeout { get; }

    /// <summary>
    /// 접속에 실패했을 때 다른 공급자로 자동 전환할지 (결정 D-A3). 기본 켜짐.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>접속 실패만 넘긴다.</b> 이것이 조건의 핵심이다. 넘기지 <b>않는</b> 경우 —
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>생성 시간 초과</b> — 느린 답 하나가 양쪽 예산을 다 쓴다.
    ///     이미 <c>TimeoutSeconds</c> 만큼 기다린 뒤에 또 기다리게 된다.
    ///   </item>
    ///   <item>
    ///     <b>무료 한도 초과(429)</b> — 고장이 아니라 "잠시 못 쓴다" 는 안내다.
    ///     사람에게 알리고 기다리게 하는 것이 맞다. 몰래 다른 곳으로 넘기면
    ///     한도가 두 곳에서 소진된다.
    ///   </item>
    ///   <item>
    ///     <b>인증 실패(401)</b> — 설정 문제다. 넘겨서 답이 나오면
    ///     <b>키가 틀렸다는 사실이 영영 안 드러난다.</b>
    ///   </item>
    ///   <item>
    ///     <b>그 밖의 HTTP 오류</b> — 상대가 응답은 하고 있다. 접속 문제가 아니다.
    ///   </item>
    /// </list>
    /// <para>
    /// 즉 <b>"상대가 아예 없다" 는 것이 확실할 때만</b> 넘긴다. 그때는 넘기지 않아도
    /// 어차피 실패하므로 잃을 것이 없다.
    /// </para>
    /// </remarks>
    public bool FailoverOnConnectFailure { get; }

    private readonly Dictionary<string, AiProvider> _providers;
    private readonly string _defaultKey;

    /// <summary>
    /// 공급자별 '오늘 몇 번 불렀나'. <see cref="AiProvider.MaxRequestsPerDay"/> 를 켤 때만 쓴다.
    /// </summary>
    /// <remarks>
    /// 프로세스 메모리에만 둔다. 재기동하면 0 으로 돌아가고, 서버를 여러 대로 늘리면
    /// 대수만큼 곱해진다. 정확한 회계가 목적이 아니라 <b>폭주를 막는 것</b>이 목적이라
    /// 이 정도로 충분하다. 정확한 잔량은 공급자가 응답 헤더로 알려 준다.
    /// </remarks>
    private static readonly ConcurrentDictionary<string, DailyCounter> Counters = new();

    public AiProviderRegistry(IConfiguration config)
    {
        _providers = new Dictionary<string, AiProvider>(StringComparer.OrdinalIgnoreCase);

        var section = config.GetSection("AI:Providers");
        foreach (var child in section.GetChildren())
        {
            _providers[child.Key] = ReadProvider(child.Key, child);
        }

        // 옛 설정 되받기 — 위 설명 참고.
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

    /// <summary>
    /// 접속 실패 시 대신 시도할 공급자들. 순서대로 한 번씩 시도한다.
    /// </summary>
    /// <remarks>
    /// <b>설정이 끝난 것만 준다.</b> 키를 넣지 않은 공급자로 넘기면 "접속 실패" 가
    /// "설정 미완" 으로 바뀌기만 하고 사용자는 여전히 답을 못 받는다.
    /// 방금 실패한 공급자도 당연히 뺀다.
    /// </remarks>
    public IReadOnlyList<AiProvider> FailoverCandidates(string failedKey)
    {
        if (!FailoverOnConnectFailure) return Array.Empty<AiProvider>();

        return All
            .Where(p => p.IsConfigured
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

    /// <summary>
    /// 요청이 고른 공급자를 찾는다.
    /// </summary>
    /// <remarks>
    /// <b>모르는 값이면 기본으로 되돌린다.</b> 화면의 선택값은 사용자 브라우저에 저장돼
    /// 따라오는데, 공급자 이름을 나중에 바꾸면 옛 값을 든 브라우저가 남는다. 그때
    /// 오류를 내면 AI 기능이 통째로 멈춘다 — 조용히 기본으로 도는 편이 낫다.
    /// </remarks>
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

    /// <summary>
    /// 하루 상한을 확인하고 한 번 쓴 것으로 센다.
    /// </summary>
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

    /// <summary>
    /// 설정의 배열을 읽는다. 빈 항목은 버린다 — JSON 배열에 쉼표를 잘못 남기면
    /// 빈 문자열이 끼는데, 그것을 모델 이름으로 들고 가면 엉뚱한 오류가 된다.
    /// </summary>
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

    /// <summary>
    /// 0 은 받지 않는 값에 쓴다.
    /// </summary>
    /// <remarks>
    /// 대기 시간에 0 을 넣으면 <b>모든 요청이 즉시 실패</b>한다. 오타 한 번으로
    /// AI 기능이 통째로 죽는 셈이라, 그때는 기본값으로 돌린다.
    /// </remarks>
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
