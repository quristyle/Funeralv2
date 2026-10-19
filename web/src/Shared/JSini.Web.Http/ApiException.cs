using System.Net;

namespace JSini.Web.Http;

/// <summary>
/// API 가 실패를 알렸을 때 던진다. HTTP 실패(4xx·5xx·네트워크)와 HTTP 200 인데 봉투 code 가 S000 이 아닌 경우(업무 규칙 위반)를 이 하나로 싸안는다.
/// 화면이 할 일은 어느 쪽이든 메시지를 보여 주는 것이고, 가려야 할 때만 StatusCode 와 Code 를 본다.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(
        string message,
        HttpStatusCode? statusCode = null,
        string? code = null,
        string? traceId = null,
        IReadOnlyList<ErrorDetail>? errors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        TraceId = traceId;
        Errors = errors ?? [];
    }

    /// <summary>HTTP 상태. 네트워크가 끊겨 응답 자체가 없으면 <c>null</c>.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>봉투의 업무 결과 코드 (<c>E401</c> 같은 것). 없으면 <c>null</c>.</summary>
    public string? Code { get; }

    /// <summary>서버 로그와 맞춰 볼 추적 ID. 오류 화면에 함께 보여 준다.</summary>
    public string? TraceId { get; }

    /// <summary>필드별 상세 오류. 폼 화면이 입력칸 밑에 붙인다.</summary>
    public IReadOnlyList<ErrorDetail> Errors { get; }

    /// <summary>인증이 풀린 것인가. 토큰 갱신까지 실패한 뒤에만 참이다(AuthTokenHandler 가 한 번은 스스로 갱신해 본다).</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>권한이 없는 것인가. 비밀번호 만료도 이걸로 온다. 한 화면이 API 를 여럿 부르면 안내가 요청 수만큼 뜨므로 셸이 한 번만 보여 준다.</summary>
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
}
