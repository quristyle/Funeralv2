using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>업무 비서(<c>/assistant/chat</c>) 대화. <c>AiChatClient.StreamAsync</c> 와 SSE 모양이
/// 같지만(답은 JSON 문자열, 안내는 <c>{notice,kind}</c> 객체) 끝점이 다르고,
/// 답이 글자 단위가 아니라 <b>한 번에 통째로</b> 온다(AIAgentServer 의 AssistantAgent.FinalizeAsync 가
/// 도구 루프를 다 돈 뒤 한 번만 쓴다) — 그래서 타이핑 커서를 그릴 자리가 없다.
/// 찾을 범위는 <see cref="AssistantProjectClient"/> 가 읽어 온 프로젝트에서 나온다.</summary>
public sealed class AssistantClient(HttpClient http)
{
    /// <summary>대화 내역을 보내고 안내·답 조각을 스트림으로 받는다. <paramref name="messages"/> 는 여태
    /// 오간 말 전부다 — 서버는 대화를 저장하지 않으므로(세션 칸이 없다) 매번 전체를 다시 보낸다.</summary>
    /// <param name="messages">여태 오간 말 전부. 문맥으로 함께 올라간다.</param>
    /// <param name="ragCategories">
    /// 찾을 범위를 좁힐 카테고리들(고른 프로젝트에 매인 것). <b>비우면 모든 카테고리</b> —
    /// 「고른 프로젝트가 없다」와 「범위를 안 정했다」를 같게 본다.
    /// </param>
    /// <param name="ragScopeLabel">안내에 보여 줄 범위 이름(고른 프로젝트).</param>
    /// <param name="cancellationToken">화면을 떠나거나 중단을 누르면 끊는다.</param>
    public async IAsyncEnumerable<AiChatPart> StreamAsync(
        IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<string>? ragCategories = null,
        string? ragScopeLabel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "ai/assistant/chat")
        {
            Content = JsonContent.Create(new { messages, ragCategories, ragScopeLabel }),
        };
        request.Headers.Accept.ParseAdd("text/event-stream");

        // ResponseHeadersRead 여야 스트림이 흐른다 — 기본값은 본문이 다 올 때까지 버퍼링한다.
        using var response = await http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"업무 비서 요청이 실패했습니다. ({(int)response.StatusCode})");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

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

            if (Parse(data) is { } part)
            {
                yield return part;
            }
        }
    }

    /// <summary>조각 하나를 해석한다. JSON 문자열이면 답, 객체면 안내다 — AiChatClient.Parse 와 같은 규약.
    /// 해석하지 못한 조각은 버린다. 스트림 전체를 죽이는 것보다 낫다.</summary>
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
