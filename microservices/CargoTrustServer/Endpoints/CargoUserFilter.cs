using CargoTrustServer.Common;
using CargoTrustServer.Users;

namespace CargoTrustServer.Endpoints;

/// <summary>
/// 모든 업무 엔드포인트 앞에 선다. 신원이 없으면 401, 있으면 app_user 줄을 잇고
/// <see cref="CurrentUser"/> 를 채운다.
///
/// 차단 판정도 여기서 한다. 계약은 「차단된 사용자는 조회는 되고 등록·수정·신고·이의제기는 403」이다 —
/// 엔드포인트마다 적으면 새 쓰기 경로를 만들 때 빠뜨리기 쉬워서, 읽기가 아닌 요청을 한 곳에서 막는다.
/// </summary>
public class CargoUserFilter(CargoUserService users, CurrentUser current) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        // UserContext.BindAsync 는 ParameterInfo 를 쓰지 않는다 — 헤더만 읽는다.
        var ctx = await UserContext.BindAsync(http, null!);
        if (ctx is null || string.IsNullOrWhiteSpace(ctx.UserId))
            return ApiError.Unauthorized("로그인이 필요합니다.");

        var (user, isAdmin) = await users.EnsureAsync(ctx, http.RequestAborted);
        current.Set(user, isAdmin);

        if (current.IsBlocked && !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
            return ApiError.Forbidden("이용이 제한된 사용자입니다. 조회만 할 수 있습니다.");

        return await next(context);
    }
}

/// <summary><c>/admin/*</c> 앞에 선다. <see cref="CargoUserFilter"/> 뒤에 걸어야 한다.</summary>
public class AdminOnlyFilter(CurrentUser current) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!current.IsAdmin)
            return ApiError.Forbidden("관리자만 쓸 수 있습니다.");
        return await next(context);
    }
}
