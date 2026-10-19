using System.Collections.Concurrent;

namespace AIAgentServer.Services;

/// <summary>공급자별 사용량을 기록해 둔다. 상태 화면이 읽어 보여 준다. 물어보지 않고, 지나가는 것을 줍는다.</summary>
public static class AiUsageTracker
{
    private static readonly ConcurrentDictionary<string, AiUsageSnapshot> Snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>응답에서 읽은 한도 정보를 적어 둔다. 헤더가 하나도 없는 공급자(로컬 LLM)도 호출 횟수는 세어 둔다</summary>
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

    /// <summary>자동 전환이 일어난 사실. 마지막 한 건만 들고 있는다. 전환이 조용히 일어나면 안 된다.</summary>
    public static AiFailoverRecord? LastFailover { get; private set; }

    /// <summary>고른 모델이 무료가 아니어서 기본 모델로 바꿔 부른 사실. 마지막 한 건. OpenRouter 는 무료 목록을 수시로 바꾼다.</summary>
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

    /// <summary>모델이 <b>한도에 걸려</b> 다른 무료 모델로 바꿔 부른 사실. 마지막 한 건. 과 따로 둔다.</summary>
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

/// <summary>모델 바꿔치기 한 건. <b>과금되는 모델을 부르지 않으려고</b> 기본 모델로 돌린 경우다.</summary>
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

/// <summary>모델 바꿔치기 한 건. <b>한도(429)에 걸려</b> 다른 무료 모델로 넘긴 경우다.</summary>
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

    /// <summary>위 한도 값들을 관측한 시각. 한도 헤더를 준 적이 없으면 null. 화면이 "몇 분 전 기준" 을 함께 보여 준다.</summary>
    public DateTimeOffset? ObservedAt { get; init; }
}
