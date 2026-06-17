namespace AuthServer.DTOs;

/// <summary>
/// 모든 API의 공통 응답 규격
/// </summary>
/// <typeparam name="T">데이터의 타입</typeparam>
public class ApiResponse<T>
{
    /// <summary>응답 코드 (0: 성공, 그 외: 에러)</summary>
    public int Code { get; set; }

    /// <summary>응답 메시지</summary>
    public string Message { get; set; } = string.Empty;

    public string Error { get; set; } 

    /// <summary>실제 데이터</summary>
    public T? Data { get; set; }

    /// <summary>
    /// 성공 응답 생성
    /// </summary>
    public static ApiResponse<T> Success(T data, string message = "성공")
    {
        return new ApiResponse<T>
        {
            Code = 0,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 에러 응답 생성
    /// </summary>
    public static ApiResponse<object> Errors(int code, string message)
    {
        return new ApiResponse<object>
        {
            Code = code,
            Message = message,
            Data = null
        };
    }
}
