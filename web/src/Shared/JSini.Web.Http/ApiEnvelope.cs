using System.Text.Json.Serialization;

namespace JSini.Web.Http;

/// <summary>
/// 게이트웨이를 지나 오는 응답 봉투. 서비스가 달라도 모양이 같다. 서버 원본은 JSini.Shared.DTOs.ApiResponse 다. 읽기 전용 사본을 두어 프론트가 백엔드의 배포 일정과 프레임워크 버전에 묶이지 않게 한다.
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
/// 봉투 안쪽. 목록이든 객체 하나든 result 는 배열이다(객체도 한 칸짜리 배열로 온다). 봉투만 보고는 1건짜리 목록과 객체 하나를 구분할 수 없어, 구분은 엔드포인트를 아는 호출 쪽이 한다. GatewayClient 가 메서드 이름으로 그 선택을 드러낸다.
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
/// data.result 가 배열이 아니라 객체 하나인 봉투. 서버가 Result 와 TotalCount 를 가진 DTO 를 돌려주면 ApiResponse 가 감싸지 않고 통과시킨다(IsPassThroughPagedData).
/// 프로젝트관리가 그렇다. 행뿐 아니라 컬럼 메타까지 함께 돌려주므로 배열 하나로는 담을 수 없다.
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
