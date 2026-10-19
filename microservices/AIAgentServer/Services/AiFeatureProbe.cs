using System.Diagnostics;
using System.Text.Json;
using AIAgentServer.Services.Rag;

namespace AIAgentServer.Services;

/// <summary>사내 문서 검색(RAG)과 웹 검색(MCP)이 <b>실제로 도는지</b> 눌러 본다. 「RAG 켜짐 · 토막 1,842개」까지는 설정과 메모리만 읽어도 적을 수 있다.</summary>
public sealed class AiFeatureProbe
{
    /// <summary>재어 둔 값을 쓰는 동안. 사람이 누르면 이것과 상관없이 다시 묻는다.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    /// <summary>임베딩 서버에 보내 볼 글. <b>짧아야 한다</b> — 상태를 보려고 CPU 를 오래 물릴 까닭이 없다. 길이가 아니라 "답이 오는가" 를 보는 것이다.</summary>
    private const string Ping = "상태 확인";

    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AiFeatureProbe> _logger;

    private readonly SemaphoreSlim _embedGate = new(1, 1);
    private readonly SemaphoreSlim _mcpGate = new(1, 1);
    private readonly SemaphoreSlim _sttGate = new(1, 1);

    private EmbeddingProbe? _embed;
    private McpServerProbe? _mcp;
    private SpeechProbe? _stt;

    public AiFeatureProbe(
        IServiceScopeFactory scopes,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<AiFeatureProbe> logger)
    {
        _scopes = scopes;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    // ── 임베딩 서버 ─────────────────────────────────────────────

    /// <param name="force">재어 둔 값을 버리고 지금 다시 묻는다.</param>
    public async Task<EmbeddingProbe> EmbeddingAsync(bool force = false, CancellationToken ct = default)
    {
        if (!force && Fresh(_embed?.CheckedAt)) return _embed!;

        await _embedGate.WaitAsync(ct);
        try
        {
            if (!force && Fresh(_embed?.CheckedAt)) return _embed!;
            return _embed = await MeasureEmbeddingAsync(ct);
        }
        finally
        {
            _embedGate.Release();
        }
    }

    private async Task<EmbeddingProbe> MeasureEmbeddingAsync(CancellationToken ct)
    {
        // 임베딩은 스코프 서비스가 아니지만, 타입드 HttpClient 를 싱글턴이 들고
        // 있으면 핸들러가 영영 돌지 않는다. 부를 때마다 꺼내 쓴다.
        using var scope = _scopes.CreateScope();
        var embed = scope.ServiceProvider.GetRequiredService<EmbeddingClient>();

        var endpoint = embed.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new EmbeddingProbe(false, embed.Model, 0, null, null,
                "로컬 LLM 주소가 설정돼 있지 않습니다. (AI:Providers:jsini 또는 LLM:ApiBase)",
                DateTime.UtcNow);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(20));

            var v = await embed.EmbedOneAsync(Ping, cts.Token);
            sw.Stop();

            return new EmbeddingProbe(
                Reachable: v.Length > 0,
                Model: embed.Model,
                Dimensions: v.Length,
                LatencyMs: (int)sw.ElapsedMilliseconds,
                Endpoint: endpoint,
                Message: v.Length > 0 ? null : "임베딩 서버가 빈 벡터를 주었습니다.",
                CheckedAt: DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new EmbeddingProbe(false, embed.Model, 0, (int)sw.ElapsedMilliseconds, endpoint,
                "임베딩 서버가 제때 답하지 않았습니다. (20초)", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "임베딩 서버를 눌러 보지 못했습니다. endpoint={Endpoint}", endpoint);
            return new EmbeddingProbe(false, embed.Model, 0, (int)sw.ElapsedMilliseconds, endpoint,
                $"임베딩 서버에 닿지 못했습니다: {ex.Message}", DateTime.UtcNow);
        }
    }

    // ── MCP 도구 서버 ───────────────────────────────────────────

    /// <summary>McpServer 자신에게 「너는 어떤 검색을 쓰느냐」를 묻는다. 검색 뒷단(DuckDuckGo · SearXNG · Brave)은 McpServer 설정이 정한다.</summary>
    public async Task<McpServerProbe> McpServerAsync(bool force = false, CancellationToken ct = default)
    {
        if (!force && Fresh(_mcp?.CheckedAt)) return _mcp!;

        await _mcpGate.WaitAsync(ct);
        try
        {
            if (!force && Fresh(_mcp?.CheckedAt)) return _mcp!;
            return _mcp = await MeasureMcpAsync(ct);
        }
        finally
        {
            _mcpGate.Release();
        }
    }

    private async Task<McpServerProbe> MeasureMcpAsync(CancellationToken ct)
    {
        var configured = _config["Mcp:Endpoint"];
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var mcp))
        {
            return new McpServerProbe(false, null, null, null,
                "MCP 서버 주소가 설정돼 있지 않습니다. (Mcp:Endpoint)", DateTime.UtcNow);
        }

        // 설정에 적힌 것은 MCP 규격 길(/mcp)이다. 상태는 같은 서버의 /tools 가 준다.
        var url = new UriBuilder(mcp) { Path = "/tools", Query = string.Empty }.Uri;

        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var http = _httpFactory.CreateClient();
            using var res = await http.GetAsync(url, cts.Token);
            var body = await res.Content.ReadAsStringAsync(cts.Token);
            sw.Stop();

            if (!res.IsSuccessStatusCode)
            {
                return new McpServerProbe(false, null, url.ToString(), (int)sw.ElapsedMilliseconds,
                    $"MCP 서버가 {(int)res.StatusCode} 로 답했습니다.", DateTime.UtcNow);
            }

            using var doc = JsonDocument.Parse(body);
            var provider = doc.RootElement.TryGetProperty("provider", out var p)
                ? p.GetString()
                : null;

            return new McpServerProbe(true, provider, url.ToString(),
                (int)sw.ElapsedMilliseconds, null, DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new McpServerProbe(false, null, url.ToString(), (int)sw.ElapsedMilliseconds,
                "MCP 서버가 제때 답하지 않았습니다. (5초)", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP 서버를 눌러 보지 못했습니다. url={Url}", url);
            return new McpServerProbe(false, null, url.ToString(), (int)sw.ElapsedMilliseconds,
                $"MCP 서버에 닿지 못했습니다: {ex.Message}", DateTime.UtcNow);
        }
    }

    // ── 받아쓰기 서버 ───────────────────────────────────────────

    /// <param name="force">재어 둔 값을 버리고 지금 다시 묻는다.</param>
    public async Task<SpeechProbe> SpeechAsync(bool force = false, CancellationToken ct = default)
    {
        if (!force && Fresh(_stt?.CheckedAt)) return _stt!;

        await _sttGate.WaitAsync(ct);
        try
        {
            if (!force && Fresh(_stt?.CheckedAt)) return _stt!;
            return _stt = await MeasureSpeechAsync(ct);
        }
        finally
        {
            _sttGate.Release();
        }
    }

    /// <summary>받아쓰기 서버를 <b>실제로 한 번 눌러 본다.</b> 모델 목록만 물어도 「서버가 떠 있나」는 알 수 있다.</summary>
    private async Task<SpeechProbe> MeasureSpeechAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var speech = scope.ServiceProvider.GetRequiredService<SpeechClient>();

        var endpoint = speech.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new SpeechProbe(false, speech.Model, speech.Language, null, null,
                "받아쓰기 서버 주소가 설정돼 있지 않습니다. (Stt:Endpoint 또는 AI:Providers:jsini)",
                DateTime.UtcNow);
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var r = await speech.TranscribeAsync(SilenceWav(), "audio/wav", "probe.wav", cts.Token);

            return new SpeechProbe(
                Reachable: r.Ok,
                Model: speech.Model,
                Language: speech.Language,
                LatencyMs: r.LatencyMs,
                Endpoint: endpoint,
                Message: r.Message,
                CheckedAt: DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new SpeechProbe(false, speech.Model, speech.Language, null, endpoint,
                "받아쓰기 서버가 제때 답하지 않았습니다. (30초)", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "받아쓰기 서버를 눌러 보지 못했습니다. endpoint={Endpoint}", endpoint);
            return new SpeechProbe(false, speech.Model, speech.Language, null, endpoint,
                $"받아쓰기 서버에 닿지 못했습니다: {ex.Message}", DateTime.UtcNow);
        }
    }

    /// <summary>0.2초짜리 무음 WAV. 16kHz · 16bit · 모노 — whisper 가 안쪽에서 맞추는 모양이라 변환 없이 바로 들어간다.</summary>
    private static byte[] SilenceWav()
    {
        const int sampleRate = 16000;
        const int samples = sampleRate / 5;
        const int dataBytes = samples * 2;

        var buf = new byte[44 + dataBytes];
        var w = new BinaryWriter(new MemoryStream(buf));

        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);                      // fmt 길이
        w.Write((short)1);                // PCM
        w.Write((short)1);                // 모노
        w.Write(sampleRate);
        w.Write(sampleRate * 2);          // 초당 바이트
        w.Write((short)2);                // 한 칸 바이트
        w.Write((short)16);               // 비트
        w.Write("data"u8);
        w.Write(dataBytes);
        // 나머지는 0 — 그것이 무음이다.

        return buf;
    }

    private static bool Fresh(DateTime? at) => at is { } t && DateTime.UtcNow - t < Ttl;
}

/// <summary>받아쓰기 서버를 한 번 눌러 본 결과.</summary>
/// <param name="Language">
/// 알아들을 말을 일러 주었나. 비어 있으면 서버가 스스로 가린다 —
/// <b>짧은 말이 엉뚱한 언어로 가려지는</b> 자리라 보통은 채워 둔다.
/// </param>
public sealed record SpeechProbe(
    bool Reachable,
    string Model,
    string? Language,
    int? LatencyMs,
    string? Endpoint,
    string? Message,
    DateTime CheckedAt);

/// <summary>임베딩 서버를 한 번 눌러 본 결과.</summary>
/// <param name="Dimensions">
/// 돌아온 벡터의 차원. <b>색인에 적힌 차원과 다르면 모델이 바뀐 것</b>이고,
/// 그때는 색인을 통째로 다시 만들어야 한다 — 차원이 다른 벡터끼리는 견줄 수 없다.
/// </param>
/// <param name="LatencyMs">짧은 글 하나를 벡터로 바꾸는 데 걸린 시간.</param>
public sealed record EmbeddingProbe(
    bool Reachable,
    string Model,
    int Dimensions,
    int? LatencyMs,
    string? Endpoint,
    string? Message,
    DateTime CheckedAt);

/// <summary>MCP 도구 서버를 눌러 본 결과.</summary>
/// <param name="SearchProvider">그 서버가 쓰는 검색 뒷단(duckduckgo · searxng · brave).</param>
public sealed record McpServerProbe(
    bool Reachable,
    string? SearchProvider,
    string? Endpoint,
    int? LatencyMs,
    string? Message,
    DateTime CheckedAt);
