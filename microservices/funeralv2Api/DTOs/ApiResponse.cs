namespace funeralv2Api.DTOs;

/// <summary>
/// 모든 API 응답을 위한 공통 구조 클래스
/// </summary>
/// <typeparam name="T">반환될 데이터의 타입</typeparam>
public class ApiResponse<T>
{
    /// <summary>성공 여부 코드 (0: 성공, 그 외: 에러)</summary>
    public int Code { get; set; }

    /// <summary>실제 반환되는 데이터 객체</summary>
    public T? Data { get; set; }

    /// <summary>응답 메시지 (에러 시 상세 내용)</summary>
    public string Message { get; set; } = "success";

    /// <summary>성공 응답 생성 팩토리 메서드</summary>
    /// <param name="data">반환할 데이터</param>
    /// <param name="message">성공 메시지</param>
    /// <returns>공통 응답 객체</returns>
    public static ApiResponse<T> Success(T data, string message = "success")
    {
        return new ApiResponse<T> { Code = 0, Data = data, Message = message };
    }

    /// <summary>에러 응답 생성 팩토리 메서드</summary>
    /// <param name="code">에러 코드 (0이 아닌 값)</param>
    /// <param name="message">에러 메시지</param>
    /// <returns>공통 응답 객체 (데이터 null)</returns>
    public static ApiResponse<object> Error(int code, string message)
    {
        return new ApiResponse<object> { Code = code, Data = null, Message = message };
    }
}
