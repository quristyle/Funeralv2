namespace AIAgentServer.Services;

/// <summary>
/// AI 공급자 호출이 실패했을 때 던진다. 사람에게 그대로 보여 줄 문구를 담는다.
/// </summary>
/// <remarks>
/// 예전에는 <c>throw new Exception("LLM API request failed.")</c> 였다. 화면에는
/// "요청 실패" 만 떠서, <b>장비가 꺼진 것인지 · 키가 틀린 것인지 · 무료 한도를 넘긴 것인지</b>
/// 구분할 수 없었다. 원인마다 사람이 할 일이 완전히 다르므로 나눠서 담는다.
/// </remarks>
public sealed class AiProviderException : Exception
{
    public AiProviderException(
        string message,
        string providerKey,
        int? statusCode = null,
        int? retryAfterSeconds = null,
        bool isRateLimited = false,
        bool isConnectFailure = false,
        bool isAccountWideLimit = false,
        bool isTransientOverload = false,
        string? model = null)
        : base(message)
    {
        ProviderKey = providerKey;
        StatusCode = statusCode;
        RetryAfterSeconds = retryAfterSeconds;
        IsRateLimited = isRateLimited;
        IsConnectFailure = isConnectFailure;
        IsAccountWideLimit = isAccountWideLimit;
        IsTransientOverload = isTransientOverload;
        Model = model;
    }

    /// <summary>어느 공급자에서 났는지. 여러 개를 쓰므로 이것 없이는 로그를 못 읽는다.</summary>
    public string ProviderKey { get; }

    /// <summary>공급자가 준 HTTP 상태. 우리 쪽에서 막은 경우엔 없다.</summary>
    public int? StatusCode { get; }

    /// <summary>다시 시도해도 되는 시각까지 남은 초. 429 응답의 <c>retry-after</c> 값.</summary>
    public int? RetryAfterSeconds { get; }

    /// <summary>
    /// 무료 한도에 걸린 것인지.
    /// </summary>
    /// <remarks>
    /// 화면이 이 값으로 문구를 가른다. 한도 초과는 <b>고장이 아니다</b> —
    /// 잠시 뒤에 다시 하거나 다른 공급자를 고르면 되는 상황이라, 오류처럼 보이면 안 된다.
    /// </remarks>
    public bool IsRateLimited { get; }

    /// <summary>
    /// <b>상대에 아예 닿지 못한</b> 경우인지 (장비 꺼짐 · 접속 시간 초과 · DNS 실패).
    /// </summary>
    /// <remarks>
    /// 자동 전환(결정 D-A3)의 <b>유일한</b> 조건이다. 응답이 오기는 한 경우
    /// (429 · 401 · 5xx)와 생성 시간 초과는 여기 해당하지 않는다 —
    /// 이유는 <c>AiProviderRegistry.FailoverOnConnectFailure</c> 주석에 적어 두었다.
    /// </remarks>
    public bool IsConnectFailure { get; }

    /// <summary>
    /// 한도가 <b>모델 하나가 아니라 계정 전체</b>에 걸린 것인지.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 429 를 두 갈래로 나눠야 한다. <b>사람이 할 일도, 우리가 할 일도 다르다.</b>
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>모델 하나가 붐빈다</b> (OpenRouter 의 상류 제공자 혼잡:
    ///     "… is temporarily rate-limited upstream"). 다른 무료 모델은 멀쩡하므로
    ///     <b>모델을 바꿔 부르면 답이 나온다.</b>
    ///   </item>
    ///   <item>
    ///     <b>계정의 무료 하루 한도를 다 썼다</b> ("Rate limit exceeded:
    ///     free-models-per-day"). 이 한도는 <b>무료 모델 전체가 공유</b>하므로
    ///     모델을 바꿔도 똑같이 막힌다 — 바꿔 시도하는 것은 남은 요청만 태우는 짓이다.
    ///   </item>
    /// </list>
    /// <para>
    /// 구분은 공급자가 준 오류 문구로 한다(<see cref="LLMService"/> 의 표).
    /// <b>문구에 의존하는 판단이므로 확실하지 않으면 '모델 한 개' 로 본다</b> —
    /// 그쪽이 틀려도 요청 몇 개를 더 쓰는 데 그치지만, 반대로 틀리면
    /// 바꿔 부르면 됐을 상황에서 그냥 실패로 끝난다.
    /// </para>
    /// </remarks>
    public bool IsAccountWideLimit { get; }

    /// <summary>
    /// <b>지금 붐빌 뿐</b>인지 (상류가 5xx 로 "high demand" 를 돌려준 경우).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 429 와 따로 두나.</b> 「이 모델이 지금 못 받는다」는 뜻은 모델 혼잡
    /// 429 와 같지만, <b>공급자가 그것을 429 로 주지 않는다.</b> Gemini 는 503 에
    /// "This model is currently experiencing high demand" 를 담아 준다. 상태 코드만
    /// 보면 그것이 「그 밖의 HTTP 오류」로 떨어져서 <b>모델도 공급자도 바꾸지 않고
    /// 그대로 실패</b>했다 — 설정해 둔 예비 모델이 멀쩡한데도.
    /// </para>
    /// <para>
    /// <b>한도(<see cref="IsRateLimited"/>)로 뭉개지 않는 이유</b>는 사람에게 할 말이
    /// 다르기 때문이다. 한도는 "내 몫을 다 썼다"(기다리거나 내일), 혼잡은 "남의
    /// 사정이다"(몇 초 뒤면 대개 풀린다). 화면 문구와 로그가 그 둘을 갈라야 한다.
    /// </para>
    /// <para>
    /// <b>우리가 하는 일은 429 와 같다</b> — 그 모델을 쉬게 하고 다음 예비 모델로
    /// 바꿔 부른다. 공급자의 모델을 전부 써 봤는데도 그대로면 다른 공급자로 넘긴다
    /// (<c>LLMService.IsFailoverWorthy</c>) — 그 자리에서는 「여기서 더 해 볼 것이
    /// 없다」가 접속 실패와 똑같이 참이다.
    /// </para>
    /// </remarks>
    public bool IsTransientOverload { get; }

    /// <summary>실패한 요청이 쓰던 모델. 어느 모델을 쉬게 할지 정하는 데 쓴다.</summary>
    public string? Model { get; }
}
