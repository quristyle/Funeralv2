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
/// 이 API 는 "이 스크립트를 돌려 달라"는 메시지를 큐에 넣기만 한다.
/// 실제 실행은 배포 장비의 큐 소비자가 맡으므로 진행 상황은 알 수 없다.
/// </remarks>
public static class ReleaseEndpoints
{
    public static void MapReleaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/release").WithTags("Release");

        // 배포 대상 목록. 화면이 이 목록으로 버튼을 만든다.
        group.MapGet("/targets", (UserContext? user, [FromServices] IReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();
            return Results.Ok(ApiResponse<List<ReleaseTargetDto>>.Ok(service.GetTargets()));
        })
        .WithName("GetReleaseTargets")
        .WithOpenApi();

        // 배포 실행 요청
        group.MapPost("/{key}", (string key, UserContext? user,
            [FromServices] IReleaseService service) =>
        {
            if (user is null) return Results.Unauthorized();

            var result = service.Trigger(key, user.UserId);
            return result.Queued
                ? Results.Ok(ApiResponse<ReleaseResultDto>.Ok(result))
                : Results.BadRequest(
                    ApiResponse<ReleaseResultDto>.Fail("RELEASE_FAILED", result.Message));
        })
        .WithName("TriggerRelease")
        .WithOpenApi();
    }
}
