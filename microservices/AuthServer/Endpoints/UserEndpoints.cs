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

            var (success, error) = await userService.UpdateProfileAsync(user.UserId, request);
            if (!success)
            {
                // 중복 이메일·전화번호처럼 사용자가 고칠 수 있는 이유는 그대로 보여 준다.
                return Results.Json(
                    ApiResponse<object>.Fail(message: error ?? "프로필 정보 업데이트에 실패했습니다.", code: "409"),
                    statusCode: 409);
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

        // ── 비밀번호 확인 (잠금화면) ────────────────────────────
        //
        // **아무것도 바꾸지 않는다.** 맞는지만 보고 참·거짓을 준다.
        //
        // 잠금화면(D7)이 쓴다. 로그인 API 를 다시 부르는 길도 있었지만 그러면
        // 새 토큰이 발급되어 기존 세션과 섞인다 — 잠금을 푸는 일이 조용히
        // 재로그인이 되는 셈이라, 확인만 하는 경로를 따로 뒀다.
        //
        // **틀렸을 때 401 이 아니라 200 + false 를 준다.** 401 을 주면
        // 프런트의 `AuthTokenHandler` 가 세션이 죽은 것으로 읽고 토큰을 버려,
        // 비밀번호를 한 번 잘못 치면 **화면 전체가 로그아웃된다.**
        //
        // 속도 제한은 게이트웨이가 로그인과 같은 정책(`auth-attempts`)으로 건다.
        // 안 걸면 로그인에 걸어 둔 제한을 이 경로로 우회할 수 있다.
        group.MapPost("/verify-password", async (
            UserContext? user, [FromBody] VerifyPasswordDto request,
            [FromServices] IUserService userService) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var matched = await userService.VerifyPasswordAsync(user.UserId, request.Password);
            return Results.Ok(ApiResponse<bool>.Ok(matched));
        })
        .WithName("VerifyPassword")
        .WithOpenApi();

        // ── 화면 환경설정 (계정별) ──────────────────────────────
        //
        // 예전에는 브라우저 로컬스토리지에만 있어서 **사람이 아니라 브라우저에 붙었다.**
        // 다른 PC 에서 로그인하면 기본값으로 돌아갔다. 계정에 붙여 어디서든 따라오게 한다.
        //
        // **자기 것만 다룬다.** 조회할 계정을 요청에서 받지 않고 게이트웨이가 넘긴
        // 신원을 쓴다. 남의 설정을 열거나 바꾸는 길이 없다.
        //
        // 서버는 내용을 해석하지 않는다. 프론트가 만든 JSON 을 그대로 보관하고 돌려준다.

        group.MapGet("/preferences", async (UserContext? user, [FromServices] IAccountPreferenceService service) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var payload = await service.GetAsync(user.UserId);
            return Results.Ok(ApiResponse<AccountPreferenceDto>.Ok(new AccountPreferenceDto
            {
                Payload = payload
            }));
        })
        .WithName("GetAccountPreferences")
        .WithOpenApi();

        group.MapPut("/preferences", async (UserContext? user, [FromBody] AccountPreferenceDto request, [FromServices] IAccountPreferenceService service) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var result = await service.SaveAsync(user.UserId, request.Payload);
            return result switch
            {
                SavePreferenceResult.Success
                    => Results.Ok(ApiResponse<object>.Ok(null)),
                SavePreferenceResult.AccountNotFound
                    => Results.Json(ApiResponse<object>.Fail("계정을 찾을 수 없습니다.", "404"), statusCode: 404),
                SavePreferenceResult.TooLarge
                    => Results.Json(
                        ApiResponse<object>.Fail(
                            $"환경설정이 너무 큽니다. {AccountPreferenceService.MaxPayloadBytes / 1024}KB 이내여야 합니다.",
                            "400"),
                        statusCode: 400),
                _ => Results.Json(ApiResponse<object>.Fail("환경설정 저장에 실패했습니다.", "400"), statusCode: 400),
            };
        })
        .WithName("SaveAccountPreferences")
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
