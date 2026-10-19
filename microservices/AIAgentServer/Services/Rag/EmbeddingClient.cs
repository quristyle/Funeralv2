using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIAgentServer.Services.Rag;

/// <summary>글을 벡터로 바꾼다. LLM 장비의 임베딩 서버를 부른다. 주소를 따로 설정하지 않는다.</summary>
public sealed class EmbeddingClient
{
    private readonly HttpClient _http;
    private readonly AiProviderRegistry _registry;

    public EmbeddingClient(HttpClient http, AiProviderRegistry registry)
    {
        _http = http;
        _registry = registry;
    }

    public string Model { get; } = "bge-m3";

    /// <summary>
    /// 실제로 부르는 주소. 설정이 없거나 해석이 안 되면 null.
    /// <b>상태 화면이 「어디에 물었는지」를 보여 주려고 읽는다</b> —
    /// 개발 PC 와 운영이 서로 다른 주소를 쓰기 때문에, 그것만 보여도
    /// 「내 쪽 설정이 틀렸구나」를 알 수 있다.
    /// </summary>
    public string? Endpoint => ResolveUrl()?.ToString();

    private Uri? ResolveUrl()
    {
        var provider = _registry.Resolve("jsini");
        return Uri.TryCreate(provider.ApiBase, UriKind.Absolute, out var api)
            ? new UriBuilder(api) { Path = "/embed/v1/embeddings", Query = string.Empty }.Uri
            : null;
    }

    /// <summary>임베딩 서버에 보낼 글의 상한(글자). 문맥 8192 토큰에 넉넉히 들어간다.</summary>
    private const int MaxChars = 3000;

    /// <summary>여러 글을 한 번에 벡터로. <b>한 번에 너무 많이 보내지 않는다</b> — 임베딩 서버가 CPU 로 돌고 있어 큰 묶음은 타임아웃을 부른다.</summary>
    public async Task<List<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var provider = _registry.Resolve("jsini");
        var url = ResolveUrl()
                  ?? throw new InvalidOperationException("로컬 LLM 주소가 설정돼 있지 않습니다.");

        // 마지막 안전장치. 토막 하나가 길다고 색인 전체가 실패하면 안 된다 —
        // 한국어는 글자당 토큰이 1.5개를 넘기도 해서 글자 수로는 다 못 막는다.
        var safe = texts.Select(t => t.Length > MaxChars ? t[..MaxChars] : t).ToArray();
        var payload = JsonSerializer.Serialize(new { model = Model, input = safe });
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(3));

        using var res = await _http.SendAsync(req, cts.Token);
        var body = await res.Content.ReadAsStringAsync(cts.Token);

        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"임베딩 서버가 {(int)res.StatusCode} 로 답했습니다. {Trim(body)}");
        }

        using var doc = JsonDocument.Parse(body);
        var list = new List<float[]>();
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            var arr = item.GetProperty("embedding");
            var v = new float[arr.GetArrayLength()];
            var i = 0;
            foreach (var x in arr.EnumerateArray()) v[i++] = x.GetSingle();
            list.Add(Normalize(v));
        }

        return list;
    }

    public async Task<float[]> EmbedOneAsync(string text, CancellationToken ct = default)
        => (await EmbedAsync([text], ct))[0];

    /// <summary>길이를 1 로 맞춘다. 그러면 코사인 유사도가 <b>내적 하나</b>로 끝난다 — 검색할 때마다 제곱근을 두 번씩 구하지 않아도 된다.</summary>
    private static float[] Normalize(float[] v)
    {
        double sum = 0;
        foreach (var x in v) sum += x * x;
        var n = Math.Sqrt(sum);
        if (n <= 0) return v;
        for (var i = 0; i < v.Length; i++) v[i] = (float)(v[i] / n);
        return v;
    }

    private static string Trim(string s) => s.Length <= 200 ? s : s[..200];
}
