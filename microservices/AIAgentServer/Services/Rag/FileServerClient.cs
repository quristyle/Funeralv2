using System.Net.Http.Headers;
using JSini.Shared.DTOs;

namespace AIAgentServer.Services.Rag;

/// <summary>RAG 문서의 실물을 맡아 두는 곳 — 파일 서버.</summary>
public sealed class FileServerClient
{
    /// <summary>파일 서버의 업무 구분. 저장 폴더 이름이 된다(<c>…/rag/yyyyMM/</c>).</summary>
    public const string BizType = "rag";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<FileServerClient> _logger;

    public FileServerClient(HttpClient http, IConfiguration config, ILogger<FileServerClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    private string Root
    {
        get
        {
            var v = _config["FileServer:Endpoint"];
            return (string.IsNullOrWhiteSpace(v) ? "http://localhost:5350" : v).TrimEnd('/');
        }
    }

    /// <summary>올린다. 돌려주는 것은 파일 서버가 붙인 아이디다.</summary>
    /// <param name="who">
    /// 올린 사람. <c>X-User-Id</c> 로 넘겨 파일에 주인을 남긴다 —
    /// 비우면 파일 서버가 임자 없는 파일로 적는다.
    /// </param>
    public async Task<Guid> UploadAsync(
        Stream content, string fileName, string? contentType, string? who,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var part = new StreamContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        form.Add(part, "file", fileName);
        form.Add(new StringContent(BizType), "bizType");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Root}/upload") { Content = form };
        if (!string.IsNullOrWhiteSpace(who)) req.Headers.TryAddWithoutValidation("X-User-Id", who);

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"파일 서버가 받지 않았습니다 ({(int)res.StatusCode}). {Trim(body)}");
        }

        return ReadId(body)
               ?? throw new InvalidOperationException($"파일 서버가 아이디를 주지 않았습니다. {Trim(body)}");
    }

    /// <summary>우리가 파일 서버에 대는 이름. 신원을 안 실으면 못 읽는다.</summary>
    public const string SystemActor = "system:rag";

    /// <summary>바이트를 받아 온다. 없으면 <c>null</c>.</summary>
    public async Task<Stream?> OpenAsync(
        Guid fileId, string? who = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Root}/download/id/{fileId}");
        req.Headers.TryAddWithoutValidation(
            "X-User-Id", string.IsNullOrWhiteSpace(who) ? SystemActor : who);

        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // **던지지 않는다.** 부르는 쪽은 색인을 만드는 고리라, 여기서 던지면
            // 파일 **하나**를 못 받은 것이 그 카테고리의 색인을 통째로 멎게 한다.
            // 못 받은 문서는 「실패」로 적히고 나머지는 계속 간다.
            //
            // 파일 서버가 바깥 호스트로 302 를 주는 환경에서는
            // 그 호스트가 안 풀려 색인이 첫 문서에서 끝난다.
            _logger.LogWarning(ex, "파일 서버에서 {Id} 를 받지 못했습니다.", fileId);
            return null;
        }

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "파일 서버가 {Id} 를 주지 않았습니다 ({Code}).", fileId, (int)res.StatusCode);
            res.Dispose();
            return null;
        }

        return await res.Content.ReadAsStreamAsync(ct);
    }

    /// <summary>지운다. <b>실패해도 던지지 않는다</b> — 아래 머리말 참고. 지우는 쪽은 이미 우리 표에서 줄을 뺀 뒤다.</summary>
    public async Task DeleteAsync(Guid fileId, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{Root}/{fileId}");
            req.Headers.TryAddWithoutValidation("X-User-Id", SystemActor);

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "파일 서버에서 {Id} 를 지우지 못했습니다 ({Code}).", fileId, (int)res.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "파일 서버에서 {Id} 를 지우지 못했습니다.", fileId);
        }
    }

    /// <summary>응답에서 아이디를 꺼낸다. 봉투 모양을 하나로 못 박지 않는다.</summary>
    internal static Guid? ReadId(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        return Find(doc.RootElement);

        static Guid? Find(System.Text.Json.JsonElement el)
        {
            switch (el.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    foreach (var p in el.EnumerateObject())
                    {
                        if (p.NameEquals("id") || p.NameEquals("fileId"))
                        {
                            if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String
                                && Guid.TryParse(p.Value.GetString(), out var g))
                            {
                                return g;
                            }
                        }

                        if (Find(p.Value) is { } found) return found;
                    }
                    return null;

                case System.Text.Json.JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray())
                    {
                        if (Find(item) is { } found) return found;
                    }
                    return null;

                default:
                    return null;
            }
        }
    }

    private static string Trim(string s) => s.Length <= 300 ? s : s[..300];
}
