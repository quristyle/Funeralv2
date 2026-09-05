using System.Text.Json.Serialization;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이를 지나 오는 응답 봉투. 서비스가 달라도 모양이 같다.
///
/// <code>
/// { "success": true, "code": "S000", "message": "Success",
///   "data": { "result": [ … ], "page": { "total": 7 } } }
/// </code>
///
/// 서버 쪽 원본은 <c>JSini.Shared.DTOs.ApiResponse&lt;T&gt;</c> 다. 그 프로젝트를
/// 직접 참조하지 않고 읽기 전용 사본을 두는 이유는, 프론트가 백엔드의 배포 일정과
/// 프레임워크 버전에 묶이지 않게 하기 위해서다. 봉투는 몇 년째 모양이 바뀌지
/// 않았고, 바뀌면 여기 한 파일만 고치면 된다.
/// </summary>
/// <typeparam name="T">봉투 안의 <c>result</c> 한 칸의 타입</typeparam>
internal sealed class ApiEnvelope<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>비즈니스 결과 코드. 성공은 <c>S000</c>.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = "S000";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public ResultPayload<T>? Data { get; set; }

    /// <summary>분산 로그 추적 ID. 오류를 서버 로그와 맞춰 볼 때 쓴다.</summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<ErrorDetail>? Errors { get; set; }

    /// <summary>실제 예외 메시지. 서버가 개발 환경에서만 채운다.</summary>
    [JsonPropertyName("realmessage")]
    public string? RealMessage { get; set; }
}

/// <summary>
/// 봉투 안쪽. <b>목록이든 객체 하나든 똑같이 <c>result</c> 배열이다.</b>
///
/// 객체 하나도 <c>{ result: [obj], page: { total: 1 } }</c> 로 온다
/// (<c>ApiResponse.BuildSerializedData</c>). 그래서 봉투만 보고는
/// '1건짜리 목록' 과 '객체 하나' 를 구분할 수 없다 — 구분은 엔드포인트를 아는
/// 사람만 할 수 있다. <see cref="GatewayClient"/> 가 메서드 이름으로 그 선택을
/// 드러내는 이유가 이것이다.
/// </summary>
internal sealed class ResultPayload<T>
{
    [JsonPropertyName("result")]
    public List<T>? Result { get; set; }

    [JsonPropertyName("page")]
    public PageInfo? Page { get; set; }
}

internal sealed class PageInfo
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }
}

/// <summary>
/// <c>data.result</c> 가 <b>배열이 아니라 객체 하나</b>인 봉투.
///
/// 서버가 <c>Result</c> 와 <c>TotalCount</c> 를 가진 DTO 를 돌려주면
/// <c>ApiResponse.BuildSerializedData</c> 가 그것을 감싸지 않고 그대로 통과시킨다
/// (<c>IsPassThroughPagedData</c>). 그래서 <c>data.result</c> 자리에 배열 대신
/// 그 객체가 온다.
///
/// 프로젝트관리가 그렇다 — 행뿐 아니라 <b>컬럼 메타</b>까지 함께 돌려주기 때문에
/// 배열 하나로는 담을 수 없다:
/// <c>data.result = { rows, cols, res, procCode }</c>.
/// </summary>
internal sealed class ApiObjectEnvelope<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "S000";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public ObjectPayload<T>? Data { get; set; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<ErrorDetail>? Errors { get; set; }
}

internal sealed class ObjectPayload<T>
{
    [JsonPropertyName("result")]
    public T? Result { get; set; }
}

/// <summary>유효성 검사 실패 같은 필드별 상세 오류.</summary>
public sealed class ErrorDetail
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
