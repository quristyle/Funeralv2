using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>업무 비서(<c>/assistant/*</c>) 대화·자료 범위 조회. <c>AiChatClient.StreamAsync</c> 와 SSE 모양이
/// 같지만(답은 JSON 문자열, 안내는 <c>{notice,kind}</c> 객체) 끝점이 다르고(<c>ai/assistant/chat</c>),
/// 답이 글자 단위가 아니라 <b>한 번에 통째로</b> 온다(AIAgentServer 의 AssistantAgent.FinalizeAsync 가
/// 도구 루프를 다 돈 뒤 한 번만 쓴다) — 그래서 타이핑 커서를 그릴 자리가 없다.
/// 범위 조회(<c>/scopes</c>)는 일반 JSON 이라 <see cref="GatewayClient"/> 를 그대로 쓴다.</summary>
public sealed class AssistantClient(GatewayClient gateway, HttpClient http)
{
    /// <summary>찾아볼 자료 범위(RAG 카테고리) 전부. 이 화면은 메뉴 권한으로 이미 좁혀진 사람만 열므로
    /// 회사·분류로 다시 거르지 않는다 — 분류(폴더)도 안 내려와 카테고리 단위로만 고른다.</summary>
    public Task<IReadOnlyList<AssistantScope>> ScopesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<AssistantScope>("ai/assistant/scopes", ct);

    /// <summary>대화 내역을 보내고 안내·답 조각을 스트림으로 받는다. <paramref name="messages"/> 는 여태
    /// 오간 말 전부다 — 서버는 대화를 저장하지 않으므로(세션 칸이 없다) 매번 전체를 다시 보낸다.</summary>
    /// <param name="messages">여태 오간 말 전부. 문맥으로 함께 올라간다.</param>
    /// <param name="ragCategory">찾을 범위를 좁힐 카테고리. 비우면 모든 카테고리.</param>
    /// <param name="cancellationToken">화면을 떠나거나 중단을 누르면 끊는다.</param>
    public async IAsyncEnumerable<AiChatPart> StreamAsync(
        IReadOnlyList<AiChatMessage> messages,
        string? ragCategory = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "ai/assistant/chat")
        {
            Content = JsonContent.Create(new { messages, ragCategory }),
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

/// <summary>업무 비서가 찾아볼 수 있는 자료 범위 한 줄(RAG 카테고리). 이 화면은 메뉴 권한으로 이미
/// 좁혀진 사람(슈퍼관리자·임원)만 열어 <c>/scopes</c> 가 회사로 거르지 않고 전체를 준다.</summary>
public sealed record AssistantScope(string Id, string Name, string? Description, string? CompanyId, string? CompanyName);
