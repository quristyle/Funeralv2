using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 플레이어 릴리스 — 포털 화면이 GitHub 릴리스를 발행하는 통로.
/// </summary>
/// <remarks>
/// 토큰은 서버에만 둔다(<see cref="PlayerReleaseService"/> 주석 참고).
/// 화면에서 GitHub 을 직접 부르지 않는 이유가 그것이다.
/// </remarks>
public static class PlayerReleaseEndpoints
{
    public static void MapPlayerReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system/player-release")
                       .WithTags("Player Release")
                       .AddApiResponseWrapper();

        // ── 화면 첫 그림 ───────────────────────────────────
        //
        // 설정 여부·권한·최신 커밋·기존 태그를 한 번에 준다.
        // 설정이 없어도 200 이다 — 화면이 안내를 띄워야 하기 때문이다.
        group.MapGet("/status", async (UserContext? user,
            [FromServices] IPlayerReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var status = await service.GetStatusAsync(user.UserId);
            return Results.Ok(ApiResponse<PlayerReleaseStatusDto>.Ok(status));
        })
        .WithName("GetPlayerReleaseStatus")
        .WithOpenApi();

        // ── 릴리스 발행 ────────────────────────────────────
        //
        // 버전 태그를 만든다. 그 순간 워크플로가 깨어나 다섯 갈래를 빌드하고
        // GitHub Release 에 첨부한다. 되돌리기 어려운 동작이라 화면이 한 번 더 묻는다.
        group.MapPost("/", async (UserContext? user,
            [FromBody] PlayerReleaseRequestDto request,
            [FromServices] IPlayerReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var (outcome, result) = await service.CreateAsync(user.UserId, request);

            return outcome switch
            {
                PlayerReleaseOutcome.Ok =>
                    Results.Ok(ApiResponse<PlayerReleaseResultDto>.Ok(result)),

                PlayerReleaseOutcome.Forbidden => Results.Json(
                    ApiResponse<PlayerReleaseResultDto>.Fail(
                        message: result.Message, code: "FORBIDDEN"),
                    statusCode: StatusCodes.Status403Forbidden),

                PlayerReleaseOutcome.Invalid => Results.BadRequest(
                    ApiResponse<PlayerReleaseResultDto>.Fail(
                        message: result.Message, code: "INVALID_VERSION")),

                PlayerReleaseOutcome.NotConfigured => Results.BadRequest(
                    ApiResponse<PlayerReleaseResultDto>.Fail(
                        message: result.Message, code: "NOT_CONFIGURED")),

                _ => Results.BadRequest(
                    ApiResponse<PlayerReleaseResultDto>.Fail(
                        message: result.Message, code: "RELEASE_FAILED"))
            };
        })
        .WithName("CreatePlayerRelease")
        .WithOpenApi();

        // ── 최신 릴리스와 첨부 파일 ─────────────────────────
        //
        // 다운로드 화면이 OS 별 카드에 파일을 짝지어 보여 준다.
        // **발행 권한과 무관하다** — 내려받는 것은 로그인한 사람 누구나 한다.
        group.MapGet("/latest", async (UserContext? user,
            [FromServices] IPlayerReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var latest = await service.GetLatestAsync();
            return Results.Ok(ApiResponse<PlayerReleaseLatestDto>.Ok(latest));
        })
        .WithName("GetPlayerReleaseLatest")
        .WithOpenApi();

        // ── 진행 상황 (화면이 폴링한다) ─────────────────────
        group.MapGet("/runs/{tag}", async (string tag, UserContext? user,
            [FromServices] IPlayerReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var run = await service.GetRunAsync(tag);
            return Results.Ok(ApiResponse<PlayerReleaseRunDto>.Ok(run));
        })
        .WithName("GetPlayerReleaseRun")
        .WithOpenApi();
    }
}
