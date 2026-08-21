using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JSini.Shared.Infrastructure.Filters;

/// <summary>
/// ApiResponseFilter 적용을 위한 RouteHandlerBuilder 및 RouteGroupBuilder 확장 메서드
/// </summary>
public static class ApiResponseFilterExtensions
{
    /// <summary>
    /// API 핸들러의 반환 값을 ApiResponse 형식으로 자동 래핑하는 필터를 적용합니다.
    /// </summary>
    public static RouteHandlerBuilder AddApiResponseWrapper(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<ApiResponseFilter>();
    }

    /// <summary>
    /// API 그룹 전체의 반환 값을 ApiResponse 형식으로 자동 래핑하는 필터를 적용합니다.
    /// </summary>
    public static RouteGroupBuilder AddApiResponseWrapper(this RouteGroupBuilder builder)
    {
        return builder.AddEndpointFilter<ApiResponseFilter>();
    }
}
