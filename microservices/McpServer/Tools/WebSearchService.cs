using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace McpServer.Tools;

public sealed record SearchResult(string Title, string Url, string Snippet);

/// <summary>
/// 웹 검색. <b>뒷단을 갈아 끼울 수 있게</b> 만들었다.
/// </summary>
/// <remarks>
/// <para>
/// 기본은 DuckDuckGo 의 HTML 화면을 읽는 것이다. <b>열쇠가 필요 없어 바로 쓸 수
/// 있지만, 화면 구조가 바뀌면 깨진다.</b> 검색이 업무에 중요해지면 제대로 된
/// API(Brave·Google) 로 옮기거나 SearXNG 를 세우는 편이 맞다 — 그래서 설정
/// 한 줄로 갈리게 해 두었다.
/// </para>
/// <para>
/// <c>Search:Provider</c> 에 <c>duckduckgo</c>(기본) · <c>searxng</c> · <c>brave</c> 를 적는다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// DuckDuckGo 는 주소를 <c>//duckduckgo.com/l/?uddg=...</c> 로 한 겹 싸서 준다.
    /// 그대로 두면 모델이 출처로 그 추적 주소를 적는다.
    /// </summary>
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
