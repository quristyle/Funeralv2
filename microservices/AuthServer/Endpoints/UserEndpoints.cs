using Microsoft.AspNetCore.Mvc;
using AuthServer.Entities;
using AuthServer.Services;
using AuthServer.DTOs;
using JSini.Shared.DTOs;

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

        // 계정 활동 정보. 계정 정보 화면이 쓴다.
        //
        // **자기 것만 볼 수 있다.** 조회할 계정을 요청에서 받지 않고
        // 게이트웨이가 넘긴 신원을 그대로 쓴다 — 남의 접속 기록은 열 수 없다.
        group.MapGet("/activity", async (UserContext? user, [FromQuery] int? limit,
            [FromServices] ILoginLogService loginLog) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var activity = await loginLog.GetActivityAsync(user.UserId, limit ?? 10);
            return Results.Ok(ApiResponse<AccountActivityDto>.Ok(activity));
        })
        .WithName("GetAccountActivity")
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

            // 90일 만료 때문에 사용자가 어쩔 수 없이 이 화면에 오는 경우가 있다.
            // 그때 뭉뚱그린 메시지를 주면 무엇을 고쳐야 할지 알 수 없으므로 이유를 구분해 돌려준다.
            var result = await userService.ChangePasswordAsync(user.UserId, request);
            if (result != ChangePasswordResult.Success)
            {
                var (message, code) = result switch
                {
                    ChangePasswordResult.AccountNotFound
                        => ("계정을 찾을 수 없습니다.", "404"),
                    ChangePasswordResult.OldPasswordMismatch
                        => ("이전 비밀번호가 일치하지 않습니다.", "400"),
                    ChangePasswordResult.NewPasswordEmpty
                        => ("새 비밀번호를 입력해주세요.", "400"),
                    ChangePasswordResult.SameAsCurrent
                        => ("새 비밀번호가 지금 쓰는 비밀번호와 같습니다. 다른 값으로 바꿔주세요.", "400"),
                    _ => ("비밀번호 변경에 실패했습니다.", "400"),
                };

                return Results.Json(
                    ApiResponse<object>.Fail(message, code),
                    statusCode: code == "404" ? 404 : 400);
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
