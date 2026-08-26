using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>
/// 배포(릴리즈) 엔드포인트
/// </summary>
/// <remarks>
/// 예전에는 헬프데스크가 자기 시스템을 배포하는 화면을 들고 있었다.
/// JSini 관리 포털이 여러 MSA 를 관장하므로 이쪽으로 옮겼고,
/// 배포 대상은 코드가 아니라 설정(Release:Targets)에서 읽는다.
///
/// <para>
/// 예전에는 "큐에 넣었다" 가 이 API 가 아는 전부였고 화면이 진행 단계를 스스로
/// 만들어 냈다. 이제 요청 한 건이 <c>scom.release_runs</c> 행 하나가 되고,
/// 배포 장비의 래퍼가 그 run id 로 진행 상황을 되돌려 보고한다.
/// </para>
///
/// <para>
/// 게이트웨이의 <c>/api/auth/**</c> 경로는 Anonymous 라 인증을 걸지 않는다.
/// 그래서 사용자용 엔드포인트는 UserContext 가 없으면 401 을 돌려준다.
/// <b>콜백은 예외다</b> — 배포 장비에는 로그인 정보가 없으므로 run 별 토큰으로 인증한다.
/// </para>
///
/// <para>
/// 배포 실행 권한(<c>/portal/release</c> 의 can_cust1)은 화면이 아니라
/// <see cref="IReleaseService"/> 가 판정한다. 화면의 <c>v-perm</c> 은 버튼을 숨기는
/// 장치일 뿐이라 요청을 직접 보내면 통과한다.
/// </para>
/// </remarks>
public static class ReleaseEndpoints
{
    /// <summary>배포 장비가 보고할 때 토큰을 담는 헤더.</summary>
    private const string TokenHeader = "X-Release-Token";

    // ── ApiResponse.Fail 인자 순서에 관하여 ─────────────────
    //
    // 시그니처는 Fail(message, code) 다. 그런데 이 저장소의 다른 엔드포인트는
    // 거의 모두 Fail("NOT_FOUND", "찾을 수 없습니다") 처럼 부르고 있어서,
    // 실제로는 code 자리에 사람 말이 들어가고 message 자리에 코드가 들어간다.
    //
    // 여기서는 **이름 있는 인자**로 불러 제 자리에 넣는다. 화면이 읽는 것은
    // message 이고, 거기에 코드가 들어가면 사용자에게 'RELEASE_FAILED' 가 보인다.
    // (저장소 전체를 맞추는 일은 이 작업의 범위를 넘는다 — 26번 문서에 적어 두었다.)

    public static void MapReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/release").WithTags("Release");

        // ── 대상 목록 ───────────────────────────────────────
        //
        // 화면이 이 목록으로 버튼을 만든다. 각 대상의 진행 중인 실행과 최근 실행 요약을
        // 함께 담으므로, 화면을 새로 열어도 "지금 무엇이 돌고 있나" 를 바로 안다.
        group.MapGet("/targets", async (UserContext? user,
            [FromServices] IReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = await service.GetTargetsAsync(user.UserId);
            return Results.Ok(ApiResponse<ReleaseTargetListDto>.Ok(result));
        })
        .WithName("GetReleaseTargets")
        .WithOpenApi();

        // ── 실행 이력 ───────────────────────────────────────
        group.MapGet("/runs", async (UserContext? user, [FromQuery] int? take,
            [FromServices] IReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var runs = await service.GetRunsAsync(take ?? 20);
            return Results.Ok(ApiResponse<List<ReleaseRunDto>>.Ok(runs));
        })
        .WithName("GetReleaseRuns")
        .WithOpenApi();

        // ── 실행 한 건 (화면이 폴링한다) ─────────────────────
        //
        // sinceSeq 이후의 로그만 담아 준다. 화면은 받은 마지막 순번을 다음 요청에
        // 그대로 돌려주면 되고, 같은 줄을 두 번 받지 않는다.
        group.MapGet("/runs/{id}", async (string id, UserContext? user,
            [FromQuery] int? sinceSeq, [FromServices] IReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var run = await service.GetRunAsync(id, sinceSeq ?? 0);
            return run is null
                ? Results.NotFound(ApiResponse<ReleaseRunDto>.Fail(
                    message: "실행을 찾을 수 없습니다.", code: "RUN_NOT_FOUND"))
                : Results.Ok(ApiResponse<ReleaseRunDto>.Ok(run));
        })
        .WithName("GetReleaseRun")
        .WithOpenApi();

        // ── 실행 요청 ───────────────────────────────────────
        group.MapPost("/{key}", async (string key, UserContext? user,
            [FromServices] IReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var (outcome, result) = await service.TriggerAsync(key, user.UserId);

            return outcome switch
            {
                ReleaseTriggerOutcome.Ok =>
                    Results.Ok(ApiResponse<ReleaseResultDto>.Ok(result)),

                ReleaseTriggerOutcome.Forbidden => Results.Json(
                    ApiResponse<ReleaseResultDto>.Fail(
                        message: result.Message, code: "FORBIDDEN"),
                    statusCode: StatusCodes.Status403Forbidden),

                // 이미 돌고 있다.
                //
                // 실패 봉투에는 데이터를 담을 수 없어 진행 중인 runId 를 돌려주지 못한다.
                // 화면은 그것에 기대지 않는다 — 오류를 받으면 대상 목록을 다시 읽고
                // activeRunId 로 진행 중인 실행을 찾아 이어 본다.
                ReleaseTriggerOutcome.Conflict => Results.Json(
                    ApiResponse<ReleaseResultDto>.Fail(
                        message: result.Message, code: "RELEASE_RUNNING"),
                    statusCode: StatusCodes.Status409Conflict),

                _ => Results.BadRequest(
                    ApiResponse<ReleaseResultDto>.Fail(
                        message: result.Message, code: "RELEASE_FAILED"))
            };
        })
        .WithName("TriggerRelease")
        .WithOpenApi();

        // ── 배포 장비의 보고 (콜백) ──────────────────────────
        //
        // **로그인 정보가 없는 요청이다.** 배포 장비는 사람이 아니라 스크립트이고
        // 계정도 토큰도 갖고 있지 않다. 대신 요청을 만들 때 발급한 run 별 1회용
        // 토큰을 헤더로 받아 그것만 확인한다 — 계정 인증이 아니라 실행 인증이다.
        //
        // 토큰은 run 이 끝나면 지워지므로, 끝난 실행에 로그를 덧붙일 수 없다.
        //
        // 응답의 stop 이 참이면 래퍼는 더 보내지 않는다(끝난 run 이거나 로그 한도 초과).
        group.MapPost("/runs/{id}/events", async (string id,
            [FromBody] ReportReleaseEventsDto report,
            HttpRequest request,
            [FromServices] IReleaseService service) =>
        {
            var token = request.Headers[TokenHeader].FirstOrDefault();

            var (outcome, result) = await service.ReportEventsAsync(id, token, report);

            // 상태 코드로 "다시 보내라" 와 "그만 보내라" 를 구분한다.
            // 래퍼는 셸 스크립트라 본문을 파싱하지 않고 코드만 본다.
            return outcome switch
            {
                ReleaseReportOutcome.Ok =>
                    Results.Ok(ApiResponse<ReportReleaseEventsResultDto>.Ok(result)),

                ReleaseReportOutcome.NotFound => Results.NotFound(
                    ApiResponse<ReportReleaseEventsResultDto>.Fail(
                        message: result.Message ?? "실행을 찾을 수 없습니다.",
                        code: "RUN_NOT_FOUND")),

                // 일시적인 충돌. 래퍼가 잠시 뒤 다시 보낸다.
                ReleaseReportOutcome.Retry => Results.Json(
                    ApiResponse<ReportReleaseEventsResultDto>.Fail(
                        message: result.Message ?? "충돌했습니다.",
                        code: "REPORT_CONFLICT"),
                    statusCode: StatusCodes.Status409Conflict),

                _ => Results.Json(
                    ApiResponse<ReportReleaseEventsResultDto>.Fail(
                        message: result.Message ?? "보고를 받지 못했습니다.",
                        code: "REPORT_REJECTED"),
                    statusCode: StatusCodes.Status403Forbidden)
            };
        })
        .WithName("ReportReleaseEvents")
        .WithOpenApi();
    }
}
