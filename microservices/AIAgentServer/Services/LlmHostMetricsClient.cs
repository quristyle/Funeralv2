using System.Text.Json;
using System.Net.Http.Headers;

namespace AIAgentServer.Services;

/// <summary>
/// LLM 장비(호스트명 <c>llm</c>)의 하드웨어 상태를 읽어 온다.
/// </summary>
/// <remarks>
/// <para>
/// 장비는 운영서버와 같은 내부망에 있고 바깥에서 직접 닿지 않는다. 그래서
/// <b>주소를 따로 설정하지 않고 <c>jsini</c> 공급자의 <c>ApiBase</c> 에서 뽑는다.</b>
/// 추론을 보내는 곳과 상태를 묻는 곳이 같은 장비이므로, 둘이 어긋날 일이 없다 —
/// 개발 PC 는 <c>https://llm.jsini.co.kr</c>, 운영은 <c>http://192.168.219.227:11434</c>
/// 로 자동으로 갈린다.
/// </para>
/// <para>
/// 인증도 추론과 같은 키를 쓴다. 장비 쪽 nginx 가 <c>/metrics</c> 에도 같은
/// <c>Bearer</c> 검사를 걸어 두었다.
/// </para>
/// </remarks>
public sealed class LlmHostMetricsClient
{
    private readonly HttpClient _http;
    private readonly AiProviderRegistry _registry;
    private readonly ILogger<LlmHostMetricsClient> _logger;

    public LlmHostMetricsClient(
        HttpClient http,
        AiProviderRegistry registry,
        ILogger<LlmHostMetricsClient> logger)
    {
        _http = http;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// 상태를 읽어 온다. <b>못 읽어도 예외를 던지지 않는다</b> —
    /// 장비가 꺼져 있는 것도 화면이 보여 줘야 하는 상태다.
    /// </summary>
    public async Task<LlmHostMetricsResult> ReadAsync(CancellationToken ct = default)
    {
        var provider = _registry.Resolve("jsini");

        if (string.IsNullOrWhiteSpace(provider.ApiBase))
        {
            return LlmHostMetricsResult.Unavailable(
                "로컬 LLM 주소가 설정돼 있지 않습니다. (AI:Providers:jsini 또는 LLM:ApiBase)");
        }

        if (!Uri.TryCreate(provider.ApiBase, UriKind.Absolute, out var api))
        {
            return LlmHostMetricsResult.Unavailable($"LLM 주소를 해석할 수 없습니다: {provider.ApiBase}");
        }

        // 수집기는 nginx 의 맨 위(/metrics)에 있다. 추론 경로(/v1/...)는 떼어 낸다.
        var url = new UriBuilder(api) { Path = "/metrics", Query = string.Empty }.Uri;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var res = await _http.SendAsync(req, cts.Token);
            var body = await res.Content.ReadAsStringAsync(cts.Token);

            if (!res.IsSuccessStatusCode)
            {
                return LlmHostMetricsResult.Unavailable(
                    $"LLM 장비가 {(int)res.StatusCode} 로 답했습니다.", url.GetLeftPart(UriPartial.Authority));
            }

            using var doc = JsonDocument.Parse(body);
            return LlmHostMetricsResult.Ok(doc.RootElement.Clone(), url.GetLeftPart(UriPartial.Authority));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return LlmHostMetricsResult.Unavailable("LLM 장비가 제때 답하지 않았습니다. (15초)",
                url.GetLeftPart(UriPartial.Authority));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 장비 상태를 읽지 못했습니다. url={Url}", url);
            return LlmHostMetricsResult.Unavailable($"LLM 장비에 닿지 못했습니다: {ex.Message}",
                url.GetLeftPart(UriPartial.Authority));
        }
    }
}

/// <summary>상태 읽기 결과. 실패도 값으로 돌려준다.</summary>
public sealed record LlmHostMetricsResult
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public string? Endpoint { get; init; }
    public JsonElement? Metrics { get; init; }

    public static LlmHostMetricsResult Ok(JsonElement metrics, string endpoint) =>
        new() { Reachable = true, Metrics = metrics, Endpoint = endpoint };

    public static LlmHostMetricsResult Unavailable(string message, string? endpoint = null) =>
        new() { Reachable = false, Message = message, Endpoint = endpoint };
}
