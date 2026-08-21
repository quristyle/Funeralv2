using System.Net;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;

namespace JSini.Shared.Infrastructure.Middleware;

/// <summary>
/// 모든 마이크로서비스에서 발생하는 예외를 캡처하여 표준 ApiResponse 형식으로 응답하는 미들웨어
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GlobalException] Unhandled exception: {Message} at {Path}", ex.Message, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        // 기본적으로 500 에러로 처리하되, 특정 예외 타입에 따라 상태 코드 조정 가능
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = ApiResponse<object>.Fail(
            message: "서버 내부 오류가 발생했습니다. 관리자에게 문의하세요.",
            code: "E500",
            realMessage: exception.ToString()
        );

        // 메타데이터 추가
        response.TraceId = context.TraceIdentifier;
        response.Path = context.Request.Path;
        response.Timestamp = DateTime.UtcNow;

        // 개발 환경에서는 상세 에러 메시지 포함 (선택 사항)
        // response.Message = exception.Message; 

        await context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// 미들웨어 등록을 위한 확장 메서드
/// </summary>
public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
