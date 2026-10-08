using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.Http;

/// <summary>대화 한 줄. 서버(AIAgentServer)의 <c>Message</c> 와 같은 모양이다.</summary>
/// <param name="Role"><c>user</c> · <c>assistant</c> · <c>system</c></param>
/// <param name="Content">말한 내용</param>
public sealed record AiChatMessage(string Role, string Content);

/// <summary>
/// 스트림에서 받은 조각 하나. 답 글자(<see cref="Text"/>)이거나
/// 안내(<see cref="Notice"/>)다 — 둘이 동시에 채워지지 않는다.
///
/// 안내는 **답 글자가 아니다.** 답에 이어 붙이면 화면이 그것을 답의 일부로
/// 저장하고 다음 턴 문맥으로 다시 올려보낸다(Vue 의 <c>streamChatMessage</c>
/// 주석 참고). 그래서 갈라서 준다.
/// </summary>
public sealed record AiChatPart(string? Text, string? Notice, string? Kind)
{
    /// <summary>
    /// <b>지금 누가 답하고 있는지</b> 알리는 표식인가(<c>kind: "used"</c>).
    /// </summary>
    /// <remarks>
    /// 안내 목록에 쌓지 않고 <b>머리말의 배지 하나를 갈아 끼우는</b> 조각이다.
    /// 매 턴 맨 앞에 온다 — 자동 전환 때문에 고른 것과 답하는 것이 다를 수 있어서,
    /// '바뀐 순간' 에만 뜨는 전환 안내만으로는 지금 상태를 알 수 없기 때문이다.
    /// </remarks>
    public bool IsUsedMarker => Kind == "used";
}

/// <summary>
/// 대화 한 줄기(주제 하나). 고르개의 한 줄이다.
/// </summary>
/// <param name="Id">대화 열쇠.</param>
/// <param name="Title">
/// 주제. <b><c>null</c> 은 아직 아무 말도 안 한 대화</b>다 — 화면이
/// 「새 대화」로 그린다.
/// </param>
/// <param name="UpdatedAt">마지막으로 말이 오간 때(UTC). 목록 차례의 기준.</param>
public sealed record AiChatSession(string Id, string? Title, DateTime UpdatedAt)
{
    /// <summary>고르개에 그릴 글자. 제목이 없으면 「새 대화」다.</summary>
    public string Label => string.IsNullOrWhiteSpace(Title) ? "새 대화" : Title;
}

/// <summary>지금 쓰이는 AI 한 줄. 물어보기 전에 보여 줄 기본값을 담는다.</summary>
/// <param name="Label">사람에게 보여 줄 이름(<c>Gemini Free · gemini-3.8-flash</c>).</param>
/// <param name="Configured">키까지 채워져 실제로 부를 수 있는 상태인지.</param>
public sealed record AiChatWho(string Label, bool Configured);

/// <summary>
/// AI 채팅 SSE 스트리밍 (<c>POST /api/ai/chat/stream</c>).
///
/// [GatewayClient 를 쓰지 않는 이유]
///
/// <see cref="JSini.Web.Http.GatewayClient"/> 는 봉투(JSON)를 통째로 받아 벗기는
/// 클라이언트라 <c>text/event-stream</c> 을 흘려 읽을 수 없다. 그래서 이 앱
/// 로컬로 HttpClient 를 직접 쓴다 — 토큰 처리는 공용 <c>AuthTokenHandler</c> 를
/// 그대로 태우므로(SiteModule 등록) BFF 구도는 같다.
///
/// [공급자·모델을 보내지 않는다]
///
/// Vue 는 환경설정에서 고른 공급자(<c>currentAiProvider</c>)를 실어 보냈다.
/// Blazor 포털에는 아직 그 환경설정 화면이 없으므로 보내지 않는다 —
/// 서버가 기본 공급자로 처리한다(<c>ChatRequestDto.Provider</c> 주석 참고).
///
/// [왜 공유 자리에 있나 — 소개사이트 모듈에 있던 것을 옮겼다]
///
/// D11 로 헤더에서 여는 대화창을 되살리면서, 이 클라이언트를 **레이아웃**이
/// 써야 하게 됐다. 레이아웃은 공용(<c>JSini.Web.Components</c>)이고 업무
/// 모듈을 참조할 수 없다(의존 규칙 4). 화면 하나가 쓰던 것이 셸 기능이 된
/// 경우라 승격했다. 화면(<c>/site/ai/chat</c>)은 그대로 이것을 쓴다.
/// </summary>
public sealed class AiChatClient(HttpClient http)
{
    /// <summary>
    /// 기본 공급자가 무엇인지 물어본다. <b>물어보기 전에 보여 줄 값</b>이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 실제로 답한 AI 는 스트림이 <c>kind: "used"</c> 로 알려 주지만, 그것은
    /// <b>첫 답이 오고 나서야</b> 안다. 그 전까지 배지가 비어 있으면 "지금 무슨 AI 를
    /// 쓰고 있나" 라는 물음에 답하지 못하므로, 설정상의 기본값을 먼저 보여 준다.
    /// </para>
    /// <para>
    /// <b>실패하면 null 이다.</b> 이것은 곁들이 정보라 못 받았다고 대화를 막지 않는다 —
    /// 배지만 뜨지 않는다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 대화 내역을 보내고 답 조각을 스트림으로 받는다.
    /// 서버가 스트림을 닫거나 <c>[DONE]</c> 을 보내면 끝난다.
    /// </summary>
    /// <param name="messages">여태 오간 말 전부. 문맥으로 함께 올라간다.</param>
    /// <param name="sessionId">
    /// 담아 둘 대화. <b>주면 서버가 오간 말을 담는다</b>(<c>AiChatStore</c>) —
    /// 화면이 따로 올리지 않는다. 비우면 아무 데도 안 남는다.
    /// </param>
    /// <param name="cancellationToken">서랍을 닫거나 화면을 떠나면 끊는다.</param>
    public async IAsyncEnumerable<AiChatPart> StreamAsync(
        IReadOnlyList<AiChatMessage> messages,
        string? sessionId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "ai/chat/stream")
        {
            // System.Net.Http.Json 기본이 웹 규칙(camelCase)이라 서버 바인딩과 맞는다.
            Content = JsonContent.Create(new { messages, sessionId }),
        };
        request.Headers.Accept.ParseAdd("text/event-stream");

        // ResponseHeadersRead 가 핵심이다 — 기본(ResponseContentRead)은 본문이
        // 다 올 때까지 기다리므로 스트리밍이 통째로 버퍼링된다.
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

            // 옛 규약의 종료 표식. 지금 서버는 스트림을 닫는 것으로 끝내지만
            // Vue 쪽 처리를 그대로 이어받아 둘 다 받아 준다.
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

    // ── 대화 보관 ───────────────────────────────────────────
    //
    // **게이트웨이 봉투를 손으로 벗긴다.** 이 클라이언트는 스트리밍 때문에
    // `GatewayClient` 를 안 쓰는데(머리말), 그렇다고 그것 하나를 더 주입하면
    // 같은 서비스로 가는 길이 둘이 된다 — 주소 접두사가 갈라질 자리다.
    // 봉투 모양은 `ApiResponse` 하나뿐이라 한 곳에서 벗긴다(`Unwrap`).

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

    /// <summary>
    /// 봉투를 벗겨 <c>data.result</c> 를 꺼낸다. 실패하면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <b>못 읽은 것과 빈 목록을 가른다.</b> 둘을 뭉개면 대화를 못 읽었을 때
    /// 화면이 「대화가 하나도 없다」로 그리고, 사람은 그것을 기록이 날아간
    /// 것으로 읽는다.
    /// </remarks>
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

    /// <summary>
    /// 조각 하나를 해석한다. JSON <b>문자열</b>이면 답 글자, <b>객체</b>면 안내다
    /// (AIAgentServer 의 <c>/chat/stream</c> 이 그렇게 갈라 보낸다).
    /// 해석에 실패한 조각은 버린다 — 스트림 전체를 죽이는 것보다 낫다.
    /// </summary>
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
