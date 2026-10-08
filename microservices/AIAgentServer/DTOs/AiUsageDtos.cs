namespace AIAgentServer.DTOs;

/// <summary>
/// 한 사람의 기간 사용량. 포털관리 「AI 사용량」 화면의 한 줄이다.
/// </summary>
public class AiUsageByUserDto
{
    /// <summary>
    /// 로그인 아이디. <b><c>null</c> 은 사람 없이 난 호출</b>이다 —
    /// 상태 화면의 정밀 확인처럼 게이트웨이를 거치지 않은 것들이다.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 이름. <b>못 찾을 수 있다</b> — 계정을 지워도 그 사람이 쓴 줄은 남는다.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>호출 건수. 실패도 센다.</summary>
    public int Calls { get; set; }

    /// <summary>
    /// 그중 실패한 건수. <b>0 이 아니면 눈에 띄어야 한다</b> —
    /// 「많이 쓰는데 자꾸 실패한다」가 한도·장비 문제의 첫 신호다.
    /// </summary>
    public int FailedCalls { get; set; }

    /// <summary>보낸 토큰. 대화가 길어질수록 커진다(문맥을 다시 올린다).</summary>
    public long PromptTokens { get; set; }

    /// <summary>답한 토큰.</summary>
    public long CompletionTokens { get; set; }

    /// <summary>둘의 합. <b>공급자가 알려 준 줄만</b> 더한 값이다.</summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// 토큰 수를 모르는 호출의 건수. <b>0 이 아니면 위 합계가 실제보다 작다.</b>
    /// </summary>
    /// <remarks>
    /// 이 칸이 없으면 화면이 「적게 썼다」와 「덜 세었다」를 구분하지 못한다.
    /// 토큰을 안 알려 주는 공급자가 있고, 스트림이 중간에 끊겨도 못 받는다.
    /// </remarks>
    public int UnknownTokenCalls { get; set; }

    /// <summary>한 번에 걸린 평균 시간.</summary>
    public int? AvgLatencyMs { get; set; }

    /// <summary>마지막으로 쓴 시각. <b>UTC 다.</b></summary>
    public DateTime LastCallAt { get; set; }
}

/// <summary>
/// 호출 한 건. 집계가 답하지 못하는 <b>「왜 많은가」</b>를 보는 자리다.
/// </summary>
public class AiUsageCallDto
{
    /// <summary>언제. <b>UTC 다</b> — 한국 시각은 화면이 만든다.</summary>
    public DateTime OccurredAt { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    /// <summary><c>chat-stream</c>(AI쳇) · <c>suggest-code</c> ….</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary><b>실제로 답한</b> 공급자. 고른 것과 다를 수 있다.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    public string? Model { get; set; }

    /// <summary><c>null</c> 은 「모른다」지 0 이 아니다.</summary>
    public int? PromptTokens { get; set; }

    /// <inheritdoc cref="PromptTokens"/>
    public int? CompletionTokens { get; set; }

    /// <inheritdoc cref="PromptTokens"/>
    public int? TotalTokens { get; set; }

    public int? LatencyMs { get; set; }

    public bool Ok { get; set; }

    /// <summary>실패한 까닭. 성공이면 비어 있다.</summary>
    public string? FailReason { get; set; }
}
