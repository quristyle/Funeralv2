using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace McpServer.Tools;

public sealed record FetchedPage(string Url, string Title, string Text, bool Truncated);

/// <summary>주소 하나를 열어 사람이 읽는 본문만 뽑는다. HTML 을 그대로 주면 메뉴·광고·스크립트가 본문의 몇 배라 문맥을 먹으므로 script·style·nav 를 걷는다.
/// 모델이 고른 주소를 그대로 열므로 내부망·장비 자신은 <see cref="OutboundGuard"/> 가 막는다 — 이 클라이언트는 그 처리기로만 만든다(Program.cs).</summary>
public sealed class WebFetchService
{
    private static readonly string[] Drop =
        ["script", "style", "nav", "header", "footer", "aside", "noscript", "iframe", "svg", "form"];

    private const int MaxRedirects = 5;

    private readonly HttpClient _http;

    public WebFetchService(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36");
    }

    public async Task<FetchedPage> FetchAsync(string url, int maxChars, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var target))
        {
            return new FetchedPage(url, string.Empty, $"(http/https 주소가 아닙니다: {url})", false);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        // 리다이렉트를 손으로 따라가며 매 홉 주소를 다시 거른다. IP 는 접속마다 OutboundGuard 가 본다.
        HttpResponseMessage? res = null;
        try
        {
            for (var hop = 0; ; hop++)
            {
                if (OutboundGuard.RejectUrl(target) is { } why)
                {
                    return new FetchedPage(url, string.Empty, $"({why})", false);
                }

                try
                {
                    res = await _http.GetAsync(target, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                }
                catch (HttpRequestException ex)
                {
                    return new FetchedPage(url, string.Empty, $"(이 쪽을 열지 못했습니다: {(ex.InnerException as HttpRequestException ?? ex).Message})", false);
                }

                var code = (int)res.StatusCode;
                if (code is < 300 or > 399 || res.Headers.Location is null) break;

                if (hop >= MaxRedirects)
                {
                    return new FetchedPage(url, string.Empty, "(리다이렉트가 너무 많습니다)", false);
                }

                var loc = res.Headers.Location;
                target = loc.IsAbsoluteUri ? loc : new Uri(target, loc);
                res.Dispose();
                res = null;
            }

            return await ReadAsync(url, res!, maxChars, cts.Token);
        }
        finally
        {
            res?.Dispose();
        }
    }

    private static async Task<FetchedPage> ReadAsync(string url, HttpResponseMessage res, int maxChars, CancellationToken ct)
    {
        // 던지지 않는다 — 404·403 은 흔하고, 예외로 끊으면 대화가 통째로 죽는다. 사실을 글로 돌려주면 모델이 다른 주소를 열거나 못 읽었다고 답한다.
        if (!res.IsSuccessStatusCode)
        {
            return new FetchedPage(
                url, string.Empty,
                $"(이 쪽을 열지 못했습니다: HTTP {(int)res.StatusCode} {res.ReasonPhrase})", false);
        }

        var type = res.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!type.Contains("html", StringComparison.OrdinalIgnoreCase)
            && !type.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            return new FetchedPage(url, string.Empty, $"(글자가 아닌 내용입니다: {type})", false);
        }

        var html = await res.Content.ReadAsStringAsync(ct);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var tag in Drop)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes is null) continue;
            foreach (var n in nodes.ToList()) n.Remove();
        }

        var title = WebUtility.HtmlDecode(
            doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? string.Empty);

        var body = doc.DocumentNode.SelectSingleNode("//article")
                   ?? doc.DocumentNode.SelectSingleNode("//main")
                   ?? doc.DocumentNode.SelectSingleNode("//body")
                   ?? doc.DocumentNode;

        var text = WebUtility.HtmlDecode(body.InnerText ?? string.Empty);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n\s*\n\s*\n+", "\n\n").Trim();

        var cut = text.Length > maxChars;
        if (cut) text = text[..maxChars];

        return new FetchedPage(url, title, text, cut);
    }
}
