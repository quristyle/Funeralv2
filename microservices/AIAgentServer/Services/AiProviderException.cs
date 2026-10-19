namespace AIAgentServer.Services;

/// <summary>AI 공급자 호출이 실패했을 때 던진다. 사람에게 그대로 보여 줄 문구를 담는다. 원인(장비 꺼짐·키 오류·한도)을 나눠 담는다.</summary>
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

    /// <summary>무료 한도에 걸린 것인지. 화면이 이 값으로 문구를 가른다.</summary>
    public bool IsRateLimited { get; }

    /// <summary>
    /// <b>상대에 아예 닿지 못한</b> 경우인지 (장비 꺼짐 · 접속 시간 초과 · DNS 실패).
    /// 자동 전환(결정 D-A3)의 유일한 조건이다.
    /// </summary>
    public bool IsConnectFailure { get; }

    /// <summary>한도가 <b>모델 하나가 아니라 계정 전체</b>에 걸린 것인지. 429 를 두 갈래로 나눠야 한다.</summary>
    public bool IsAccountWideLimit { get; }

    /// <summary>
    /// <b>지금 붐빌 뿐</b>인지 (상류가 5xx 로 "high demand" 를 돌려준 경우).
    /// Gemini 등은 붐빔을 429 가 아닌 503 으로 주므로 한도로 뭉개지 않는다.
    /// </summary>
    public bool IsTransientOverload { get; }

    /// <summary>실패한 요청이 쓰던 모델. 어느 모델을 쉬게 할지 정하는 데 쓴다.</summary>
    public string? Model { get; }
}
