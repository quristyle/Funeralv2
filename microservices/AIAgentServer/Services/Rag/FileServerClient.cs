using System.Net.Http.Headers;
using JSini.Shared.DTOs;

namespace AIAgentServer.Services.Rag;

/// <summary>
/// RAG 문서의 실물을 맡아 두는 곳 — 파일 서버.
/// </summary>
/// <remarks>
/// <para>
/// <b>처음에는 제 디스크에 직접 썼다</b>(<c>Rag:UploadRoot</c>). 요건("문서는
/// 운영시스템에 보관한다")은 그것으로도 채워졌지만, 이 시스템의 규칙은
/// <b>파일은 파일 서버를 거친다</b>는 것이다. 어긴 값을 치렀다 — 볼륨이 둘로
/// 갈려 백업도 둘이 되고, <b>내려받기가 아예 안 됐다.</b> 포털은 브라우저에
/// 토큰을 안 내리므로(BFF) 첨부는 셸이 중계해야 하는데, 그 중계는 파일
/// 서버의 아이디를 받게 되어 있어서 RAG 문서만 거기 낄 자리가 없었다.
/// </para>
/// <para>
/// <b>게이트웨이를 거치지 않고 직접 부른다.</b> 컨테이너망 안의 서비스끼리는
/// 그렇게 부르고 있고(<c>Mcp:Endpoint</c> · <c>Stt:Endpoint</c> 와 같은 자리),
/// 게이트웨이를 끼우면 색인 한 번에 문서 수만큼 왕복이 한 겹 더 는다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 우리가 파일 서버에 대는 이름.
    /// </summary>
    /// <remarks>
    /// <b>신원을 안 실으면 못 읽는다.</b> 파일 서버는 <c>X-User-Id</c> 가 없는
    /// 읽기를 익명으로 보고 <c>is_public</c> 인 파일만 내준다
    /// (<c>PublicFileAccessFilter</c>) — RAG 문서는 공개가 아니라 **404 가 난다.**
    /// 실제로 그래서 내려받기가 통째로 비었다.
    /// <para>
    /// 색인은 사람이 시키는 일이 아니라 댈 사람이 없다. 알림 서버가 쓰는
    /// <c>system:</c> 머리를 그대로 따른다 — 기록에서 사람과 갈라 보인다.
    /// </para>
    /// </remarks>
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
            // 실제로 밟았다 — 파일 서버가 바깥 호스트로 302 를 주는 환경에서
            // 그 호스트가 안 풀려 색인이 첫 문서에서 끝났다.
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

    /// <summary>지운다. <b>실패해도 던지지 않는다</b> — 아래 머리말 참고.</summary>
    /// <remarks>
    /// 지우는 쪽은 이미 우리 표에서 줄을 뺀 뒤다. 여기서 던지면 **표에는 없는데
    /// 사람에게는 실패로 보이는** 어긋남이 남는다. 남은 파일은 임자 없는
    /// 바이트일 뿐이라 기록만 남기고 넘어간다.
    /// </remarks>
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

    /// <summary>
    /// 응답에서 아이디를 꺼낸다.
    /// </summary>
    /// <remarks>
    /// <b>봉투 모양을 하나로 못 박지 않는다.</b> 이 서버의 응답은 자리마다
    /// <c>data.result[0]</c> 이기도 하고 <c>data</c> 이기도 해서
    /// (<c>ApiResponse</c> 의 <c>BuildSerializedData</c>), 한 모양만 읽으면
    /// 어느 날 조용히 <c>null</c> 이 된다. 첫 번째로 나오는 <c>id</c> 꼴의
    /// GUID 를 집는다.
    /// </remarks>
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
