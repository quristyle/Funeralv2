using System.Collections.Concurrent;

namespace AIAgentServer.Services;

/// <summary>
/// <b>한도에 걸린 모델을 잠시 쉬게 한다.</b> 다음 요청은 그 모델을 건너뛰고
/// 다른 무료 모델로 간다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 기억해야 하나.</b> 기억하지 않으면 매 요청이 <b>똑같이 막힌 모델을 먼저 부르고</b>
/// 429 를 받은 뒤에야 다음 모델로 넘어간다. 왕복 한 번이 늘어나는 것은 물론이고,
/// OpenRouter 무료는 <b>하루 요청 수</b>로도 한도가 걸리므로 그 헛호출이 남은 몫을 깎는다.
/// </para>
/// <para>
/// <b>쉬는 시간은 공급자가 정한다.</b> 429 응답의 <c>retry-after</c> 를 그대로 쓴다.
/// 없으면 <see cref="DefaultRest"/>. 값이 터무니없이 크면 <see cref="MaxRest"/> 로 자른다 —
/// 공급자가 "하루 뒤" 를 보내는 경우가 있는데, 그것을 믿고 하루 동안 빼 두면
/// 실제로는 몇 분 뒤에 풀렸는데도 좋은 모델을 계속 안 쓰게 된다.
/// </para>
/// <para>
/// <b>프로세스 메모리에만 둔다.</b> 재기동하면 잊는다. 서버를 여러 대로 늘리면
/// 각자 따로 배운다. 정확한 회계가 목적이 아니라 <b>헛호출을 줄이는 것</b>이 목적이라
/// 이 정도로 충분하다 — 잘못 기억해도 다른 무료 모델로 답이 나가고, 잊어도
/// 429 를 한 번 더 맞고 넘어갈 뿐이다.
/// </para>
/// <para>
/// <b>이것이 유일한 방어선이 되면 안 된다.</b> 쉬는 모델만 남은 경우에는
/// 그래도 부른다(<see cref="LLMService"/>) — 이 기억은 추측이고, 추측 때문에
/// "쓸 모델이 없습니다" 로 끝내는 것이 더 나쁘다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 지금 쉬는 모델들. 상태 화면이 보여 준다.
    /// </summary>
    /// <remarks>
    /// <b>보여 줘야 하는 값이다.</b> 사용자가 환경설정에서 A 를 골라 뒀는데 서버가
    /// B 로 답하고 있다면, 그 사실이 어딘가에 드러나야 한다. 안 그러면
    /// "왜 고른 것과 다르게 동작하지" 를 알아낼 방법이 없다.
    /// </remarks>
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
