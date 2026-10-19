using System.Net.Http.Json;
using System.Text.Json;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이(:5265)로 나가는 유일한 통로. 메서드 이름이 봉투 해석 방식을 정한다.
/// 목록은 <see cref="GetListAsync{T}"/>, 객체 하나는 <see cref="GetOneAsync{T}"/>, 총건수는 <see cref="GetPageAsync{T}"/>. 봉투만으로는 구분할 수 없다.
/// </summary>
public sealed class GatewayClient(HttpClient http)
{
    /// <summary>봉투를 벗길 때 쓰는 옵션(대소문자 무시). 서비스마다 직렬화 설정이 달라 엄격하면 일부만 조용히 null 이 된다.</summary>
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

    /// <summary>객체 하나를 읽는다. 봉투가 객체도 <c>result: [obj]</c> 로 싣으므로 첫 칸을 꺼낸다. 없으면 <c>null</c>.</summary>
    public async Task<T?> GetOneAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);
        return payload?.Result is { Count: > 0 } rows ? rows[0] : default;
    }

    /// <summary>목록과 총건수를 함께 읽는다(서버 쪽 페이징). <c>page.total</c> 이 없으면 받은 건수를 총건수로 본다.</summary>
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

    /// <summary><b>보낸 칸만</b> 고치고 결과는 쓰지 않는다. PutAsync 와 달리 한 벌을 통째로 덮지 않아 낡은 한 벌이 남의 변경을 되돌리지 않는다.</summary>
    public Task PatchAsync(
        string path,
        object? body,
        CancellationToken cancellationToken = default)
        => SendAsync<object>(HttpMethod.Patch, path, body, cancellationToken);

    /// <summary>지운다.</summary>
    public Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default)
        => SendAsync<object>(HttpMethod.Delete, path, null, cancellationToken);

    /// <summary>보내고 <b>바뀐 목록 전체</b>를 받는다. 서버가 순서를 정하므로 화면이 손으로 더하고 빼지 않게 한다.</summary>
    public async Task<IReadOnlyList<T>> PostListAsync<T>(
        string path,
        object? body,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Post, path, body, cancellationToken);
        return payload?.Result ?? [];
    }

    /// <summary><b>파일을 올리고</b> 담긴 목록을 받는다. JSON 으로 굳히는 <see cref="PostListAsync{T}"/> 로는 첨부를 보낼 수 없다.</summary>
    /// <remarks>바이트를 메모리에 올리지 않는다. 스트림을 담은 <see cref="MultipartFormDataContent"/> 의 <c>Dispose</c> 는 부르는 쪽 몫이다.</remarks>
    public async Task<IReadOnlyList<T>> PostFormListAsync<T>(
        string path,
        MultipartFormDataContent form,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;

        using (var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form })
        {
            try
            {
                response = await http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException(
                    "서버에 연결하지 못했습니다.", statusCode: null, innerException: ex);
            }
            finally
            {
            // 부르는 쪽이 든 content 를 우리가 Dispose 하지 않는다.
                request.Content = null;
            }
        }

        using (response)
        {
            await EnsureHttpSuccessAsync(response, path, cancellationToken);

            if (response.Content.Headers.ContentLength == 0)
            {
                return [];
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
                    "응답을 해석하지 못했습니다.", statusCode: response.StatusCode, innerException: ex);
            }

            if (envelope is { Success: false })
            {
                throw new ApiException(
                    envelope.Message ?? "요청이 거절되었습니다.",
                    statusCode: response.StatusCode,
                    code: envelope.Code);
            }

            return envelope?.Data?.Result ?? [];
        }
    }

    /// <summary>고치고 <b>바뀐 목록 전체</b>를 받는다. 이유는 <see cref="PostListAsync{T}"/> 와 같다.</summary>
    public async Task<IReadOnlyList<T>> PutListAsync<T>(
        string path,
        object? body,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Put, path, body, cancellationToken);
        return payload?.Result ?? [];
    }

    /// <summary>지우고 <b>바뀐 목록 전체</b>를 받는다. 이유는 <see cref="PostListAsync{T}"/> 와 같다.</summary>
    public async Task<IReadOnlyList<T>> DeleteListAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendAsync<T>(HttpMethod.Delete, path, null, cancellationToken);
        return payload?.Result ?? [];
    }

    /// <summary><c>data.result</c> 가 배열이 아니라 객체 하나인 응답을 읽는다(<c>ApiResponse.IsPassThroughPagedData</c>). <see cref="GetOneAsync{T}"/> 는 배열의 첫 칸을 꺼내므로 헷갈리기 쉽다.</summary>
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

    /// <summary>봉투를 쓰지 않는 맨 JSON 응답을 그대로 받는다. 옛 시스템에서 이식한 헬프데스크·프로젝트관리의 일부 엔드포인트에만 쓴다.</summary>
    public async Task<T?> GetRawAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        await EnsureHttpSuccessAsync(response, path, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// 응답을 해석하지 않고 돌려준다. 부르는 쪽이 <c>Dispose</c> 한다. 파일처럼 JSON 이 아닌 것에 쓴다(<c>FileDownload</c>).
    /// 상태 코드는 여기서 던지지 않는다. 404 와 403 을 다르게 다루는 쪽이 판단한다.
    /// </summary>
    /// <param name="method">HTTP 메서드</param>
    /// <param name="path">게이트웨이 기준 경로</param>
    /// <param name="content">실어 보낼 것(멀티파트 포함). 부르는 쪽이 만들고 <c>Dispose</c> 한다.</param>
    /// <param name="bearer">주면 지금 사용자 토큰 대신 이것이 나간다(<c>AuthTokenHandler</c> 는 이미 붙은 <c>Authorization</c> 을 덮지 않는다). 앱알림 아이콘 중계에서만 쓴다.</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<HttpResponseMessage> SendRawAsync(
        HttpMethod method,
        string path,
        HttpContent? content = null,
        string? bearer = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);

        if (content is not null)
        {
            request.Content = content;
        }

        if (!string.IsNullOrWhiteSpace(bearer))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
        }

        try
        {
            return await http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(
                "서버에 연결하지 못했습니다.", statusCode: null, innerException: ex);
        }
        finally
        {
            // request 를 버리면 content 도 함께 버려지므로 떼어 놓는다.
            request.Content = null;
        }
    }

    /// <summary>봉투 모양을 가리지 않고 객체 하나를 읽는다(<c>{data:{result:[…]}}</c> · <c>{data:…}</c> · 맨 JSON). 새 API 에는 쓰지 않는다. 이미 굳은 곳만 받는다.</summary>
    public async Task<T?> GetFlexibleAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        var payload = await ReadPayloadAsync(path, cancellationToken);

        return payload is null || payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? default
            : payload.Value.Deserialize<T>(JsonOptions);
    }

    /// <summary>봉투 모양을 가리지 않고 목록을 읽는다. 무엇이 와도 배열이다.</summary>
    /// <summary>유연한 봉투에서 목록과 <b>총건수</b>를 함께 읽는다. 총건수를 찾지 못하면 받은 건수를 총건수로 본다(없는 숫자를 지어내지 않는다).</summary>
    /// <remarks>화면은 총건수로 「잘렸다」를 알려야 한다. 페이지 크기만큼만 받고 전부처럼 그리면 사용자가 그게 전부라고 읽는다.</remarks>
    public async Task<(IReadOnlyList<T> Items, int Total)> GetFlexibleCountedListAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        var (payload, envelope) = await ReadPayloadWithEnvelopeAsync(path, cancellationToken);
        var items = ReadList<T>(payload);

        return (items, ReadTotal(envelope) ?? items.Count);
    }

    /// <summary>봉투에서 총건수를 찾는다. 서비스마다 이름이 달라 <c>totalcount</c> 와 <c>page.total</c> 을 본다.</summary>
    private static int? ReadTotal(JsonElement? envelope)
    {
        if (envelope is not { ValueKind: JsonValueKind.Object } root)
        {
            return null;
        }

        foreach (var name in (string[])["totalcount", "totalCount", "total"])
        {
            if (root.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number))
            {
                return number;
            }
        }

        if (root.TryGetProperty("page", out var page)
            && page.ValueKind == JsonValueKind.Object
            && page.TryGetProperty("total", out var pageTotal)
            && pageTotal.ValueKind == JsonValueKind.Number
            && pageTotal.TryGetInt32(out var fromPage))
        {
            return fromPage;
        }

        return null;
    }

    private static IReadOnlyList<T> ReadList<T>(JsonElement? payload)
    {
        if (payload is not { } value)
        {
            return [];
        }

        return value.ValueKind switch
        {
            JsonValueKind.Array => value.Deserialize<List<T>>(JsonOptions) ?? [],

            // 한 건만 올 때 배열로 감싸지 않는 엔드포인트가 있다.
            JsonValueKind.Object => value.Deserialize<T>(JsonOptions) is { } one ? [one] : [],

            _ => [],
        };
    }

    public async Task<IReadOnlyList<T>> GetFlexibleListAsync<T>(
        string path,
        CancellationToken cancellationToken = default)
    {
        return ReadList<T>(await ReadPayloadAsync(path, cancellationToken));
    }

    /// <summary>응답에서 알맹이를 꺼낸다(세 모양을 차례로 벗긴다). 봉투가 실패(<c>success: false</c>)면 그 메시지로 던진다. 아니면 화면이 「자료가 없습니다」로 잘못 안내한다.</summary>
    private async Task<JsonElement?> ReadPayloadAsync(string path, CancellationToken cancellationToken)
        => (await ReadPayloadWithEnvelopeAsync(path, cancellationToken)).Payload;

    /// <summary>알맹이와 그것을 감싸던 <b>봉투</b>를 함께 돌려준다. 총건수가 봉투에 있어 알맹이만으로는 「전부인지」를 알 수 없다. 맨 배열이면 봉투는 <c>null</c>.</summary>
    private async Task<(JsonElement? Payload, JsonElement? Envelope)> ReadPayloadWithEnvelopeAsync(
        string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        await EnsureHttpSuccessAsync(response, path, cancellationToken);

        JsonDocument? document;
        try
        {
            document = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"응답을 해석하지 못했습니다. ({path})", response.StatusCode, innerException: ex);
        }

        if (document is null)
        {
            return (null, null);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return (root.Clone(), null);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return (root.Clone(), null);
            }

            if (root.TryGetProperty("success", out var success)
                && success.ValueKind == JsonValueKind.False)
            {
                var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;

                throw new ApiException(
                    string.IsNullOrWhiteSpace(message) ? $"요청을 처리하지 못했습니다. ({path})" : message,
                    response.StatusCode);
            }

            var envelope = root.Clone();

            if (!root.TryGetProperty("data", out var data))
            {
                // 봉투가 아니면 몸통이 알맹이다. 헬프데스크는 몸통에 총건수가 있어 봉투로도 넘긴다.
                return (envelope, envelope);
            }

            // funeralv2 봉투는 한 겹 더 있다.
            var payload = data.ValueKind == JsonValueKind.Object
                          && data.TryGetProperty("result", out var result)
                ? result.Clone()
                : data.Clone();

            return (payload, envelope);
        }
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

    /// <summary>HTTP 실패를 <see cref="ApiException"/> 으로 바꾼다. 실패 응답도 봉투로 오므로 메시지를 먼저 꺼내고, 못 꺼내면 상태 코드 메시지를 쓴다.</summary>
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
