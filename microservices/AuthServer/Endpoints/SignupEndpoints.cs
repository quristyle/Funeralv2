using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 가입 신청(익명 하나)과 승인 처리(관리자 셋).
/// </summary>
/// <remarks>
/// <para>
/// 한 파일에 둔 이유는 <b>둘이 한 흐름</b>이기 때문이다. 신청서에 무엇을
/// 받는지와 승인 화면이 무엇을 보는지가 떨어져 있으면 칸 하나를 더할 때
/// 한쪽만 고치게 된다.
/// </para>
/// <para>
/// 익명 경로에는 게이트웨이에서 <c>public-write</c> 시도 제한을 건다 —
/// 소개 사이트의 문의 접수와 같은 취지다. 익명 쓰기는 열어 두면 곧 스팸의
/// 통로가 된다.
/// </para>
/// </remarks>
public static class SignupEndpoints
{
    public static void MapSignupEndpoints(this IEndpointRouteBuilder app)
    {
        // ── 신청 (익명) ──────────────────────────────────────
        app.MapPost("/signup", async (
            [FromBody] SignupRequestDto request,
            [FromServices] ISignupService signup,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (ok, error) = await signup.RequestAsync(request, ClientIp(http), ct);

            return ok
                ? Results.Ok(ApiResponse<object>.Ok(
                    data: null!,
                    message: "가입 신청을 받았습니다. 관리자 승인 뒤에 로그인하실 수 있습니다."))
                : Results.BadRequest(ApiResponse<object>.Fail(error ?? "신청을 받지 못했습니다.", "INVALID"));
        })
        .WithName("RequestSignup")
        .WithOpenApi();

        // ── 승인 처리 (관리자) ───────────────────────────────
        //
        // 계정 관리와 같은 자리에 둔다(`/system/…`). 화면에서 이 경로를 볼 수
        // 있는지는 메뉴 권한이 정한다 — 다른 `/system` 경로들과 같은 방식이다.
        var admin = app.MapGroup("/system/signup");

        admin.MapGet("/list", async (
            UserContext? user,
            [FromServices] ISignupService signup,
            CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var pending = await signup.GetPendingAsync(ct);
            return Results.Ok(ApiResponse<List<SignupPendingDto>>.Ok(pending));
        })
        .WithName("GetPendingSignups")
        .WithOpenApi();

        admin.MapPost("/{id}/approve", async (
            string id,
            UserContext? user,
            [FromServices] ISignupService signup,
            CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var ok = await signup.ApproveAsync(id, user.UserId, ct);

            // 「없다」와 「대기 중이 아니다」를 같은 답으로 준다. 둘 다 화면을
            // 다시 읽으면 풀리는 상황이고, 대개는 다른 사람이 먼저 처리한 것이다.
            return ok
                ? Results.Ok(ApiResponse<object>.Ok(data: null!, message: "승인했습니다."))
                : Results.BadRequest(ApiResponse<object>.Fail(
                    "승인 대기 중인 신청이 아닙니다. 목록을 다시 읽어 주십시오.", "INVALID"));
        })
        .WithName("ApproveSignup")
        .WithOpenApi();

        admin.MapPost("/{id}/reject", async (
            string id,
            [FromQuery] string? reason,
            UserContext? user,
            [FromServices] ISignupService signup,
            CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var ok = await signup.RejectAsync(id, user.UserId, reason, ct);

            return ok
                ? Results.Ok(ApiResponse<object>.Ok(data: null!, message: "거절했습니다."))
                : Results.BadRequest(ApiResponse<object>.Fail(
                    "승인 대기 중인 신청이 아닙니다. 목록을 다시 읽어 주십시오.", "INVALID"));
        })
        .WithName("RejectSignup")
        .WithOpenApi();
    }

    /// <summary>요청한 곳의 아이피. <c>PasswordResetEndpoints</c> 에 같은 것이 있다.</summary>
    private static string? ClientIp(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        var value = !string.IsNullOrWhiteSpace(forwarded)
            ? forwarded.Split(',')[0].Trim()
            : http.Connection.RemoteIpAddress?.ToString();

        return value is null || value.Length <= 100 ? value : value[..100];
    }
}
