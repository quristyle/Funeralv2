using System.Collections.Concurrent;

namespace AIAgentServer.Services;

/// <summary>
/// 공급자별 사용량을 기록해 둔다. 상태 화면이 읽어 보여 준다.
/// </summary>
/// <remarks>
/// <para>
/// <b>물어보지 않고, 지나가는 것을 줍는다.</b> Groq 는 응답마다
/// <c>x-ratelimit-remaining-*</c> 헤더로 남은 한도를 알려 준다. 그래서 사용량을 알려고
/// 따로 호출할 필요가 없다 — <b>오히려 그 호출이 한도를 깎는다.</b> 실제 요청이
/// 오갈 때 헤더를 받아 적어 두고, 화면은 그 마지막 값을 본다.
/// </para>
/// <para>
/// 그래서 <b>한 번도 부르지 않았으면 값이 없다.</b> 화면은 그때 '아직 호출 없음' 으로
/// 보여 준다. 실제보다 오래된 값일 수 있으므로 <see cref="AiUsageSnapshot.ObservedAt"/> 를
/// 함께 준다 — 언제 기준의 값인지 모르면 숫자를 믿을 수 없다.
/// </para>
/// <para>
/// 프로세스 메모리에만 둔다. 재기동하면 사라지고, 서버를 여러 대로 늘리면 대수마다
/// 따로 쌓인다. 정확한 회계가 목적이 아니라 <b>"얼마 안 남았나" 를 사람이 보는 것</b>이
/// 목적이라 이 정도로 충분하다. 정확한 잔량은 언제나 공급자가 준 마지막 헤더 값이다.
/// </para>
/// </remarks>
public static class AiUsageTracker
{
    private static readonly ConcurrentDictionary<string, AiUsageSnapshot> Snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 응답에서 읽은 한도 정보를 적어 둔다.
    /// </summary>
    /// <remarks>
    /// 헤더가 하나도 없는 공급자(로컬 LLM)도 호출 횟수는 세어 둔다 —
    /// "이 공급자를 실제로 쓰고 있는가" 가 화면에서 유용하다.
    /// </remarks>
    public static void Record(
        string providerKey,
        bool ok,
        int? latencyMs,
        string? limitRequests,
        string? remainingRequests,
        string? limitTokens,
        string? remainingTokens,
        string? resetRequests,
        string? resetTokens)
    {
        Snapshots.AddOrUpdate(
            providerKey,
            _ => new AiUsageSnapshot
            {
                CallsOk = ok ? 1 : 0,
                CallsFailed = ok ? 0 : 1,
                LastCallAt = DateTimeOffset.UtcNow,
                LastLatencyMs = latencyMs,
                LimitRequests = limitRequests,
                RemainingRequests = remainingRequests,
                LimitTokens = limitTokens,
                RemainingTokens = remainingTokens,
                ResetRequests = resetRequests,
                ResetTokens = resetTokens,
                ObservedAt = HasAnyLimit(limitRequests, remainingRequests, limitTokens, remainingTokens)
                    ? DateTimeOffset.UtcNow
                    : null,
            },
            (_, prev) => new AiUsageSnapshot
            {
                CallsOk = prev.CallsOk + (ok ? 1 : 0),
                CallsFailed = prev.CallsFailed + (ok ? 0 : 1),
                LastCallAt = DateTimeOffset.UtcNow,
                LastLatencyMs = latencyMs ?? prev.LastLatencyMs,

                // 헤더가 없는 응답(연결 실패 등)에는 **옛 값을 지우지 않는다.**
                // 마지막으로 알던 잔량이 사라지면 화면이 '모름' 으로 후퇴한다.
                LimitRequests = limitRequests ?? prev.LimitRequests,
                RemainingRequests = remainingRequests ?? prev.RemainingRequests,
                LimitTokens = limitTokens ?? prev.LimitTokens,
                RemainingTokens = remainingTokens ?? prev.RemainingTokens,
                ResetRequests = resetRequests ?? prev.ResetRequests,
                ResetTokens = resetTokens ?? prev.ResetTokens,
                ObservedAt = HasAnyLimit(limitRequests, remainingRequests, limitTokens, remainingTokens)
                    ? DateTimeOffset.UtcNow
                    : prev.ObservedAt,
            });
    }

    /// <summary>기록이 있으면 준다. 한 번도 안 불렀으면 null.</summary>
    public static AiUsageSnapshot? Get(string providerKey)
    {
        return Snapshots.TryGetValue(providerKey, out var snapshot) ? snapshot : null;
    }

    /// <summary>
    /// 자동 전환이 일어난 사실. 마지막 한 건만 들고 있는다.
    /// </summary>
    /// <remarks>
    /// <b>전환이 조용히 일어나면 안 된다.</b> 사용자는 다른 모델의 답을 받았고,
    /// 관리자는 로컬 장비가 꺼져 있다는 사실을 알아야 한다. 로그에도 남기지만
    /// 상태 화면에서 바로 보이는 편이 훨씬 빠르다.
    /// </remarks>
    public static AiFailoverRecord? LastFailover { get; private set; }

    /// <summary>
    /// 고른 모델이 무료가 아니어서 기본 모델로 바꿔 부른 사실. 마지막 한 건.
    /// </summary>
    /// <remarks>
    /// OpenRouter 는 무료 목록을 수시로 바꾼다. 어제 무료였던 모델이 오늘 사라지면
    /// 사용자 설정은 그대로인데 실제로는 다른 모델이 답한다 — <b>그 사실이 보여야</b>
    /// 관리자가 환경설정의 모델 목록을 손볼 수 있다.
    /// </remarks>
    public static AiModelSubstitution? LastModelSubstitution { get; private set; }

    public static void RecordModelSubstitution(
        string providerKey, string from, string to, string reason)
    {
        LastModelSubstitution = new AiModelSubstitution
        {
            ProviderKey = providerKey,
            From = from,
            To = to,
            Reason = reason,
            At = DateTimeOffset.UtcNow,
            Count = (LastModelSubstitution?.Count ?? 0) + 1,
        };
    }

    /// <summary>
    /// 모델이 <b>한도에 걸려</b> 다른 무료 모델로 바꿔 부른 사실. 마지막 한 건.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="LastModelSubstitution"/> 과 <b>따로 둔다.</b> 원인이 다르고 사람이 할 일이
    /// 다르다 — 저쪽은 "고른 모델이 무료가 아니게 됐다"(설정 목록을 손볼 때),
    /// 이쪽은 "그 모델이 지금 붐빈다"(시간이 지나면 풀린다). 한 칸에 뭉개면
    /// 목록을 고쳐야 하는지 그냥 기다리면 되는지 알 수 없다.
    /// </para>
    /// <para>
    /// 이것이 <b>자주</b> 뜨면 환경설정 기본 모델을 실제로 잘 답하는 것으로 바꿀 때다.
    /// </para>
    /// </remarks>
    public static AiModelRotation? LastModelRotation { get; private set; }

    public static void RecordModelRotation(
        string providerKey, string from, string to, string reason)
    {
        LastModelRotation = new AiModelRotation
        {
            ProviderKey = providerKey,
            From = from,
            To = to,
            Reason = reason,
            At = DateTimeOffset.UtcNow,
            Count = (LastModelRotation?.Count ?? 0) + 1,
        };
    }

    public static void RecordFailover(string from, string to)
    {
        LastFailover = new AiFailoverRecord
        {
            From = from,
            To = to,
            At = DateTimeOffset.UtcNow,
            Count = (LastFailover?.Count ?? 0) + 1,
        };
    }

    private static bool HasAnyLimit(params string?[] values)
    {
        return values.Any(v => !string.IsNullOrWhiteSpace(v));
    }
}

/// <summary>
/// 모델 바꿔치기 한 건. <b>과금되는 모델을 부르지 않으려고</b> 기본 모델로 돌린 경우다.
/// </summary>
public sealed class AiModelSubstitution
{
    public string ProviderKey { get; init; } = string.Empty;

    /// <summary>사용자가 고른, 쓸 수 없던 모델.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>대신 쓴 모델.</summary>
    public string To { get; init; } = string.Empty;

    /// <summary>왜 못 썼는지. 무료 목록에서 사라졌거나 이름 규칙에 맞지 않는다.</summary>
    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset At { get; init; }

    public int Count { get; init; }
}

/// <summary>
/// 모델 바꿔치기 한 건. <b>한도(429)에 걸려</b> 다른 무료 모델로 넘긴 경우다.
/// </summary>
public sealed class AiModelRotation
{
    public string ProviderKey { get; init; } = string.Empty;

    /// <summary>한도에 걸린 모델. 사용자가 고른 것일 수 있다.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>대신 부른 모델. <b>이것도 무료 확인을 통과한 것이다.</b></summary>
    public string To { get; init; } = string.Empty;

    /// <summary>공급자가 준 한도 안내 문구.</summary>
    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset At { get; init; }

    /// <summary>기동 이후 바꿔치기 횟수. 잦으면 기본 모델을 바꿀 때다.</summary>
    public int Count { get; init; }
}

/// <summary>자동 전환 한 건 (결정 D-A3).</summary>
public sealed class AiFailoverRecord
{
    /// <summary>접속에 실패한 공급자.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>대신 답한 공급자.</summary>
    public string To { get; init; } = string.Empty;

    public DateTimeOffset At { get; init; }

    /// <summary>기동 이후 전환 횟수. 잦으면 장비를 손봐야 한다는 신호다.</summary>
    public int Count { get; init; }
}

/// <summary>공급자 한 곳의 사용량. 전부 마지막으로 관측한 값이다.</summary>
public sealed class AiUsageSnapshot
{
    /// <summary>성공한 호출 수(프로세스 기동 이후).</summary>
    public int CallsOk { get; init; }

    /// <summary>실패한 호출 수(프로세스 기동 이후).</summary>
    public int CallsFailed { get; init; }

    /// <summary>마지막으로 부른 시각.</summary>
    public DateTimeOffset? LastCallAt { get; init; }

    /// <summary>마지막 호출에 걸린 시간.</summary>
    public int? LastLatencyMs { get; init; }

    /// <summary>하루 요청 상한 (<c>x-ratelimit-limit-requests</c>).</summary>
    public string? LimitRequests { get; init; }

    /// <summary>남은 요청 수 (<c>x-ratelimit-remaining-requests</c>).</summary>
    public string? RemainingRequests { get; init; }

    /// <summary>분당 토큰 상한 (<c>x-ratelimit-limit-tokens</c>).</summary>
    public string? LimitTokens { get; init; }

    /// <summary>남은 토큰 (<c>x-ratelimit-remaining-tokens</c>).</summary>
    public string? RemainingTokens { get; init; }

    /// <summary>요청 한도가 초기화되기까지 (예: <c>12m57.599s</c>).</summary>
    public string? ResetRequests { get; init; }

    /// <summary>토큰 한도가 초기화되기까지 (예: <c>30.225s</c>).</summary>
    public string? ResetTokens { get; init; }

    /// <summary>
    /// 위 한도 값들을 관측한 시각. 한도 헤더를 준 적이 없으면 null.
    /// </summary>
    /// <remarks>
    /// 화면이 "몇 분 전 기준" 을 함께 보여 준다. 언제 기준의 숫자인지 모르면
    /// 남은 한도를 믿고 판단할 수 없다.
    /// </remarks>
    public DateTimeOffset? ObservedAt { get; init; }
}
