using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace McpServer.Tools;

public sealed record FetchedPage(string Url, string Title, string Text, bool Truncated);

/// <summary>
/// 주소 하나를 열어 <b>사람이 읽는 본문만</b> 뽑아 낸다.
/// </summary>
/// <remarks>
/// <para>
/// HTML 을 그대로 모델에게 주면 안 된다. 메뉴·광고·스크립트가 본문의 몇 배라
/// 문맥을 다 먹고, 모델이 머리말 글자를 본문으로 착각한다. 그래서
/// <c>script</c>·<c>style</c>·<c>nav</c> 따위를 걷어내고 글자만 남긴다.
/// </para>
/// <para>
/// <b>아무 주소나 열지 않는다.</b> 모델이 지어낸 주소나, 검색 결과에 섞인
/// 내부망 주소를 그대로 열면 바깥에서 닿지 못할 곳을 긁게 된다 —
/// http/https 가 아니거나 사설망·로컬 주소면 거절한다.
/// </para>
/// </remarks>
public sealed class WebFetchService
{
    private static readonly string[] Drop =
        ["script", "style", "nav", "header", "footer", "aside", "noscript", "iframe", "svg", "form"];

    private readonly HttpClient _http;

    public WebFetchService(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36");
    }

    public async Task<FetchedPage> FetchAsync(string url, int maxChars, CancellationToken ct)
    {
        if (Reject(url) is { } why)
        {
            return new FetchedPage(url, string.Empty, $"({why})", false);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        using var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // **던지지 않는다.** 모델이 고른 주소가 404·403 인 일은 흔하고, 그때
        // 예외로 끊으면 대화가 통째로 죽는다. 사실을 글로 돌려주면 모델이
        // 다른 주소를 열거나 "못 읽었다" 고 답한다.
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

        var html = await res.Content.ReadAsStringAsync(cts.Token);

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

    /// <summary>
    /// 열면 안 되는 주소면 까닭을 돌려준다. 괜찮으면 null.
    /// <b>바깥 주소만 연다</b> — 모델이 지어낸 내부망 주소를 그대로 긁지 않게.
    /// </summary>
    private static string? Reject(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)
            || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
        {
            return $"http/https 주소가 아닙니다: {url}";
        }

        var host = u.Host.ToLowerInvariant();
        if (host is "localhost" or "127.0.0.1" or "::1"
            || host.EndsWith(".local", StringComparison.Ordinal)
            || host.EndsWith(".internal", StringComparison.Ordinal)
            || Regex.IsMatch(host, @"^(10\.|127\.|192\.168\.|169\.254\.|172\.(1[6-9]|2\d|3[01])\.)"))
        {
            return $"내부망 주소는 열지 않습니다: {host}";
        }

        return null;
    }
}
