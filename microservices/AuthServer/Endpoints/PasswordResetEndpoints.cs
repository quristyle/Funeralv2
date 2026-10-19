using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 비밀번호를 잊은 사람이 쓰는 두 경로. <b>둘 다 로그인 없이</b> 부른다.
/// </summary>
/// 게이트웨이의 auth-route 가 /api/auth/** 를 익명으로 열어 두므로 여기서 따로 열 것은 없다. 대신 게이트웨이에 시도 제한(auth-attempts)을 걸어 둔다 — 익명이 아무 아이디로나 두드릴 수 있는 경로이기 때문이다.
/// 알고 있는 사람의 변경은 /user/change-password 다. 이쪽은 비밀번호를 묻지 않는 대신 메일함을 열 수 있다는 것으로 신원을 대신한다.
public static class PasswordResetEndpoints
{
    public static void MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/password");

        // ── 링크 보내 달라 ────────────────────────────────────
        //
                // 언제나 200 이다 — 답이 갈리면 이 경로가 아이디 존재 확인 도구가 된다. 발송 실패 사유는 서버 로그에 남는다.
        group.MapPost("/forgot", async (
            [FromBody] ForgotPasswordDto request,
            [FromServices] IPasswordResetService reset,
            HttpContext http,
            CancellationToken ct) =>
        {
            await reset.RequestAsync(request.LoginId, request.Email, ClientIp(http), ct);

            return Results.Ok(ApiResponse<object>.Ok(
                data: null!,
                message: "입력하신 정보와 맞는 계정이 있으면 안내 메일을 보냈습니다."));
        })
        .WithName("RequestPasswordReset");

        // ── 링크로 다시 정하기 ───────────────────────────────
        //
                // 이유를 구분해 준다 — 링크를 든 사람이 다시 요청할지 다른 값을 넣을지 알아야 한다. 계정이 있는지는 말하지 않는다.
        group.MapPost("/reset", async (
            [FromBody] ResetPasswordDto request,
            [FromServices] IPasswordResetService reset,
            CancellationToken ct) =>
        {
            var result = await reset.ResetAsync(request.Token, request.NewPassword, ct);

            return result switch
            {
                PasswordResetResult.Success => Results.Ok(ApiResponse<object>.Ok(
                    data: null!,
                    message: "비밀번호를 다시 정했습니다. 새 비밀번호로 로그인해 주십시오.")),

                PasswordResetResult.NewPasswordEmpty => Results.BadRequest(
                    ApiResponse<object>.Fail("새 비밀번호를 넣어 주십시오.", "INVALID")),

                PasswordResetResult.SameAsCurrent => Results.BadRequest(
                    ApiResponse<object>.Fail("지금 쓰는 비밀번호와 다른 값으로 넣어 주십시오.", "INVALID")),

                PasswordResetResult.Expired => Results.BadRequest(
                    ApiResponse<object>.Fail(
                        "링크의 사용 시간이 지났습니다. 비밀번호 찾기를 다시 해 주십시오.", "EXPIRED")),

                PasswordResetResult.AlreadyUsed => Results.BadRequest(
                    ApiResponse<object>.Fail(
                        "이미 사용한 링크입니다. 비밀번호 찾기를 다시 해 주십시오.", "USED")),

                _ => Results.BadRequest(
                    ApiResponse<object>.Fail(
                        "올바르지 않은 링크입니다. 메일의 주소를 그대로 열었는지 확인해 주십시오.", "INVALID")),
            };
        })
        .WithName("ResetPassword");
    }

    /// <summary>
    /// AuthEndpoints 에 같은 것이 있으나 private 이라 옮겨 적었다. 셋째 곳이 생기면 공용으로 올린다.
    /// </summary>
    private static string? ClientIp(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        var value = !string.IsNullOrWhiteSpace(forwarded)
            ? forwarded.Split(',')[0].Trim()
            : http.Connection.RemoteIpAddress?.ToString();

        return value is null || value.Length <= 100 ? value : value[..100];
    }
}
