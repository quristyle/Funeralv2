using System.Collections;
using System.Net.Http.Json;
using System.Text.Json;
using JSini.Web.Http;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 헬프데스크 API 전용 클라이언트. 게이트웨이의 <c>/api/helpdesk/*</c> 만 부른다.
///
/// <see cref="GatewayClient"/> 를 쓰지 않는 이유는 <b>봉투가 다르기 때문</b>이다 —
/// 헬프데스크는 <c>{ success, message, data, meta, totalcount }</c> 로 답한다
/// (<see cref="HelpDeskEnvelope{T}"/> 참고). 토큰 처리는 같은
/// <c>AuthTokenHandler</c> 를 파이프라인에 끼워 그대로 물려받는다.
/// </summary>
public sealed class HelpDeskApi(HttpClient http)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── 기본 동사 ────────────────────────────────────────────────

    /// <summary>객체 하나를 읽는다. 본문이 비면 <c>default</c>.</summary>
    public Task<T?> GetAsync<T>(string path, object? query = null, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Get, WithQuery(path, query), null, ct);

    /// <summary>목록을 읽는다. 무엇이 와도 배열이다(비어 있을 수는 있다).</summary>
    public async Task<List<T>> GetListAsync<T>(string path, object? query = null, CancellationToken ct = default)
        => await SendAsync<List<T>>(HttpMethod.Get, WithQuery(path, query), null, ct) ?? [];

    /// <summary>보내고 결과 객체를 받는다 (등록).</summary>
    public Task<T?> PostAsync<T>(string path, object? body = null, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Post, path, body, ct);

    /// <summary>보내고 결과는 쓰지 않는다.</summary>
    public Task PostAsync(string path, object? body = null, CancellationToken ct = default)
        => SendAsync<JsonElement?>(HttpMethod.Post, path, body, ct);

    /// <summary>수정하고 결과 객체를 받는다.</summary>
    public Task<T?> PutAsync<T>(string path, object? body = null, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Put, path, body, ct);

    /// <summary>수정하고 결과는 쓰지 않는다.</summary>
    public Task PutAsync(string path, object? body = null, CancellationToken ct = default)
        => SendAsync<JsonElement?>(HttpMethod.Put, path, body, ct);

    /// <summary>지운다.</summary>
    public Task DeleteAsync(string path, CancellationToken ct = default)
        => SendAsync<JsonElement?>(HttpMethod.Delete, path, null, ct);

    // ── 페이징 목록 ──────────────────────────────────────────────

    /// <summary>
    /// 검색 조건을 본문에 담아 POST 하고 목록과 총건수를 함께 받는다.
    /// 헬프데스크 목록 조회 대부분이 이 모양이다 (<c>/requests/srch</c> 등 —
    /// DynamicFilterHelper 규약: <c>title_or_like</c>, <c>sorts</c>, <c>page</c>,
    /// <c>pageSize</c> …). Vue 의 <c>helpdeskFetchPage</c> 와 같다.
    /// </summary>
    public Task<HelpDeskPage<T>> SearchAsync<T>(string path, object? body = null, CancellationToken ct = default)
        => SendPageAsync<T>(HttpMethod.Post, path, body ?? new Dictionary<string, object?>(), ct);

    /// <summary>GET 방식 페이징 목록. 총건수 규약은 동일하다.</summary>
    public Task<HelpDeskPage<T>> GetPageAsync<T>(string path, object? query = null, CancellationToken ct = default)
        => SendPageAsync<T>(HttpMethod.Get, WithQuery(path, query), null, ct);

    // ── 파일 업로드 (multipart) ──────────────────────────────────

    /// <summary>
    /// 첨부와 함께 등록한다. <see cref="MultipartFormDataContent"/> 는 부르는
    /// 쪽이 조립한다 — 필드 구성이 화면마다 달라서 여기서 감출 수 없다.
    /// </summary>
    public Task<T?> PostMultipartAsync<T>(string path, MultipartFormDataContent content, CancellationToken ct = default)
        => SendRawAsync<T>(HttpMethod.Post, path, content, ct);

    /// <summary>첨부와 함께 수정한다.</summary>
    public Task<T?> PutMultipartAsync<T>(string path, MultipartFormDataContent content, CancellationToken ct = default)
        => SendRawAsync<T>(HttpMethod.Put, path, content, ct);

    // ── 안쪽 ─────────────────────────────────────────────────────

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        HttpContent? content = body is null ? null : JsonContent.Create(body, options: JsonOptions);
        return await SendRawAsync<T>(method, path, content, ct);
    }

    private async Task<T?> SendRawAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var envelope = await SendEnvelopeAsync<T>(method, path, content, ct);
        return envelope is null ? default : envelope.Data;
    }

    private async Task<HelpDeskPage<T>> SendPageAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        HttpContent? content = body is null ? null : JsonContent.Create(body, options: JsonOptions);
        var envelope = await SendEnvelopeAsync<List<T>>(method, path, content, ct);
        var items = envelope?.Data ?? [];
        return new HelpDeskPage<T>(
            items,
            envelope?.TotalCount ?? items.Count,
            envelope?.TotalPageCount ?? 1);
    }

    private async Task<HelpDeskEnvelope<T>?> SendEnvelopeAsync<T>(
        HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("서버에 연결하지 못했습니다.", statusCode: null, innerException: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // 실패에도 봉투가 실려 오는 경우가 많다. message 를 건져 본다.
                string? message = null;
                try
                {
                    var failed = await response.Content
                        .ReadFromJsonAsync<HelpDeskEnvelope<JsonElement?>>(JsonOptions, ct);
                    message = failed?.Message;
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException)
                {
                    // 봉투가 아닌 본문(HTML 오류 페이지 등).
                }

                throw new ApiException(
                    string.IsNullOrWhiteSpace(message) ? DefaultMessage(response, path) : message,
                    response.StatusCode);
            }

            if (response.Content.Headers.ContentLength == 0)
            {
                return null;
            }

            HelpDeskEnvelope<T>? envelope;
            try
            {
                envelope = await response.Content
                    .ReadFromJsonAsync<HelpDeskEnvelope<T>>(JsonOptions, ct);
            }
            catch (JsonException ex)
            {
                throw new ApiException($"응답을 해석하지 못했습니다. ({path})", response.StatusCode, innerException: ex);
            }

            if (envelope is null)
            {
                return null;
            }

            if (!envelope.IsSuccess)
            {
                throw new ApiException(
                    string.IsNullOrWhiteSpace(envelope.Message) ? "요청을 처리하지 못했습니다." : envelope.Message,
                    response.StatusCode);
            }

            return envelope;
        }
    }

    private static string DefaultMessage(HttpResponseMessage response, string path) =>
        (int)response.StatusCode switch
        {
            401 => "로그인이 필요합니다.",
            403 => "권한이 없습니다.",
            404 => $"요청한 자원을 찾을 수 없습니다. ({path})",
            >= 500 => "서버에서 오류가 발생했습니다.",
            _ => $"요청을 처리하지 못했습니다. ({(int)response.StatusCode})",
        };

    /// <summary>
    /// 쿼리스트링을 붙인다. 익명 객체나 사전을 받고, <c>null</c> 값은 뺀다 —
    /// Vue(axios) 가 params 의 undefined 를 빼던 것과 같은 동작이다.
    /// </summary>
    internal static string WithQuery(string path, object? query)
    {
        if (query is null)
        {
            return path;
        }

        var pairs = new List<string>();

        if (query is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                Append(pairs, entry.Key?.ToString(), entry.Value);
            }
        }
        else
        {
            foreach (var property in query.GetType().GetProperties())
            {
                Append(pairs, property.Name, property.GetValue(query));
            }
        }

        if (pairs.Count == 0)
        {
            return path;
        }

        var separator = path.Contains('?') ? '&' : '?';
        return path + separator + string.Join('&', pairs);

        static void Append(List<string> pairs, string? name, object? value)
        {
            if (string.IsNullOrEmpty(name) || value is null)
            {
                return;
            }

            var text = value switch
            {
                bool b => b ? "true" : "false",
                DateTime d => d.ToString("yyyy-MM-dd"),
                _ => value.ToString(),
            };

            if (text is null)
            {
                return;
            }

            pairs.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(text)}");
        }
    }
}
