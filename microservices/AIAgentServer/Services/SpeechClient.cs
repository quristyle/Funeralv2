using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AIAgentServer.Services;

/// <summary>
/// 말소리를 글로 바꾼다(STT). 추론 장비의 받아쓰기 서버를 부른다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 브라우저가 가진 것을 안 쓰나]
/// </para>
/// <para>
/// 브라우저에도 받아쓰기가 있다(<c>SpeechRecognition</c>). 그런데 그것을 쓰면
/// 두 가지가 걸린다. <b>첫째, 아이폰에서는 아예 없다</b> — Safari 는 그 규격을
/// 지원하지 않아서 휴대폰 사용자의 절반이 음성 기능을 못 쓴다. <b>둘째, 말소리가
/// 바깥으로 나간다</b> — 크롬의 받아쓰기는 소리를 구글 서버로 보낸다. 사내 업무를
/// 말로 묻는 자리라 그것은 받아들일 수 없다.
/// </para>
/// <para>
/// 그래서 소리를 우리 장비로 보낸다. 아이폰도 되고(녹음은 어느 브라우저나 된다)
/// 소리가 사내에 머문다. 대신 <b>녹음이 끝나야 글이 나온다</b> — 말하는 동안
/// 글자가 따라 붙는 실시간 받아쓰기는 이 길로는 안 된다. 그것까지 하려면
/// 소리를 흘려보내는 길(WebSocket)을 따로 열어야 한다.
/// </para>
/// <para>
/// [주소를 어떻게 찾나]
/// </para>
/// <para>
/// <see cref="Rag.EmbeddingClient"/> 와 같다 — <c>jsini</c> 공급자의
/// <c>ApiBase</c> 에서 호스트만 떼어 <c>/stt/v1/audio/transcriptions</c> 를
/// 붙인다. 추론·임베딩·받아쓰기가 모두 같은 장비, 같은 nginx, 같은 키라서
/// 개발 PC 와 운영이 저절로 갈린다.
/// </para>
/// <para>
/// 다만 여기에는 <b>따로 적을 길(<c>Stt:Endpoint</c>)을 남겨 두었다.</b>
/// 받아쓰기는 임베딩과 달리 GPU 를 안 써도 되는 일이라, VRAM 이 모자라면
/// 다른 장비로 옮길 만하다. 적어 두면 그쪽이 이긴다.
/// </para>
/// </remarks>
public sealed class SpeechClient
{
    private readonly HttpClient _http;
    private readonly AiProviderRegistry _registry;
    private readonly IConfiguration _config;
    private readonly ILogger<SpeechClient> _logger;

    public SpeechClient(
        HttpClient http,
        AiProviderRegistry registry,
        IConfiguration config,
        ILogger<SpeechClient> logger)
    {
        _http = http;
        _registry = registry;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 받아쓰기에 쓸 모델. <b>기본값을 작은 것으로 둔다</b> — 코드가 가정하는
    /// 자리는 「VRAM 이 모자랄 수도 있는 장비」다. 운영은 설정으로 큰 것을
    /// 가리킨다(<c>deploy/stt/README.md</c>).
    /// </summary>
    public string Model => _config["Stt:Model"] ?? "Systran/faster-whisper-small";

    /// <summary>
    /// 한국어로 알아들으라고 일러 준다. 비우면 서버가 스스로 가린다.
    /// </summary>
    /// <remarks>
    /// <b>비워 두지 않는 편이 낫다.</b> 짧은 말("저장해 줘")은 알아들을 거리가
    /// 모자라서 영어나 일본어로 잘못 가려지는 일이 잦고, 그러면 글자가 통째로
    /// 엉뚱하게 나온다 — 못 알아들은 것이 아니라 다른 말로 알아들은 것이라
    /// 사람도 왜 그런지 모른다.
    /// </remarks>
    public string? Language => _config["Stt:Language"] ?? "ko";

    /// <summary>
    /// 받을 소리의 상한. 넘으면 서버에 보내지 않고 바로 돌려보낸다.
    /// </summary>
    /// <remarks>
    /// 한 번에 한 마디를 받는 자리라 길 까닭이 없다. 화면도 녹음을 2분에서
    /// 끊지만, 그것은 화면의 약속일 뿐이라 여기서 한 번 더 막는다.
    /// </remarks>
    public const int MaxBytes = 10 * 1024 * 1024;

    /// <summary>
    /// 실제로 부르는 주소. 설정이 없거나 해석이 안 되면 null.
    /// 상태 화면이 「어디에 물었는지」를 보여 주려고 읽는다.
    /// </summary>
    public string? Endpoint => ResolveUrl()?.ToString();

    private Uri? ResolveUrl()
    {
        // 따로 적어 두었으면 그쪽이 이긴다.
        if (Uri.TryCreate(_config["Stt:Endpoint"], UriKind.Absolute, out var explicitUrl))
        {
            return explicitUrl;
        }

        var provider = _registry.Resolve("jsini");
        return Uri.TryCreate(provider.ApiBase, UriKind.Absolute, out var api)
            ? new UriBuilder(api) { Path = "/stt/v1/audio/transcriptions", Query = string.Empty }.Uri
            : null;
    }

    /// <summary>
    /// 소리 한 토막을 글로 바꾼다.
    /// </summary>
    /// <param name="audio">녹음된 바이트. 브라우저가 준 그대로다.</param>
    /// <param name="contentType">
    /// 그 바이트의 종류(<c>audio/webm;codecs=opus</c> 등). <b>브라우저마다 다르다</b> —
    /// 크롬은 webm/opus, 사파리는 mp4/aac 를 준다. 받아쓰기 서버가 ffmpeg 로
    /// 풀어 주므로 여기서는 가리지 않고 그대로 넘긴다.
    /// </param>
    /// <param name="fileName">서버 로그에 남을 이름. 확장자만 쓸모가 있다.</param>
    public async Task<Transcript> TranscribeAsync(
        byte[] audio,
        string contentType,
        string fileName,
        CancellationToken ct = default)
    {
        if (audio.Length == 0)
        {
            return Transcript.Failed("녹음된 소리가 없습니다.");
        }

        if (audio.Length > MaxBytes)
        {
            return Transcript.Failed($"녹음이 너무 깁니다. ({audio.Length / 1024 / 1024}MB, 최대 {MaxBytes / 1024 / 1024}MB)");
        }

        var url = ResolveUrl();
        if (url is null)
        {
            return Transcript.Failed(
                "받아쓰기 서버 주소가 설정돼 있지 않습니다. (Stt:Endpoint 또는 AI:Providers:jsini)");
        }

        var provider = _registry.Resolve("jsini");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audio);
        file.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            ? parsed
            : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(Model), "model");

        if (Language is { Length: > 0 } lang)
        {
            form.Add(new StringContent(lang), "language");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        }

        // 받아쓰기는 소리 길이에 비례한다. 2분짜리를 CPU 로 풀면 1분이 걸리기도 해서
        // 임베딩(3분)과 비슷하게 잡는다 — 짧은 말은 어차피 1초 안에 돌아온다.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(3));

        var sw = Stopwatch.StartNew();
        try
        {
            using var res = await _http.SendAsync(req, cts.Token);
            var body = await res.Content.ReadAsStringAsync(cts.Token);
            sw.Stop();

            if (!res.IsSuccessStatusCode)
            {
                // **답 본문은 로그에만 둔다.** 이 글은 대화 화면에도 그대로
                // 뜨는데, 거기 상대 서버의 JSON 을 실으면 사람이 읽을 것이
                // 못 된다. 까닭을 가리는 데 쓸모 있는 것은 상태 코드이고
                // (deploy/stt/README.md 의 「안 될 때」 표가 그것으로 가른다)
                // 본문은 그 표를 이미 아는 사람이 로그에서 찾을 값이다.
                _logger.LogWarning(
                    "받아쓰기 서버가 {Status} 로 답했습니다. url={Url} body={Body}",
                    (int)res.StatusCode, url, Trim(body));

                return Transcript.Failed(
                    $"받아쓰기 서버가 {(int)res.StatusCode} 로 답했습니다.");
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.TryGetProperty("text", out var t)
                ? t.GetString()?.Trim() ?? string.Empty
                : string.Empty;

            return new Transcript(true, text, (int)sw.ElapsedMilliseconds, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Transcript.Failed("받아쓰기 서버가 제때 답하지 않았습니다. (3분)");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogWarning(ex, "받아쓰기 서버에 닿지 못했습니다. url={Url}", url);
            return Transcript.Failed($"받아쓰기 서버에 닿지 못했습니다: {ex.Message}");
        }
    }

    private static string Trim(string s) => s.Length <= 200 ? s : s[..200];
}

/// <summary>
/// 소리 한 토막을 글로 바꾼 결과.
/// </summary>
/// <param name="Ok">바꿨나. 거짓이면 <paramref name="Message"/> 에 까닭이 있다.</param>
/// <param name="Text">
/// 알아들은 글. <b>성공했는데 비어 있을 수 있다</b> — 아무 말도 없는 소리를
/// 보냈을 때가 그렇고, 그것은 고장이 아니다. 화면이 그 둘을 갈라 안내한다.
/// </param>
/// <param name="LatencyMs">서버가 답하기까지 걸린 시간.</param>
public sealed record Transcript(bool Ok, string Text, int? LatencyMs, string? Message)
{
    public static Transcript Failed(string message) => new(false, string.Empty, null, message);
}
