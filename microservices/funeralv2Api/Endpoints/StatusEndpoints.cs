using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 빈소 현황 API. 현황 화면 다섯이 모두 이 하나를 쓴다.
/// </summary>
public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/status").AddApiResponseWrapper();

        // 목록과 요약을 함께. 화면이 두 번 부르지 않도록 한 번에 준다.
        group.MapGet("/funeral-status/board", async (
            [FromQuery] string? buildingId,
            [FromQuery] string? floorId,
            // bool 은 값이 없으면 바인딩이 실패해 400 이 난다. nullable 로 받고 기본값을 준다.
            [FromQuery] bool? onlyInUse,
            [FromServices] IStatusService service) =>
        {
            return await service.GetBoardAsync(buildingId, floorId, onlyInUse ?? false);
        })
        .WithName("GetFuneralStatusBoard")
        .WithOpenApi();

        // 목록만. 옛 화면들이 목록만 쓰던 자리를 위해 남겨 둔다.
        group.MapGet("/funeral-status/list", async (
            [FromQuery] string? buildingId,
            [FromQuery] string? floorId,
            [FromQuery] bool? onlyInUse,
            [FromServices] IStatusService service) =>
        {
            var board = await service.GetBoardAsync(buildingId, floorId, onlyInUse ?? false);
            return board.Rooms;
        })
        .WithName("GetFuneralStatuses")
        .WithOpenApi();

        // 빈소현황 대시보드 — 호실·고인·장비를 서버에서 붙여 한 번에 준다.
        group.MapGet("/room-board", async (
            [AsParameters] RoomBoardQueryDto query,
            [FromServices] IStatusService service) =>
        {
            return await service.GetRoomBoardAsync(query);
        })
        .WithName("GetRoomBoard")
        .WithOpenApi();

        group.MapGet("/funeral-status/{roomId}", async (
            string roomId,
            [FromServices] IStatusService service) =>
        {
            var result = await service.GetRoomStatusAsync(roomId);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<FuneralStatusDto>.Fail("호실 현황을 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("GetFuneralStatusDetail")
        .WithOpenApi();
    }
}
