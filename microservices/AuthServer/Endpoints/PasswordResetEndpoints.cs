using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 비밀번호를 잊은 사람이 쓰는 두 경로. <b>둘 다 로그인 없이</b> 부른다.
/// </summary>
/// <remarks>
/// <para>
/// 게이트웨이의 <c>auth-route</c> 가 <c>/api/auth/**</c> 를 통째로 익명으로
/// 열어 두므로 여기서 따로 열 것은 없다. 대신 게이트웨이 설정에
/// <b>시도 제한(<c>auth-attempts</c>)</b>을 걸어 두었다 — 익명이 아무 아이디로나
/// 두드릴 수 있는 경로이기 때문이다.
/// </para>
///
/// <para>
/// 비밀번호를 <b>알고 있는</b> 사람이 바꾸는 길은 여기가 아니라
/// <c>/user/change-password</c> 다. 이쪽은 지금 비밀번호를 물어보지 않는
/// 대신 메일함을 열 수 있다는 것으로 신원을 대신한다.
/// </para>
/// </remarks>
public static class PasswordResetEndpoints
{
    public static void MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/password");

        // ── 링크 보내 달라 ────────────────────────────────────
        //
        // **언제나 200 이다.** 아이디가 없어도, 이메일이 달라도, 메일 발송이
        // 실패해도 같은 답을 준다. 답이 갈리는 순간 이 경로가 「그 아이디가
        // 있는지」를 확인해 주는 도구가 된다.
        //
        // 무슨 일이 있었는지는 서버 로그에 남는다 — 「메일이 안 온다」는
        // 문의가 왔을 때 볼 곳이 거기다.
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
        .WithName("RequestPasswordReset")
        .WithOpenApi();

        // ── 링크로 다시 정하기 ───────────────────────────────
        //
        // 여기는 이유를 구분해 준다. 링크를 손에 든 사람에게 「안 됩니다」만
        // 주면 다시 요청해야 하는지, 다른 값을 넣어야 하는지 알 수 없다.
        // 어느 답도 계정이 있는지 없는지는 말하지 않는다.
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
        .WithName("ResetPassword")
        .WithOpenApi();
    }

    /// <summary>
    /// 요청한 곳의 아이피. <c>AuthEndpoints</c> 에 같은 것이 있는데, 그쪽은
    /// private 이고 여섯 줄짜리라 옮겨 적었다. 셋째 곳이 생기면 승격한다.
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
