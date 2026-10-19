using System.Collections.Concurrent;

namespace AIAgentServer.Services;

/// <summary>
/// <b>한도에 걸린 모델을 잠시 쉬게 한다.</b> 기억하지 않으면 매 요청이 막힌 모델을 먼저 불러 429 를 받는다.
/// </summary>
public static class AiModelCooldown
{
    /// <summary><c>retry-after</c> 를 주지 않았을 때 쉬는 시간.</summary>
    private static readonly TimeSpan DefaultRest = TimeSpan.FromMinutes(1);

    /// <summary>아무리 길어도 이만큼만 쉰다.</summary>
    private static readonly TimeSpan MaxRest = TimeSpan.FromMinutes(30);

    /// <summary>공급자가 아주 짧은 값을 줘도 최소한 이만큼은 쉰다.</summary>
    private static readonly TimeSpan MinRest = TimeSpan.FromSeconds(10);

    /// <summary>키는 <c>공급자|모델</c>. 값은 쉬는 것이 끝나는 시각.</summary>
    private static readonly ConcurrentDictionary<string, RestEntry> Resting = new();

    private static string KeyOf(string providerKey, string model) =>
        $"{providerKey}|{model}";

    /// <summary>이 모델을 쉬게 한다.</summary>
    /// <param name="retryAfterSeconds">공급자가 알려 준 재시도 가능 시각까지의 초. 없으면 null.</param>
    public static void Rest(
        string providerKey, string model, int? retryAfterSeconds, string reason)
    {
        if (string.IsNullOrWhiteSpace(model)) return;

        var span = retryAfterSeconds is > 0
            ? TimeSpan.FromSeconds(retryAfterSeconds.Value)
            : DefaultRest;

        if (span > MaxRest) span = MaxRest;
        if (span < MinRest) span = MinRest;

        Resting[KeyOf(providerKey, model)] = new RestEntry
        {
            ProviderKey = providerKey,
            Model = model,
            Until = DateTimeOffset.UtcNow.Add(span),
            Reason = reason,
        };
    }

    /// <summary>지금 쉬는 중인지. 시간이 지난 항목은 여기서 정리한다.</summary>
    public static bool IsResting(string providerKey, string model)
    {
        if (string.IsNullOrWhiteSpace(model)) return false;

        var key = KeyOf(providerKey, model);
        if (!Resting.TryGetValue(key, out var entry)) return false;

        if (entry.Until > DateTimeOffset.UtcNow) return true;

        // 끝났다. 지워 둔다 — 안 지우면 목록이 계속 자란다.
        Resting.TryRemove(key, out _);
        return false;
    }

    /// <summary>지금 쉬는 모델들. 상태 화면이 보여 준다. 보여 줘야 하는 값이다.</summary>
    public static IReadOnlyList<RestEntry> Snapshot()
    {
        var now = DateTimeOffset.UtcNow;

        return Resting.Values
            .Where(e => e.Until > now)
            .OrderBy(e => e.Until)
            .ToList();
    }

    /// <summary>쉬는 것을 전부 잊는다. 진단(정밀 확인)에서 쓴다.</summary>
    public static void Clear() => Resting.Clear();

    /// <summary>쉬는 모델 한 건.</summary>
    public sealed class RestEntry
    {
        public string ProviderKey { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;

        /// <summary>이 시각이 지나면 다시 쓴다.</summary>
        public DateTimeOffset Until { get; init; }

        /// <summary>왜 쉬는지. 사람에게 그대로 보여 준다.</summary>
        public string Reason { get; init; } = string.Empty;
    }
}
