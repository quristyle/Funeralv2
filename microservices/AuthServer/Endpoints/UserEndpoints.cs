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

        group.MapPost("/profile", async (UserContext? user, [FromBody] UpdateProfileDto request, [FromServices] IUserService userService) =>
        {
            if (user is null) 
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var success = await userService.UpdateProfileAsync(user.UserId, request);
            if (!success)
            {
                return Results.Json(ApiResponse<object>.Fail("프로필 정보 업데이트에 실패했습니다.", "400"), statusCode: 400);
            }

            return Results.Ok(ApiResponse<object>.Ok(null));
        })
        .WithName("UpdateProfile")
        .WithOpenApi();

        group.MapPost("/change-password", async (UserContext? user, [FromBody] ChangePasswordDto request, [FromServices] IUserService userService) =>
        {
            if (user is null) 
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var success = await userService.ChangePasswordAsync(user.UserId, request);
            if (!success)
            {
                return Results.Json(ApiResponse<object>.Fail("이전 비밀번호가 일치하지 않거나 변경에 실패했습니다.", "400"), statusCode: 400);
            }

            return Results.Ok(ApiResponse<object>.Ok(null));
        })
        .WithName("ChangePassword")
        .WithOpenApi();

        group.MapPost("/settings", async (UserContext? user, [FromBody] UpdateSettingDto request, [FromServices] IUserService userService) =>
        {
            if (user is null) 
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var success = await userService.UpdateSettingAsync(user.UserId, request);
            if (!success)
            {
                return Results.Json(ApiResponse<object>.Fail("설정 업데이트에 실패했습니다.", "400"), statusCode: 400);
            }

            return Results.Ok(ApiResponse<object>.Ok(null));
        })
        .WithName("UpdateSetting")
        .WithOpenApi();
    }
}
