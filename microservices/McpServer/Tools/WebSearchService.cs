using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace McpServer.Tools;

public sealed record SearchResult(string Title, string Url, string Snippet);

/// <summary>
/// 검색 뒷단 점검 결과. <c>Reason</c> 은 성할 때 <c>null</c> 이다.
/// </summary>
public sealed record SearchProbe(bool Ok, string Provider, string Endpoint, string? Reason);

/// <summary>웹 검색. 뒷단을 갈아 끼울 수 있다. 기본은 DuckDuckGo 의 HTML 화면을 읽는 것이라 화면 구조가 바뀌면 깨진다 — 검색이 업무에 중요해지면 Brave·Google API 나 SearXNG 로 옮긴다.
/// Search:Provider 설정 한 줄로 갈린다(duckduckgo · searxng · brave).</summary>
public sealed class WebSearchService
{
    private const string UserAgent =
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<WebSearchService> _logger;

    public WebSearchService(HttpClient http, IConfiguration config, ILogger<WebSearchService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>뒷단이 지금 쓸 수 있는지 두드려 본다(/health 가 쓴다). 프로세스가 살아 있어도 나가는 길이 막히면 도구는 못 쓴다. Brave 는 요청 수가 곧 요금이라 두드리지 않고 열쇠만 본다.</summary>
    public async Task<SearchProbe> ProbeAsync(CancellationToken ct)
    {
        var provider = (_config["Search:Provider"] ?? "duckduckgo").ToLowerInvariant();

        switch (provider)
        {
            case "brave":
                var key = _config["Search:BraveApiKey"];
                return string.IsNullOrWhiteSpace(key)
                    ? new SearchProbe(false, provider, "api.search.brave.com",
                        "Search:BraveApiKey 가 없습니다.")
                    : new SearchProbe(true, provider, "api.search.brave.com",
                        "열쇠가 있습니다(요금 때문에 실제로 묻지는 않습니다).");

            case "searxng":
                var baseUrl = (_config["Search:SearxngUrl"] ?? "http://127.0.0.1:8888").TrimEnd('/');
                using (var res = await _http.GetAsync(baseUrl, ct))
                {
                    return res.IsSuccessStatusCode
                        ? new SearchProbe(true, provider, baseUrl, null)
                        : new SearchProbe(false, provider, baseUrl,
                            $"응답이 {(int)res.StatusCode} 입니다.");
                }

            default:
                // 화면을 긁는 방식이라 200 만으로는 모자란다 — 구조가 바뀌면 200 을 받고도 결과가 0 건이라 줄 무늬가 잡히는지까지 본다.
                const string ddg = "https://html.duckduckgo.com/html/";
                using (var res = await _http.GetAsync($"{ddg}?q=ping", ct))
                {
                    if (!res.IsSuccessStatusCode)
                    {
                        return new SearchProbe(false, provider, ddg,
                            $"응답이 {(int)res.StatusCode} 입니다.");
                    }

                    var body = await res.Content.ReadAsStringAsync(ct);
                    return DdgRow.IsMatch(body)
                        ? new SearchProbe(true, provider, ddg, null)
                        : new SearchProbe(false, provider, ddg,
                            "응답은 왔지만 결과를 뽑지 못했습니다. 화면 구조가 바뀌었을 수 있습니다.");
                }
        }
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int count, CancellationToken ct)
        => (_config["Search:Provider"] ?? "duckduckgo").ToLowerInvariant() switch
        {
            "searxng" => SearxngAsync(query, count, ct),
            "brave" => BraveAsync(query, count, ct),
            _ => DuckDuckGoAsync(query, count, ct),
        };

    // ── DuckDuckGo (열쇠 없음) ──────────────────────────────────

    private static readonly Regex DdgRow = new(
        """<a[^>]*class="result__a"[^>]*href="(?<url>[^"]+)"[^>]*>(?<title>.*?)</a>""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex DdgSnippet = new(
        """<a[^>]*class="result__snippet"[^>]*>(?<s>.*?)</a>""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private async Task<IReadOnlyList<SearchResult>> DuckDuckGoAsync(
        string query, int count, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("q", query)]);
        using var res = await _http.PostAsync("https://html.duckduckgo.com/html/", content, ct);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadAsStringAsync(ct);

        var titles = DdgRow.Matches(body);
        var snippets = DdgSnippet.Matches(body);

        var outp = new List<SearchResult>();
        for (var i = 0; i < titles.Count && outp.Count < count; i++)
        {
            var url = Unwrap(HttpUtility.HtmlDecode(titles[i].Groups["url"].Value));
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

            outp.Add(new SearchResult(
                Strip(titles[i].Groups["title"].Value),
                url,
                i < snippets.Count ? Strip(snippets[i].Groups["s"].Value) : string.Empty));
        }

        if (outp.Count == 0)
        {
            _logger.LogWarning("DuckDuckGo 에서 결과를 뽑지 못했습니다. 화면 구조가 바뀌었을 수 있습니다.");
        }

        return outp;
    }

    /// <summary>DuckDuckGo 는 주소를 //duckduckgo.com/l/?uddg=... 로 한 겹 싸서 준다. 그대로 두면 모델이 그 추적 주소를 출처로 적으므로 풀어서 쓴다.</summary>
    private static string Unwrap(string href)
    {
        var m = Regex.Match(href, @"[?&]uddg=([^&]+)");
        if (m.Success) return HttpUtility.UrlDecode(m.Groups[1].Value);
        return href.StartsWith("//", StringComparison.Ordinal) ? "https:" + href : href;
    }

    // ── SearXNG (스스로 세운 메타검색) ──────────────────────────

    private async Task<IReadOnlyList<SearchResult>> SearxngAsync(
        string query, int count, CancellationToken ct)
    {
        var baseUrl = (_config["Search:SearxngUrl"] ?? "http://127.0.0.1:8888").TrimEnd('/');
        var url = $"{baseUrl}/search?q={Uri.EscapeDataString(query)}&format=json";

        using var res = await _http.GetAsync(url, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var outp = new List<SearchResult>();
        foreach (var r in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            if (outp.Count >= count) break;
            outp.Add(new SearchResult(
                r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                r.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                r.TryGetProperty("content", out var c) ? c.GetString() ?? "" : ""));
        }

        return outp;
    }

    // ── Brave (열쇠 필요) ───────────────────────────────────────

    private async Task<IReadOnlyList<SearchResult>> BraveAsync(
        string query, int count, CancellationToken ct)
    {
        var key = _config["Search:BraveApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Search:BraveApiKey 가 없습니다.");
        }

        var url = $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}&count={count}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Subscription-Token", key);
        req.Headers.Add("Accept", "application/json");

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var outp = new List<SearchResult>();
        if (doc.RootElement.TryGetProperty("web", out var web)
            && web.TryGetProperty("results", out var arr))
        {
            foreach (var r in arr.EnumerateArray())
            {
                if (outp.Count >= count) break;
                outp.Add(new SearchResult(
                    r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    r.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                    r.TryGetProperty("description", out var d) ? d.GetString() ?? "" : ""));
            }
        }

        return outp;
    }

    private static string Strip(string html)
        => WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", string.Empty)).Trim();
}
