using System.Net.Http.Json;
using System.Text.Json;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이(:5265)로 나가는 유일한 통로.
///
/// [메서드 이름이 봉투 해석을 강제한다]
///
/// 봉투는 목록이든 객체 하나든 똑같이 <c>result</c> 배열이라, 봉투만 보고는 둘을
/// 구분할 수 없다. 구분은 엔드포인트를 아는 사람만 할 수 있으므로 그 선택을
/// 이름으로 드러낸다 — Vue 의 <c>unwrapList</c> · <c>unwrapOne</c> ·
/// <c>unwrapPage</c> 와 같은 구도다.
///
/// <list type="table">
///   <item><term>목록을 기대한다</term><description><see cref="GetListAsync{T}"/> — 항상 배열</description></item>
///   <item><term>객체 하나를 기대한다</term><description><see cref="GetOneAsync{T}"/> — 없으면 <c>null</c></description></item>
///   <item><term>총건수까지 필요하다</term><description><see cref="GetPageAsync{T}"/> — 서버 쪽 페이징</description></item>
/// </list>
///
/// [모듈은 자기 경로만 부른다]
///
/// 게이트웨이 경로가 곧 서비스 경계다(<c>/api/funeral/…</c>, <c>/api/helpdesk/…</c>).
/// 모듈이 남의 경로를 부르면 아키텍처 테스트가 잡는다 — 그 순간 모듈 사이에
/// 백엔드를 경유한 결합이 생기기 때문이다.
/// </summary>
public sealed class GatewayClient(HttpClient http)
{
    /// <summary>
    /// 봉투를 벗길 때 쓰는 옵션.
    ///
    /// 백엔드가 camelCase 로 내려주므로 대소문자를 가리지 않게 열어 둔다 —
    /// 서비스가 열 개가 넘고 일부는 직렬화 설정을 따로 두고 있어서, 여기서
    /// 엄격하게 굴면 특정 서비스만 조용히 <c>null</c> 이 된다.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>목록을 읽는다. 무엇이 와도 배열이다(비어 있을 수는 있다).</summary>
    public async Task<IReadOnlyList<T>> GetListAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);
        return payload?.Result ?? [];
    }

    /// <summary>
    /// 객체 하나를 읽는다. 봉투가 객체 하나도 <c>result: [obj]</c> 로 싣기 때문에
    /// 첫 칸을 꺼낸다. 비어 있으면 <c>null</c>.
    /// </summary>
    public async Task<T?> GetOneAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);
        return payload?.Result is { Count: > 0 } rows ? rows[0] : default;
    }

    /// <summary>
    /// 목록과 총건수를 함께 읽는다. 서버 쪽 페이징을 하는 그리드가 쓴다.
    /// <c>page.total</c> 이 없으면 받은 건수를 총건수로 본다.
    /// </summary>
    public async Task<(IReadOnlyList<T> Items, int Total)> GetPageAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);
        var items = payload?.Result ?? [];
        return (items, payload?.Page?.Total ?? items.Count);
    }

    /// <summary>보내고 결과 객체 하나를 받는다 (등록·수정).</summary>
    public async Task<T?> PostAsync<T>(
        string path,
        object? body,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Post, path, body, cancellationToken);
        return payload?.Result is { Count: > 0 } rows ? rows[0] : default;
    }

    /// <summary>보내고 결과는 쓰지 않는다. 실패하면 <see cref="ApiException"/>.</summary>
    public Task PostAsync(
        string path,
        object? body,
        CancellationToken cancellationToken = default)
        => SendAsync<object>(HttpMethod.Post, path, body, cancellationToken);

    /// <summary>수정하고 결과 객체 하나를 받는다.</summary>
    public async Task<T?> PutAsync<T>(
        string path,
        object? body,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Put, path, body, cancellationToken);
        return payload?.Result is { Count: > 0 } rows ? rows[0] : default;
    }

    /// <summary>수정하고 결과는 쓰지 않는다.</summary>
    public Task PutAsync(
        string path,
        object? body,
        CancellationToken cancellationToken = default)
        => SendAsync<object>(HttpMethod.Put, path, body, cancellationToken);

    /// <summary>지운다.</summary>
    public Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default)
        => SendAsync<object>(HttpMethod.Delete, path, null, cancellationToken);

    /// <summary>
    /// <c>data.result</c> 가 배열이 아니라 <b>객체 하나</b>인 응답을 읽는다.
    ///
    /// 서버가 <c>Result</c>+<c>TotalCount</c> 를 가진 DTO 를 돌려줄 때의 모양이다
    /// (<c>ApiResponse.IsPassThroughPagedData</c>). 프로젝트관리처럼 행과 함께
    /// 컬럼 메타를 돌려주는 서비스가 이 모양을 쓴다.
    ///
    /// <see cref="GetOneAsync{T}"/> 와 헷갈리기 쉬운데 다른 것이다 —
    /// 그쪽은 <c>result</c> 가 배열이고 그 <b>첫 칸</b>을 꺼낸다.
    /// </summary>
    public async Task<T?> PostObjectAsync<T>(
        string path,
        object? body,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(
                "서버에 연결하지 못했습니다.", statusCode: null, innerException: ex);
        }

        using (response)
        {
            await EnsureHttpSuccessAsync(response, path, cancellationToken);

            if (response.Content.Headers.ContentLength == 0)
            {
                return default;
            }

            ApiObjectEnvelope<T>? envelope;
            try
            {
                envelope = await response.Content
                    .ReadFromJsonAsync<ApiObjectEnvelope<T>>(JsonOptions, cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new ApiException(
                    $"응답을 해석하지 못했습니다. ({path})",
                    response.StatusCode, innerException: ex);
            }

            if (envelope is null)
            {
                return default;
            }

            if (!envelope.Success || envelope.Code != "S000")
            {
                throw new ApiException(
                    string.IsNullOrWhiteSpace(envelope.Message)
                        ? "요청을 처리하지 못했습니다."
                        : envelope.Message,
                    response.StatusCode,
                    envelope.Code,
                    envelope.TraceId,
                    envelope.Errors);
            }

            return envelope.Data is null ? default : envelope.Data.Result;
        }
    }

    /// <summary>
    /// 봉투를 쓰지 않는 응답을 그대로 받는다.
    ///
    /// 헬프데스크·프로젝트관리는 옛 시스템에서 이식한 코드라 봉투 없이 맨 JSON 을
    /// 내려주는 엔드포인트가 남아 있다. 그런 곳에만 쓴다 — 새로 만드는 API 에는
    /// 쓰지 않는다.
    /// </summary>
    public async Task<T?> GetRawAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        await EnsureHttpSuccessAsync(response, path, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>파일을 내려받는다. 바이트를 메모리에 다 올리지 않도록 스트림으로 준다.</summary>
    public async Task<Stream> GetStreamAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(
            path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureHttpSuccessAsync(response, path, cancellationToken);
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <summary>보내고, HTTP 와 봉투를 모두 확인한 뒤 안쪽만 돌려준다.</summary>
    private async Task<ResultPayload<T>?> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // 게이트웨이가 죽었거나 망이 끊긴 경우. 상태 코드가 없다.
            throw new ApiException(
                "서버에 연결하지 못했습니다.", statusCode: null, innerException: ex);
        }

        using (response)
        {
            await EnsureHttpSuccessAsync(response, path, cancellationToken);

            // 204 · 빈 본문. 지우기 같은 곳에서 정상이다.
            if (response.Content.Headers.ContentLength == 0)
            {
                return null;
            }

            ApiEnvelope<T>? envelope;
            try
            {
                envelope = await response.Content
                    .ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions, cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new ApiException(
                    $"응답을 해석하지 못했습니다. ({path})",
                    response.StatusCode, innerException: ex);
            }

            if (envelope is null)
            {
                return null;
            }

            // HTTP 200 인데 업무 규칙에서 막힌 경우. 봉투의 code 로만 알 수 있다.
            if (!envelope.Success || envelope.Code != "S000")
            {
                throw new ApiException(
                    string.IsNullOrWhiteSpace(envelope.Message)
                        ? "요청을 처리하지 못했습니다."
                        : envelope.Message,
                    response.StatusCode,
                    envelope.Code,
                    envelope.TraceId,
                    envelope.Errors);
            }

            return envelope.Data;
        }
    }

    /// <summary>
    /// HTTP 실패를 <see cref="ApiException"/> 으로 바꾼다.
    ///
    /// 실패 응답에도 봉투가 실려 오는 경우가 많으므로(게이트웨이·서비스가 모두
    /// 봉투로 답한다) 먼저 봉투에서 메시지를 꺼내 본다. 못 꺼내면 상태 코드로
    /// 만든 일반 메시지를 쓴다.
    /// </summary>
    private static async Task EnsureHttpSuccessAsync(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? message = null;
        string? code = null;
        string? traceId = null;
        IReadOnlyList<ErrorDetail>? errors = null;

        try
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions, cancellationToken);
            message = envelope?.Message;
            code = envelope?.Code;
            traceId = envelope?.TraceId;
            errors = envelope?.Errors;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // 봉투가 아닌 본문(HTML 오류 페이지 등). 상태 코드로만 판단한다.
        }

        throw new ApiException(
            string.IsNullOrWhiteSpace(message) ? DefaultMessage(response, path) : message,
            response.StatusCode,
            code,
            traceId,
            errors);
    }

    private static string DefaultMessage(HttpResponseMessage response, string path) =>
        (int)response.StatusCode switch
        {
            401 => "로그인이 필요합니다.",
            403 => "권한이 없습니다.",
            404 => $"요청한 자원을 찾을 수 없습니다. ({path})",
            429 => "요청이 너무 잦습니다. 잠시 뒤 다시 시도해 주세요.",
            >= 500 => "서버에서 오류가 발생했습니다.",
            _ => $"요청을 처리하지 못했습니다. ({(int)response.StatusCode})",
        };
}
