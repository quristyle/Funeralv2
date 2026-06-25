using Microsoft.AspNetCore.Http;
using Funeralv2.Shared.DTOs;
using System.Reflection;

namespace Funeralv2.Shared.Infrastructure.Filters;

/// <summary>
/// Minimal API 핸들러의 실행 결과를 가로채어 공통 ApiResponse 형식으로 자동 래핑하는 엔드포인트 필터
/// </summary>
public class ApiResponseFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        // 1. 결과가 null인 경우 성공 상태의 ApiResponse로 반환
        if (result is null)
        {
            return Results.Ok(ApiResponse<object>.Ok(null));
        }

        // 2. 이미 ApiResponse<T> 형식인 경우 이중 래핑 방지를 위해 그대로 반환
        if (IsApiResponse(result.GetType()))
        {
            return Results.Ok(result);
        }

        // 3. IResult 형태의 응답인 경우
        if (result is IResult iResult)
        {
            // ASP.NET Core의 HttpResults 중 Value 프로퍼티가 정의되어 있는 경우 (예: Ok<T>, BadRequest<T> 등)
            if (iResult.GetType().GetProperty("Value") is PropertyInfo valueProp)
            {
                var innerValue = valueProp.GetValue(iResult);
                
                // 내부에 이미 ApiResponse가 감싸져 있다면 그대로 반환
                if (innerValue != null && IsApiResponse(innerValue.GetType()))
                {
                    return result;
                }

                // 일반 데이터가 들어있는 경우 상태 코드를 유지하면서 ApiResponse로 감싸서 반환
                if (innerValue != null)
                {
                    var wrapped = WrapInApiResponse(innerValue);
                    int statusCode = StatusCodes.Status200OK;
                    
                    if (iResult.GetType().GetProperty("StatusCode") is PropertyInfo statusProp)
                    {
                        statusCode = (int)(statusProp.GetValue(iResult) ?? StatusCodes.Status200OK);
                    }
                    
                    return Results.Json(wrapped, statusCode: statusCode);
                }
            }

            return result;
        }

        // 4. 일반 비즈니스 데이터 객체인 경우 ApiResponse로 래핑 후 Results.Ok 반환
        var wrappedResult = WrapInApiResponse(result);
        return Results.Ok(wrappedResult);
    }

    /// <summary>
    /// 해당 타입이 ApiResponse<T> 계열인지 판별합니다.
    /// </summary>
    private static bool IsApiResponse(Type type)
    {
        if (type == null) return false;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>))
        {
            return true;
        }

        return type.Name.StartsWith("ApiResponse");
    }

    /// <summary>
    /// 데이터를 ApiResponse 객체로 래핑합니다.
    /// </summary>
    private static object WrapInApiResponse(object data)
    {
        return ApiResponse<object>.Ok(data);
    }
}
