using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 건물별 음원 배정 API (옛 <c>page/rsrc/music_build.jsp</c>).
/// </summary>
public static class BuildingMusicEndpoints
{
    public static void MapBuildingMusicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/music").AddApiResponseWrapper();

        // 음원 하나를 고르면 건물 목록에 배정 여부가 붙어 온다.
        group.MapGet("/{mediaSourceId}/buildings", async (
            string mediaSourceId,
            [FromServices] IBuildingMusicService service) =>
        {
            return await service.GetBuildingsForMusicAsync(mediaSourceId);
        })
        .WithName("GetBuildingsForMusic")
        .WithOpenApi();

        group.MapPut("/{mediaSourceId}/buildings", async (
            string mediaSourceId,
            [FromBody] BuildingMusicSaveDto dto,
            UserContext? user,
            [FromServices] IBuildingMusicService service) =>
        {
            return await service.SaveAsync(user?.UserId ?? string.Empty, mediaSourceId, dto.BuildingIds);
        })
        .WithName("SaveBuildingsForMusic")
        .WithOpenApi();

        // 장비가 재생 목록을 받아 갈 때 쓴다.
        group.MapGet("/building/{buildingId}", async (
            string buildingId,
            [FromServices] IBuildingMusicService service) =>
        {
            return await service.GetMusicIdsForBuildingAsync(buildingId);
        })
        .WithName("GetMusicIdsForBuilding")
        .WithOpenApi();
    }
}
