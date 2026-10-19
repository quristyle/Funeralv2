using System.Text.Json;

namespace AIAgentServer.Services;

/// <summary>
/// <b>과금되는 모델을 부르지 않도록 막는다.</b> OpenRouter 처럼 무료·유료 모델이
/// 같은 주소에 섞여 있는 공급자에 쓴다.
/// Groq 무료 플랜은 결제 수단이 없으면 애초에 청구될 곳이 없다.
/// </summary>
public sealed class FreeModelGuard
{
    /// <summary>무료 모델 이름의 약속된 접미사.</summary>
    public const string FreeSuffix = ":free";

    /// <summary>카탈로그를 이 시간 동안 재사용한다. 목록이 자주 바뀌지는 않는다.</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static HashSet<string>? _cached;
    private static DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FreeModelGuard> _logger;

    public FreeModelGuard(IHttpClientFactory httpClientFactory, ILogger<FreeModelGuard> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>이 모델을 무료로 확인했는지.</summary>
    /// <param name="catalogUrl">모델 목록 주소. 공급자 설정에서 온다.</param>
    public async Task<FreeModelVerdict> VerifyAsync(
        string? model, string catalogUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return FreeModelVerdict.Reject("모델 이름이 비어 있습니다.");
        }

        // ── 1단계: 접미사 (돈 들지 않는 검사) ──────────────────
        //
        // 이것을 먼저 보는 이유는 카탈로그를 받지 못하는 상황에서도 최소한의
        // 방어가 남아야 하기 때문이다.
        if (!model.EndsWith(FreeSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return FreeModelVerdict.Reject(
                $"'{model}' 은 무료 모델이 아닙니다. 이름이 '{FreeSuffix}' 로 끝나는 모델만 사용합니다.");
        }

        // ── 2단계: 카탈로그의 실제 가격 ────────────────────────
        var freeModels = await GetFreeModelsAsync(catalogUrl, cancellationToken);

        if (freeModels is null)
        {
            // 카탈로그를 못 받았다. 접미사 검사는 통과했으므로 **통과시킨다.**
            //
            // fail closed 를 여기까지 밀면, 카탈로그 주소가 잠깐 안 될 때 AI 기능이
            // 통째로 멈춘다. 접미사는 공급자가 문서로 약속한 규칙이고, 요청 본문의
            // `max_price = 0` 이 마지막 방어선으로 남아 있다.
            _logger.LogWarning(
                "무료 모델 목록을 받지 못해 이름 규칙만으로 판단합니다: {Model}", model);

            return FreeModelVerdict.Accept(model, verifiedByCatalog: false);
        }

        if (!freeModels.Contains(model))
        {
            return FreeModelVerdict.Reject(
                $"'{model}' 이 무료 모델 목록에 없습니다. "
                + "무료 제공이 끝났거나 이름이 바뀐 모델일 수 있습니다.");
        }

        return FreeModelVerdict.Accept(model, verifiedByCatalog: true);
    }

    /// <summary>확인된 무료 모델 이름들. 화면이 목록을 보여 주는 데도 쓴다. 받지 못하면 null (빈 목록과 구분해야 한다).</summary>
    public async Task<HashSet<string>?> GetFreeModelsAsync(
        string catalogUrl, CancellationToken cancellationToken = default)
    {
        if (_cached is { } cached && DateTimeOffset.UtcNow - _cachedAt < CacheFor)
        {
            return cached;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is { } fresh && DateTimeOffset.UtcNow - _cachedAt < CacheFor)
            {
                return fresh;
            }

            var fetched = await FetchAsync(catalogUrl, cancellationToken);
            if (fetched is null) return null;

            _cached = fetched;
            _cachedAt = DateTimeOffset.UtcNow;
            return fetched;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<HashSet<string>?> FetchAsync(
        string catalogUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(FetchTimeout);

            var client = _httpClientFactory.CreateClient();

            // 목록 조회는 **인증이 필요 없다.** 키를 붙이지 않는다 —
            // 붙일 이유가 없는 곳에 키를 흘리지 않는 편이 낫다.
            var body = await client.GetStringAsync(catalogUrl, cts.Token);
            var models = ParseFreeModels(body);

            _logger.LogInformation(
                "무료 모델 목록을 갱신했습니다. {Count}개 (출처: {Url})", models.Count, catalogUrl);

            return models;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "무료 모델 목록을 받지 못했습니다: {Url}", catalogUrl);
            return null;
        }
    }

    /// <summary>카탈로그에서 <b>접미사와 가격 둘 다</b> 무료인 모델만 골라낸다.</summary>
    internal static HashSet<string> ParseFreeModels(string body)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(body)) return result;

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var model in data.EnumerateArray())
        {
            if (!model.TryGetProperty("id", out var idElement)) continue;
            if (idElement.GetString() is not { } id) continue;

            // 접미사가 없으면 가격이 0 이어도 넣지 않는다.
            // `openrouter/free`(자동 라우터)가 여기 걸린다 — 지금 0 이라도
            // 어디로 라우팅될지 알 수 없다.
            if (!id.EndsWith(FreeSuffix, StringComparison.OrdinalIgnoreCase)) continue;

            if (!model.TryGetProperty("pricing", out var pricing)
                || pricing.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (IsZero(pricing, "prompt") && IsZero(pricing, "completion"))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>가격 항목이 0 인지. 값이 없으면 <b>0 으로 보지 않는다.</b> 문자열("0")로도 숫자(0)로도 온다.</summary>
    private static bool IsZero(JsonElement pricing, string field)
    {
        if (!pricing.TryGetProperty(field, out var value)) return false;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDouble(out var n) && n == 0,
            JsonValueKind.String => double.TryParse(value.GetString(), out var s) && s == 0,
            _ => false,
        };
    }

    /// <summary>목록 캐시를 버린다. 화면에서 새로 받아 보고 싶을 때.</summary>
    public static void InvalidateCache()
    {
        _cached = null;
        _cachedAt = DateTimeOffset.MinValue;
    }
}

/// <summary>무료 여부 판정 결과.</summary>
public sealed class FreeModelVerdict
{
    public bool IsFree { get; private init; }

    /// <summary>통과한 모델 이름.</summary>
    public string? Model { get; private init; }

    /// <summary>거절 사유. 사람에게 그대로 보여 준다.</summary>
    public string? Reason { get; private init; }

    /// <summary>
    /// 카탈로그의 실제 가격까지 확인했는지.
    /// <c>false</c> 면 이름 규칙만으로 통과한 것이다(목록을 못 받은 경우).
    /// </summary>
    public bool VerifiedByCatalog { get; private init; }

    public static FreeModelVerdict Accept(string model, bool verifiedByCatalog) =>
        new() { IsFree = true, Model = model, VerifiedByCatalog = verifiedByCatalog };

    public static FreeModelVerdict Reject(string reason) =>
        new() { IsFree = false, Reason = reason };
}
