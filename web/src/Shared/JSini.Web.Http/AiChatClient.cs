using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.Http;

/// <summary>대화 한 줄. 서버(AIAgentServer)의 <c>Message</c> 와 같은 모양이다.</summary>
/// <param name="Role"><c>user</c> · <c>assistant</c> · <c>system</c></param>
/// <param name="Content">말한 내용</param>
public sealed record AiChatMessage(string Role, string Content);

/// <summary>스트림에서 받은 조각 하나. 답 글자(<see cref="Text"/>)이거나 안내(<see cref="Notice"/>)다. 안내를 답에 이어 붙이면 화면이 답의 일부로 저장하므로 갈라서 준다.</summary>
public sealed record AiChatPart(string? Text, string? Notice, string? Kind)
{
    /// <summary>지금 누가 답하는지 알리는 표식(<c>kind: "used"</c>). 안내 목록에 쌓지 않고 머리말의 배지를 갈아 끼운다. 자동 전환 때문에 고른 것과 답하는 것이 다를 수 있다.</summary>
    public bool IsUsedMarker => Kind == "used";
}

/// <summary>대화 한 줄기. 고르개의 한 줄이다.</summary>
/// <param name="Id">대화 열쇠.</param>
/// <param name="Title">주제. <c>null</c> 은 아직 말이 없는 대화다(「새 대화」로 그린다).</param>
/// <param name="UpdatedAt">마지막으로 말이 오간 때(UTC). 목록 차례의 기준.</param>
public sealed record AiChatSession(string Id, string? Title, DateTime UpdatedAt)
{
    /// <summary>고르개에 그릴 글자. 제목이 없으면 「새 대화」다.</summary>
    public string Label => string.IsNullOrWhiteSpace(Title) ? "새 대화" : Title;
}

/// <summary>녹음 한 마디를 글로 바꾼 결과. 성공해도 Text 가 비어 있을 수 있다(아무 말 없는 녹음은 고장이 아니다).</summary>
/// <param name="Ok">바꿨나.</param>
/// <param name="Text">알아들은 글.</param>
/// <param name="Message">못 바꾼 까닭. 바꿨으면 <c>null</c>.</param>
public sealed record AiTranscript(bool Ok, string Text, string? Message)
{
    public static AiTranscript Failed(string message) => new(false, string.Empty, message);

    /// <summary>서버는 멀쩡히 답했는데 알아들은 말이 없다. 고장이 아니다.</summary>
    public bool HeardNothing => Ok && string.IsNullOrWhiteSpace(Text);
}

/// <summary>지금 쓰이는 AI 한 줄. 물어보기 전에 보여 줄 기본값을 담는다.</summary>
/// <param name="Label">사람에게 보여 줄 이름(<c>Gemini Free · gemini-3.8-flash</c>).</param>
/// <param name="Configured">키까지 채워져 실제로 부를 수 있는 상태인지.</param>
public sealed record AiChatWho(string Label, bool Configured);

/// <summary>
/// AI 채팅 SSE 스트리밍(<c>POST /api/ai/chat/stream</c>). GatewayClient 는 봉투를 통째로 받아 벗기므로 text/event-stream 을 못 읽어 맨 HttpClient 를 쓴다.
/// 토큰은 공용 AuthTokenHandler 가 그대로 붙이므로 BFF 구도는 같다. 공급자는 보내지 않는다. 서버가 기본 공급자로 처리한다.
/// 레이아웃도 쓰므로 업무 모듈이 아닌 공용 자리에 둔다.
/// </summary>
public sealed class AiChatClient(HttpClient http)
{
    /// <summary>기본 공급자를 물어본다. 실제로 답한 AI 는 첫 답이 와야 알려지므로, 그 전에 배지에 보여 줄 설정상의 기본값이다. 실패하면 null 이며 대화를 막지 않는다(배지만 안 뜬다).</summary>
    public async Task<AiChatWho?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = await http.GetFromJsonAsync<JsonDocument>(
                "ai/providers", cancellationToken);

            if (document is null) return null;

            // 봉투: { data: { result: [ { defaultProvider, providers: [...] } ] } }
            if (!document.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array
                || result.GetArrayLength() == 0)
            {
                return null;
            }

            var state = result[0];
            var defaultKey = state.TryGetProperty("defaultProvider", out var dk)
                ? dk.GetString()
                : null;

            if (defaultKey is null
                || !state.TryGetProperty("providers", out var providers)
                || providers.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var p in providers.EnumerateArray())
            {
                if (p.TryGetProperty("key", out var key)
                    && key.GetString() == defaultKey)
                {
                    var name = p.TryGetProperty("displayName", out var n)
                        ? n.GetString() ?? defaultKey
                        : defaultKey;

                    var configured = p.TryGetProperty("configured", out var c)
                        && c.ValueKind == JsonValueKind.True;

                    return new AiChatWho(name, configured);
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            or NotSupportedException or TaskCanceledException)
        {
            // 곁들이 정보다. 못 받으면 배지를 접는다.
            return null;
        }
    }

    /// <summary>대화 내역을 보내고 답 조각을 스트림으로 받는다. 서버가 스트림을 닫거나 <c>[DONE]</c> 을 보내면 끝난다.</summary>
    /// <param name="messages">여태 오간 말 전부. 문맥으로 함께 올라간다.</param>
    /// <param name="sessionId">주면 서버가 오간 말을 대화로 담는다(<c>AiChatStore</c>). 비우면 아무 데도 안 남는다.</param>
    /// <param name="cancellationToken">서랍을 닫거나 화면을 떠나면 끊는다.</param>
    /// <param name="preferRag">사내 자료(RAG)를 먼저 뒤진다. 맞는 문서가 있으면 웹은 안 뒤진다.</param>
    public async IAsyncEnumerable<AiChatPart> StreamAsync(
        IReadOnlyList<AiChatMessage> messages,
        string? sessionId = null,
        bool preferRag = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "ai/chat/stream")
        {
            // System.Net.Http.Json 기본이 웹 규칙(camelCase)이라 서버 바인딩과 맞는다.
            Content = JsonContent.Create(new { messages, sessionId, preferRag }),
        };
        request.Headers.Accept.ParseAdd("text/event-stream");

        // ResponseHeadersRead 여야 스트림이 흐른다(기본값은 본문이 다 올 때까지 버퍼링한다).
        using var response = await http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"AI 응답 요청이 실패했습니다. ({(int)response.StatusCode})");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // 서버는 이벤트마다 data 한 줄을 보낸다 (`data: {JSON}\n\n`).
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            // 옛 규약의 종료 표식. 지금 서버는 스트림을 닫는 것으로 끝내지만 둘 다 받아 준다.
            if (data == "[DONE]")
            {
                yield break;
            }

            if (Parse(data) is { } part)
            {
                yield return part;
            }
        }
    }

    // ── 말소리를 글로 (STT) ─────────────────────────────────

    /// <summary>녹음한 한 마디를 글로 바꾼다. 브라우저는 게이트웨이를 직접 부르지 않고(CORS·토큰) 회로를 거쳐 여기서 붙인다.</summary>
    /// <remarks>못 바꿔도 던지지 않는다. 받아쓰기가 끊긴 것은 흔한 일이라 화면은 「글로 물어 주세요」를 안내한다.</remarks>
    /// <param name="base64">녹음된 바이트. <c>chat-voice.js</c> 의 <c>stop()</c> 이 준다.</param>
    /// <param name="contentType">그 바이트의 종류. 브라우저마다 다르다.</param>
    /// <param name="fileName">받아쓰기 서버 로그에 남을 이름. 확장자만 쓸모가 있다.</param>
    /// <param name="ct">화면을 떠나거나 음성 모드를 닫으면 끊는다.</param>
    public async Task<AiTranscript> TranscribeAsync(
        string base64,
        string contentType,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(
                "ai/stt",
                new { audioBase64 = base64, contentType, fileName },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return AiTranscript.Failed(
                    $"받아쓰기 요청이 실패했습니다. ({(int)response.StatusCode})");
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            // 봉투: { data: { result: [ { ok, text, latencyMs, message } ] } }
            if (!document.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array
                || result.GetArrayLength() == 0)
            {
                return AiTranscript.Failed("받아쓰기 결과를 읽지 못했습니다.");
            }

            var row = result[0];

            return new AiTranscript(
                Ok: row.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True,
                Text: row.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                Message: row.TryGetProperty("message", out var m) ? m.GetString() : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            or NotSupportedException or TaskCanceledException)
        {
            return AiTranscript.Failed($"받아쓰기 서버에 닿지 못했습니다 — {ex.Message}");
        }
    }

    // ── 대화 보관 ───────────────────────────────────────────
    // 봉투를 여기서 벗긴다(Unwrap). GatewayClient 를 따로 주입하면 같은 서비스로 가는 길이 둘이 된다.

    /// <summary>내 대화 목록. 최근에 말이 오간 순이다.</summary>
    public async Task<IReadOnlyList<AiChatSession>> ListSessionsAsync(
        CancellationToken ct = default)
        => await UnwrapListAsync<AiChatSession>(
            await http.GetAsync("ai/chat/sessions", ct), ct) ?? [];

    /// <summary>그 대화의 마디들. 못 열면 빈 목록.</summary>
    public async Task<IReadOnlyList<AiChatMessage>> GetSessionAsync(
        string sessionId, CancellationToken ct = default)
    {
        var rows = await UnwrapListAsync<StoredMessage>(
            await http.GetAsync($"ai/chat/sessions/{Uri.EscapeDataString(sessionId)}", ct), ct);

        return rows is null ? [] : [.. rows.Select(r => new AiChatMessage(r.Role, r.Content))];
    }

    /// <summary>빈 대화를 연다. 제목은 첫 질문이 들어올 때 붙는다.</summary>
    public async Task<AiChatSession?> CreateSessionAsync(CancellationToken ct = default)
    {
        var rows = await UnwrapListAsync<AiChatSession>(
            await http.PostAsync("ai/chat/sessions", content: null, ct), ct);

        return rows?.FirstOrDefault();
    }

    /// <summary>제목을 고친다. 비워 보내면 지운다.</summary>
    public async Task RenameSessionAsync(
        string sessionId, string? title, CancellationToken ct = default)
        => (await http.PutAsJsonAsync(
                $"ai/chat/sessions/{Uri.EscapeDataString(sessionId)}",
                new { title }, ct))
            .EnsureSuccessStatusCode();

    /// <summary>대화를 지운다. 마디도 함께 사라진다.</summary>
    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
        => (await http.DeleteAsync(
                $"ai/chat/sessions/{Uri.EscapeDataString(sessionId)}", ct))
            .EnsureSuccessStatusCode();

    /// <summary>서버에 담긴 한 마디. 역할과 내용만 쓴다.</summary>
    private sealed record StoredMessage(string Role, string Content);

    /// <summary>봉투를 벗겨 <c>data.result</c> 를 꺼낸다. 실패하면 null. 못 읽은 것과 빈 목록은 가른다. 뭉개면 화면이 「대화가 없다」로 그려 기록이 날아간 것으로 읽힌다.</summary>
    private static async Task<List<T>?> UnwrapListAsync<T>(
        HttpResponseMessage response, CancellationToken ct)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode) return null;

            try
            {
                using var document = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

                if (!document.RootElement.TryGetProperty("data", out var data)
                    || !data.TryGetProperty("result", out var result)
                    || result.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                return result.Deserialize<List<T>>(WireJson);
            }
            catch (Exception ex) when (ex is JsonException or HttpRequestException)
            {
                return null;
            }
        }
    }

    /// <summary>서버가 camelCase 로 보낸다.</summary>
    private static readonly JsonSerializerOptions WireJson =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>조각 하나를 해석한다. JSON 문자열이면 답 글자, 객체면 안내다. 해석하지 못한 조각은 버린다. 스트림 전체를 죽이는 것보다 낫다.</summary>
    private static AiChatPart? Parse(string data)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                return new AiChatPart(root.GetString(), null, null);
            }

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("notice", out var notice)
                && notice.ValueKind == JsonValueKind.String)
            {
                var kind = root.TryGetProperty("kind", out var k)
                    && k.ValueKind == JsonValueKind.String
                    ? k.GetString()
                    : null;

                return new AiChatPart(null, notice.GetString(), kind);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
