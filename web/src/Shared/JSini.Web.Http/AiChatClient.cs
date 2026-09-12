using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
    public async IAsyncEnumerable<AiChatPart> StreamAsync(
        IReadOnlyList<AiChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "ai/chat/stream")
        {
            // System.Net.Http.Json 기본이 웹 규칙(camelCase)이라 서버 바인딩과 맞는다.
            Content = JsonContent.Create(new { messages }),
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
