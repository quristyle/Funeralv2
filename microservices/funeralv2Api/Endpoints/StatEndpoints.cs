using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 통계 API — 과금 내역 · 빈소 사용 내역.
/// </summary>
public static class StatEndpoints
{
    public static void MapStatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/stat").AddApiResponseWrapper();

        group.MapGet("/billing/list", async (
            [FromQuery] string? buildingId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromServices] IStatService service) =>
        {
            return await service.GetBillingAsync(buildingId, from, to);
        })
        .WithName("GetBillingStats")
        .WithOpenApi();

        group.MapGet("/room-usage/list", async (
            [FromQuery] string? buildingId,
            [FromQuery] string? roomId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromServices] IStatService service) =>
        {
            return await service.GetRoomUsageAsync(buildingId, roomId, from, to);
        })
        .WithName("GetRoomUsageStats")
        .WithOpenApi();

        group.MapGet("/summary", async (
            [FromQuery] string? buildingId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromServices] IStatService service) =>
        {
            return await service.GetSummaryAsync(buildingId, from, to);
        })
        .WithName("GetStatSummary")
        .WithOpenApi();
    }
}
