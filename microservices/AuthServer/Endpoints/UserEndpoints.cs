using Microsoft.AspNetCore.Mvc;
using AuthServer.Entities;
using AuthServer.Services;
using AuthServer.DTOs;
using Funeralv2.Shared.DTOs;

namespace AuthServer.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/user");

        group.MapGet("/info", async (UserContext? user, [FromServices] IUserService userService) =>
        {
            if (user is null) 
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보(Gateway Header)가 없습니다.", "401"), statusCode: 401);
            }

            var userInfo = await userService.GetUserInfoAsync(user.UserId);
            if (userInfo is null)
            {
                return Results.Json(ApiResponse<object>.Fail("사용자를 찾을 수 없습니다.", "404"), statusCode: 404);
            }

            return Results.Ok(ApiResponse<UserInfoDto>.Ok(userInfo));
        })
        .WithName("GetUserInfo")
        .WithOpenApi();
    }
}
