using System.Net.Http.Json;
using System.Text.Json;
using JSini.Web.Http;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 한주(고객사) OADR 시스템 클라이언트.
///
/// 설비 모니터링·리포트·한주 화면들이 읽는 <b>외부 시스템</b>이다. 게이트웨이의
/// <c>/api/oadr</c> 라우트가 <c>https://nums.hanjucorp.co.kr/oadr</c> 로 중계한다
/// (브라우저에서 직접 부르면 CORS 에 막힌다).
///
/// 외부 시스템이라 봉투 규약이 없다 — 본문을 그대로 받는다. 화면 대부분은
/// <see cref="ExecuteProcedureAsync{T}"/> 하나에 프로시저 이름만 바꿔 태운다.
/// </summary>
public sealed class OadrApi(HttpClient http)
{
    /// <summary>프로시저 파라미터 한 칸. 이름은 <c>@QueryType</c> 처럼 @ 를 붙인다.</summary>
    public sealed record OadrParameter(string Name, object? Value);

    /// <summary>
    /// 저장 프로시저를 실행하고 결과를 돌려준다.
    /// 리포트 화면 대부분이 이 하나의 엔드포인트에 이름만 바꿔 호출한다.
    /// </summary>
    public Task<T?> ExecuteProcedureAsync<T>(
        string procedureName,
        IReadOnlyList<OadrParameter>? parameters = null,
        CancellationToken ct = default)
        => PostAsync<T>("api/procedure/execute", new
        {
            procedureName,
            parameters = (parameters ?? []).Select(p => new { name = p.Name, value = p.Value }),
        }, ct);

    /// <summary>서버 리포트 조회 — QueryType 만 다른 <c>P_QURI_SERVER_REPORT</c> 호출을 감싼다.</summary>
    public Task<T?> GetServerReportAsync<T>(string queryType, CancellationToken ct = default)
        => ExecuteProcedureAsync<T>("P_QURI_SERVER_REPORT", [new OadrParameter("@QueryType", queryType)], ct);

    /// <summary>임의 경로 GET — 화면별 개별 엔드포인트용.</summary>
    public async Task<T?> GetAsync<T>(string path, object? query = null, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, HelpDeskApi.WithQuery(path, query)), ct);
        return await ReadAsync<T>(response, path, ct);
    }

    /// <summary>임의 경로 POST.</summary>
    public async Task<T?> PostAsync<T>(string path, object? body = null, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: HelpDeskApi.JsonOptions);
        }

        using var response = await SendAsync(request, ct);
        return await ReadAsync<T>(response, path, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            request.Dispose();
            throw new ApiException("OADR 서버에 연결하지 못했습니다.", statusCode: null, innerException: ex);
        }

        request.Dispose();

        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new ApiException($"OADR 요청이 실패했습니다. ({(int)status})", status);
        }

        return response;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, string path, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(HelpDeskApi.JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"OADR 응답을 해석하지 못했습니다. ({path})", response.StatusCode, innerException: ex);
        }
    }
}
