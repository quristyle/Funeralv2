using System.Text.Json.Serialization;

namespace Funeralv2.Shared.DTOs;

/// <summary>
/// 고도화된 공통 API 응답 구조 (MSA 대응)
/// </summary>
/// <typeparam name="T">데이터 타입</typeparam>
public class ApiResponse<T>
{
    /// <summary>성공 여부</summary>
    public bool Success { get; set; }

    /// <summary>비즈니스 결과 코드 (기본값: S000)</summary>
    public string Code { get; set; } = "S000";

    /// <summary>응답 메시지</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>실제 반환 데이터</summary>
    [JsonIgnore]
    public T? Data { get; set; }

    /// <summary>직렬화용 데이터 구조</summary>
    [JsonPropertyName("data")]
    public object? SerializedData => BuildSerializedData(Data);

    /// <summary>응답 생성 시간 (UTC)</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>분산 로그 추적 ID (Correlation ID)</summary>
    public string? TraceId { get; set; }

    /// <summary>요청된 API 경로</summary>
    public string? Path { get; set; }

    /// <summary>상세 에러 목록 (유효성 검사 실패 등)</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<ErrorDetail>? Errors { get; set; }

    /// <summary>실제 예외 메시지 (디버깅용)</summary>
    [JsonPropertyName("realmessage")]
    public string? RealMessage { get; set; }

    // --- 정적 팩토리 메서드 ---

    /// <summary>
    /// 성공 응답 객체를 생성합니다.
    /// </summary>
    public static ApiResponse<T> Ok(T? data, string message = "Success", string code = "S000")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = code,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 실패 응답 객체를 생성합니다.
    /// </summary>
    public static ApiResponse<T> Fail(string message, string code = "E500", IEnumerable<ErrorDetail>? errors = null, string? realMessage = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Errors = errors,
            RealMessage = realMessage
        };
    }

    private static object? BuildSerializedData(T? data)
    {
        if (data is null)
            return null;

        if (IsPassThroughPagedData(data))
        {
            
            return new
            {
                result = data.GetType().GetProperty("Result")?.GetValue(data),
                page = new { total = data.GetType().GetProperty("TotalCount")?.GetValue(data) }
            };

        }

        if (data is System.Collections.IEnumerable enumerable && data is not string)
        {
            var resultList = new List<object?>();
            foreach (var item in enumerable)
            {
                resultList.Add(item);
            }

            return new
            {
                result = resultList,
                page = new { total = resultList.Count }
            };
        }

        return new
        {
            result = new object?[] { data },
            page = new { total = 1 }
        };
    }

    private static bool IsPassThroughPagedData(T? data)
    {
        if (data is null)
            return false;

        var dataType = data.GetType();
        return dataType.GetProperty("Result") is not null
            && dataType.GetProperty("TotalCount") is not null;
    }


}

/// <summary>
/// 필드별 상세 에러 정보
/// </summary>
public class ErrorDetail
{
    /// <summary>에러가 발생한 필드명</summary>
    public string? Field { get; set; }

    /// <summary>해당 필드의 에러 내용</summary>
    public string? Message { get; set; }

    /// <summary>거부된 입력값 (디버깅용)</summary>
    public object? RejectedValue { get; set; }

    public ErrorDetail() { }

    public ErrorDetail(string field, string message, object? rejectedValue = null)
    {
        Field = field;
        Message = message;
        RejectedValue = rejectedValue;
    }
}
