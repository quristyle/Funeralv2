using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using JSini.Shared.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AIAgentServer.Services;

/// <summary>
/// LLM 장비까지 실제로 연결되는지 점검한다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 필요한가.</b> 이 서비스는 스스로 답을 만들지 않고 LLM 장비에 물어본다.
/// 그래서 프로세스가 멀쩡해도 <b>LLM 이 꺼져 있으면 아무 일도 못 한다.</b>
/// 예전 <c>/health</c> 는 "이 프로세스가 응답한다" 만 알려 주어,
/// 상태 화면이 LLM 이 죽은 상태에서도 초록으로 보였다.
/// </para>
///
/// <para>
/// <b>어디까지 확인하나 (두 단계).</b>
/// </para>
/// <list type="number">
///   <item><b>접속</b> — LLM 주소에 HTTP 로 닿는가. 장비 꺼짐·네트워크 단절을 잡는다.</item>
///   <item>
///     <b>모델 목록</b> — <c>GET /v1/models</c> 로 <b>설정한 모델이 올라와 있는가.</b>
///     이것이 특히 중요하다. 장비는 켜져 있는데 그 모델이 로딩돼 있지 않으면
///     지금까지는 <b>실제 대화 요청에서만 터지는 조용한 실패</b>였다.
///   </item>
/// </list>
///
/// <para>
/// <b>추론은 하지 않는다.</b> 실제로 한 토큰이라도 만들어 보면 가장 확실하지만
/// 점검마다 GPU 를 쓰고, 모델 로드가 걸리면 수십 초씩 늘어져 상태 화면이 답답해진다.
/// 생성까지 확인하는 것은 화면의 '정밀 확인' 버튼(<c>POST /ai/health/deep</c>)이 맡는다.
/// </para>
///
/// <para>
/// <b>결과를 캐시한다.</b> 상태 화면은 주기적으로 폴링하고, 게이트웨이도 같은 <c>/health</c> 를
/// 찌른다. 캐시가 없으면 LLM 장비가 우리 점검 요청만으로 두들겨 맞는다.
/// </para>
///
/// <para>
/// <b>Degraded 로 보고한다.</b> Unhealthy(503)로 올리면 로드밸런서가 이 서비스를 내려
/// 화면에서 아예 사라진다. 서비스는 살아 있고 딸린 것이 죽은 상태이므로,
/// 200 을 유지하면서 본문에 이유를 담는 편이 맞다.
/// </para>
/// </remarks>
public class LlmHealthCheck : IHealthCheck
{
    /// <summary>점검 이름. 화면이 이 이름으로 줄을 만든다.</summary>
    public const string Name = "llm";

    /// <summary>
    /// 같은 결과를 이 시간 동안 재사용한다.
    /// 상태 화면 폴링 주기(수십 초)보다 짧게 두어 '너무 오래된 값' 이 되지 않게 한다.
    /// </summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 점검 한 번에 허용하는 시간.
    /// LLM 장비가 꺼져 있으면 연결이 타임아웃까지 매달리므로 짧게 잡는다 —
    /// 재어 보니 기본값으로는 6초 넘게 걸려 상태 화면이 그만큼 멈춰 있었다.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static HealthCheckResult? _cached;
    private static DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiProviderRegistry _registry;
    private readonly ILogger<LlmHealthCheck> _logger;

    public LlmHealthCheck(
        IHttpClientFactory httpClientFactory,
        AiProviderRegistry registry,
        ILogger<LlmHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_cached is { } cached && DateTimeOffset.UtcNow - _cachedAt < CacheFor)
        {
            return cached;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            // 기다리는 동안 다른 요청이 이미 채워 두었을 수 있다.
            if (_cached is { } fresh && DateTimeOffset.UtcNow - _cachedAt < CacheFor)
            {
                return fresh;
            }

            var result = await ProbeAsync(cancellationToken);
            _cached = result;
            _cachedAt = DateTimeOffset.UtcNow;
            return result;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<HealthCheckResult> ProbeAsync(CancellationToken cancellationToken)
    {
        // [기본 공급자만 본다]
        //
        // 공급자는 여러 개고(로컬 LLM · Groq), **고르는 것은 사용자다.** 헬스체크는
        // 서버 전체의 상태를 말하는 자리이므로 특정 사용자의 선택을 따라갈 수 없다.
        // 그래서 설정의 기본 공급자를 대표로 본다.
        //
        // 다른 공급자가 살아 있는지는 상태 화면의 '정밀 확인'(`POST /ai/health/deep?provider=`)
        // 으로 하나씩 확인한다. 여기서 전부 찔러 보게 하면 점검 한 번에 여러 곳이
        // 붙어 느려지고, Groq 는 그 점검만으로도 무료 한도를 깎는다.
        var provider = _registry.Resolve(null);
        var apiBase = provider.ApiBase;
        var model = provider.Model;

        // 설정이 비어 있으면 '연결 실패' 와 뜻이 다르다. 고칠 곳이 장비가 아니라 설정이다.
        if (string.IsNullOrWhiteSpace(apiBase))
        {
            return HealthCheckResult.Unhealthy(
                $"기본 AI 공급자({provider.Key})의 주소가 설정되지 않았습니다. "
                + "appsettings.Local.json 의 AI:Providers 를 확인하세요.");
        }

        var modelsUrl = BuildModelsUrl(apiBase);
        var data = new Dictionary<string, object>
        {
            // 화면이 "어느 장비인지" 를 함께 보여 준다. **키는 절대 넣지 않는다** — 이 경로는 익명이다.
            ["provider"] = provider.Key,
            ["providerName"] = provider.DisplayName,
            ["endpoint"] = SafeEndpoint(apiBase),
            ["model"] = string.IsNullOrWhiteSpace(model) ? "(설정 없음)" : model,
            // 고를 수 있는 공급자들. 상태 화면이 "다른 것으로 바꿔 보라" 고 안내할 수 있다.
            ["availableProviders"] = _registry.All
                .Select(p => $"{p.Key}{(p.IsConfigured ? "" : "(미설정)")}")
                .ToArray(),
        };

        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);

            // 로컬 Ollama 는 키가 필요 없지만, 키를 요구하는 게이트웨이 뒤에 둔 경우와
            // Groq 처럼 키가 필수인 공급자를 위해 붙인다.
            // 자리표시자가 그대로인 경우는 `IsConfigured` 가 false 이므로 붙이지 않는다.
            if (provider.IsConfigured && !string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", provider.ApiKey);
            }

            using var response = await client.SendAsync(request, cts.Token);
            sw.Stop();
            data["latencyMs"] = (int)sw.ElapsedMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                // 닿기는 했다. 장비가 아니라 경로·권한 문제일 수 있으므로 상태 코드를 남긴다.
                data["httpStatus"] = (int)response.StatusCode;
                return HealthCheckResult.Degraded(
                    $"LLM 장비에 닿았지만 모델 목록을 받지 못했습니다 (HTTP {(int)response.StatusCode}).",
                    data: data);
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            var models = ParseModelIds(body);
            data["modelCount"] = models.Count;

            // 모델 목록을 못 읽어도 '연결은 된다' 는 사실은 확인됐다.
            // 목록 형식이 서버마다 다를 수 있어 여기서 단정하지 않는다.
            if (models.Count == 0)
            {
                return HealthCheckResult.Healthy(
                    $"{provider.DisplayName} 에 연결됩니다. (모델 목록을 읽지 못해 모델 확인은 건너뛰었습니다)",
                    data: data);
            }

            if (!string.IsNullOrWhiteSpace(model) && !models.Contains(model))
            {
                // 장비는 살아 있는데 쓰려는 모델이 없다 — 원인이 완전히 다르므로 문구를 구분한다.
                data["availableModels"] = models.Take(10).ToArray();
                return HealthCheckResult.Degraded(
                    $"{provider.DisplayName} 은 응답하지만 설정한 모델 '{model}' 이 목록에 없습니다.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"{provider.DisplayName} 에 연결됩니다. 모델 '{model}' 사용 가능.",
                data: data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            data["latencyMs"] = (int)sw.ElapsedMilliseconds;
            _logger.LogWarning("LLM 점검 시간 초과: {Endpoint}", modelsUrl);
            return HealthCheckResult.Degraded(
                $"{provider.DisplayName} 이 {Timeout.TotalSeconds:0}초 안에 응답하지 않습니다. 꺼져 있을 수 있습니다.",
                data: data);
        }
        catch (Exception ex)
        {
            sw.Stop();
            data["latencyMs"] = (int)sw.ElapsedMilliseconds;
            _logger.LogWarning(ex, "LLM 점검 실패: {Endpoint}", modelsUrl);
            return HealthCheckResult.Degraded(
                $"{provider.DisplayName} 에 연결할 수 없습니다. ({ex.GetBaseException().Message})",
                data: data);
        }
    }

    /// <summary>
    /// 대화 요청 주소에서 모델 목록 주소를 만든다.
    /// </summary>
    /// <remarks>
    /// 설정값은 <c>http://host:11434/v1/chat/completions</c> 처럼 <b>경로까지 들어 있다.</b>
    /// 그래서 그대로 쓰면 모델 목록을 못 받는다. <c>/v1</c> 까지 남기고 <c>/models</c> 를 붙인다.
    /// 형식이 다르면 마지막 두 칸을 떼는 대신 원본 호스트에 <c>/v1/models</c> 를 붙인다.
    /// </remarks>
    internal static string BuildModelsUrl(string apiBase)
    {
        if (!Uri.TryCreate(apiBase, UriKind.Absolute, out var uri))
        {
            return apiBase.TrimEnd('/') + "/models";
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        var v1 = path.LastIndexOf("/v1", StringComparison.OrdinalIgnoreCase);

        var basePath = v1 >= 0 ? path[..(v1 + 3)] : "/v1";
        return new UriBuilder(uri) { Path = basePath + "/models", Query = string.Empty }.Uri.ToString();
    }

    /// <summary>주소에서 자격증명이 섞여 있으면 지운다. 화면에 그대로 나가는 값이다.</summary>
    private static string SafeEndpoint(string apiBase)
    {
        return Uri.TryCreate(apiBase, UriKind.Absolute, out var uri)
            ? $"{uri.Scheme}://{uri.Host}:{uri.Port}"
            : apiBase;
    }

    /// <summary>
    /// 모델 목록 응답에서 모델 이름을 뽑는다.
    /// </summary>
    /// <remarks>
    /// OpenAI 규격은 <c>{ "data": [ { "id": "..." } ] }</c> 다.
    /// Ollama 고유 규격(<c>{ "models": [ { "name": "..." } ] }</c>)도 함께 받아 준다 —
    /// 같은 장비가 두 경로를 다 열어 두는 경우가 있다.
    /// </remarks>
    internal static HashSet<string> ParseModelIds(string body)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(body)) return ids;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataArray)
                && dataArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id) && id.GetString() is { } s) ids.Add(s);
                }
            }

            if (root.TryGetProperty("models", out var modelArray)
                && modelArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in modelArray.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var n) && n.GetString() is { } s) ids.Add(s);
                    if (item.TryGetProperty("model", out var m) && m.GetString() is { } s2) ids.Add(s2);
                }
            }
        }
        catch (JsonException)
        {
            // 형식을 모르면 모델 확인만 건너뛴다. 연결 자체는 이미 확인됐다.
        }

        return ids;
    }

    /// <summary>
    /// 캐시를 버린다. '정밀 확인' 을 누른 뒤 화면이 바로 새 값을 보게 한다.
    /// </summary>
    public static void InvalidateCache()
    {
        _cached = null;
        _cachedAt = DateTimeOffset.MinValue;
    }
}
