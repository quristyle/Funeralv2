using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace JSini.Web.Site.Api;

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
public sealed record AiChatPart(string? Text, string? Notice, string? Kind);

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
/// </summary>
public sealed class AiChatClient(HttpClient http)
{
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
