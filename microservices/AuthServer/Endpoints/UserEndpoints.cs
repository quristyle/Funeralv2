using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthServer.Data;
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
        .WithName("GetUserInfo");

        // 여러 사람의 이름과 얼굴 — 업무 DB 에는 로그인 아이디만 있어서 업무 서비스가 직접 풀지 못하고 여기서 푼다.
        group.MapGet("/faces", async (
            UserContext? user, [FromQuery] string? ids,
            [FromServices] AppDbContext db, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var wanted = (ids ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxFaces)
                .ToList();

            if (wanted.Count == 0)
            {
                return Results.Ok(ApiResponse<List<UserFaceDto>>.Ok([]));
            }

            var accounts = await db.Accounts
                .Where(a => !a.IsDeleted && wanted.Contains(a.UserId))
                .Select(a => new { a.Id, a.UserId, a.UserName, a.RealName })
                .ToListAsync(ct);

            var accountIds = accounts.Select(a => a.Id).ToList();

            // 대표 사진이 여럿일 수 있어 알림 아이콘과 같은 규칙으로 대표를 먼저 세운다.
            var avatars = await db.AccountProfileDetails
                .Where(d => accountIds.Contains(d.AccountId)
                    && d.DetailType == "Avatar"
                    && !d.IsDeleted
                    && d.Content != "")
                .Select(d => new { d.AccountId, d.Content, d.IsPrimary })
                .ToListAsync(ct);

            var avatarOf = avatars
                .GroupBy(d => d.AccountId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(d => d.IsPrimary).First().Content);

            // 못 찾은 아이디는 빠진 채로 돌려준다 — 화면이 첫 글자를 그리면 되고, 없는 사람을 지어내지 않는다.
            var faces = accounts
                .Select(a => new UserFaceDto
                {
                    UserId = a.UserId,
                    Name = FirstFilled(a.UserName, a.RealName, a.UserId),
                    Avatar = avatarOf.TryGetValue(a.Id, out var url) ? url : null,
                })
                .ToList();

            return Results.Ok(ApiResponse<List<UserFaceDto>>.Ok(faces));
        })
        .WithName("GetUserFaces");

        // 사람 카드 한 장 — 메일·전화가 실려서 누른 한 명만 준다. 익명 통과 경로라 신원을 여기서 본다.
        group.MapGet("/card", async (
            UserContext? user, [FromQuery] string? id,
            [FromServices] AppDbContext db, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var loginId = id?.Trim();
            if (string.IsNullOrEmpty(loginId))
            {
                return Results.Json(ApiResponse<object>.Fail("사람을 지정하지 않았습니다.", "400"), statusCode: 400);
            }

            var account = await db.Accounts
                .Where(a => !a.IsDeleted && a.UserId == loginId)
                .Select(a => new
                {
                    a.Id, a.UserId, a.UserName, a.RealName,
                    Department = a.Department != null ? a.Department.Name : null,
                    // 계정에 회사가 없으면 부서의 회사를 쓴다.
                    Company = a.Company != null ? a.Company.Name
                        : a.Department != null && a.Department.Company != null ? a.Department.Company.Name
                        : null,
                })
                .FirstOrDefaultAsync(ct);

            if (account is null)
            {
                return Results.Json(ApiResponse<object>.Fail("사람을 찾을 수 없습니다.", "404"), statusCode: 404);
            }

            var details = await db.AccountProfileDetails
                .Where(d => d.AccountId == account.Id
                    && !d.IsDeleted
                    && d.Content != ""
                    && (d.DetailType == "Avatar" || d.DetailType == "Email" || d.DetailType == "Phone"))
                .Select(d => new { d.DetailType, d.Content, d.IsPrimary })
                .ToListAsync(ct);

            string? Primary(string type) => details
                .Where(d => d.DetailType == type)
                .OrderByDescending(d => d.IsPrimary)
                .Select(d => d.Content)
                .FirstOrDefault();

            return Results.Ok(ApiResponse<UserCardDto>.Ok(new UserCardDto
            {
                UserId = account.UserId,
                Name = FirstFilled(account.UserName, account.RealName, account.UserId),
                Avatar = Primary("Avatar"),
                Company = account.Company,
                Department = account.Department,
                Email = Primary("Email"),
                Phone = Primary("Phone"),
            }));
        })
        .WithName("GetUserCard");

        // 계정 활동 — 자기 것만 본다. 조회 대상을 요청에서 받지 않고 게이트웨이가 넘긴 신원을 쓴다.
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
        .WithName("GetAccountActivity");

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
        .WithName("UpdateProfile");

        // 이메일 변경을 위한 인증번호 발송
        group.MapPost("/email-verification/send", async (
            UserContext? user,
            [FromBody] SendEmailVerificationDto request,
            [FromServices] AppDbContext db,
            [FromServices] IEmailVerificationService emailVerification,
            CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.UserId || a.Id == user.UserId, ct);
            if (account == null)
            {
                return Results.Json(ApiResponse<object>.Fail("계정을 찾을 수 없습니다.", "404"), statusCode: 404);
            }

            var (success, message, devCode) = await emailVerification.SendCodeAsync(account.Id, request.Email, ct);
            if (!success)
            {
                return Results.Json(ApiResponse<object>.Fail(message, "400"), statusCode: 400);
            }

            return Results.Ok(ApiResponse<EmailVerificationResultDto>.Ok(new EmailVerificationResultDto
            {
                Success = true,
                Message = message,
                DevCode = devCode
            }));
        })
        .WithName("SendEmailVerificationCode");

        // 이메일 변경을 위한 인증번호 검증
        group.MapPost("/email-verification/verify", async (
            UserContext? user,
            [FromBody] VerifyEmailCodeDto request,
            [FromServices] AppDbContext db,
            [FromServices] IEmailVerificationService emailVerification,
            CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.UserId || a.Id == user.UserId, ct);
            if (account == null)
            {
                return Results.Json(ApiResponse<object>.Fail("계정을 찾을 수 없습니다.", "404"), statusCode: 404);
            }

            var (success, message, token) = await emailVerification.VerifyCodeAsync(account.Id, request.Email, request.Code, ct);
            if (!success)
            {
                return Results.Json(ApiResponse<object>.Fail(message, "400"), statusCode: 400);
            }

            return Results.Ok(ApiResponse<EmailVerificationResultDto>.Ok(new EmailVerificationResultDto
            {
                Success = true,
                Message = message,
                VerificationToken = token
            }));
        })
        .WithName("VerifyEmailCode");

        group.MapPost("/change-password", async (UserContext? user, [FromBody] ChangePasswordDto request, [FromServices] IUserService userService) =>
        {
            if (user is null) 
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            // 90일 만료로 온 사용자에게는 이유를 구분해 돌려준다 — 뭉뚱그리면 무엇을 고칠지 알 수 없다.
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
        .WithName("ChangePassword");

        // 비밀번호 확인(잠금화면) — 아무것도 바꾸지 않고 맞는지만 준다. 로그인 API 를 다시 부르면 새 토큰이 발급되어 기존 세션과 섞이므로 따로 둔다.
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
        .WithName("VerifyPassword");

        // 화면 환경설정(계정별) — 브라우저가 아니라 계정에 붙여 어느 기기에서든 따라오게 한다. 조회 대상은 게이트웨이가 넘긴 신원만 쓴다.

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
        .WithName("GetAccountPreferences");

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
        .WithName("SaveAccountPreferences");

        // 한 칸만 고친다 — PUT 은 통째로 덮어쓰므로, 기기를 넘나들며 고치는 자리가 둘 이상이면 따로 고친다.
        group.MapPatch("/preferences", async (UserContext? user, [FromBody] AccountPreferenceDto request, [FromServices] IAccountPreferenceService service) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var result = await service.MergeAsync(user.UserId, request.Payload);
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
        .WithName("MergeAccountPreferences");

        // 로그인 뒤 처음 열리는 화면 — 환경설정 덩어리와 달리 UserInfoDto.HomePath 칸 하나를 쓴다. 부트스트랩이 내려보내는 값이다.

        group.MapPut("/home-path", async (
            UserContext? user,
            [FromBody] UpdateHomePathDto request,
            [FromServices] IUserService userService) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var (success, error) = await userService.UpdateHomePathAsync(user.UserId, request.HomePath);

            return success
                ? Results.Ok(ApiResponse<object>.Ok(null))
                : Results.Json(
                    ApiResponse<object>.Fail(error ?? "홈 화면을 저장하지 못했습니다.", "400"),
                    statusCode: 400);
        })
        .WithName("UpdateHomePath");

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
        .WithName("UpdateSetting");
    }

    /// <summary>한 번에 풀어 주는 얼굴 수의 상한. 건수가 아니라 주소 길이와 질의 크기가 이유다 — 아이디를 쉼표로 이어 보내므로 막지 않으면 요청 한 줄로 전 직원을 긁을 수 있다.</summary>
    private const int MaxFaces = 100;

    /// <summary>비어 있지 않은 첫 값. 셋 다 비면 빈 글자다.</summary>
    private static string FirstFilled(params string?[] candidates)
        => candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;
}
