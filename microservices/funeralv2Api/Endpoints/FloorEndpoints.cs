using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;
using Funeralv2.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 층 관련 API 엔드포인트 정의
/// </summary>
public static class FloorEndpoints
{
    public static void MapFloorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/floor").AddApiResponseWrapper();

        // 층 목록 조회 (건물 필터 적용)
        group.MapGet("/list", async ([FromQuery] string? buildingId, [FromServices] IFloorService floorService) =>
        {
            return await floorService.GetFloorsAsync(buildingId);
        })
        .WithName("GetFloors")
        .WithOpenApi();

        // 층 상세 조회
        group.MapGet("/{id}", async (string id, [FromServices] IFloorService floorService) =>
        {
            var result = await floorService.GetFloorByIdAsync(id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<FloorDto>.Fail("층 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("GetFloorById")
        .WithOpenApi();

        // 층 생성
        group.MapPost("/", async ([FromBody] FloorCreateDto dto, [FromServices] IFloorService floorService) =>
        {
            return await floorService.CreateFloorAsync(dto);
        })
        .WithName("CreateFloor")
        .WithOpenApi();

        // 층 수정
        group.MapPut("/{id}", async (string id, [FromBody] FloorUpdateDto dto, [FromServices] IFloorService floorService) =>
        {
            var result = await floorService.UpdateFloorAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<FloorDto>.Fail("수정할 층 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("UpdateFloor")
        .WithOpenApi();

        // 층 삭제
        group.MapDelete("/{id}", async (string id, [FromServices] IFloorService floorService) =>
        {
            var success = await floorService.DeleteFloorAsync(id);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 층 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("DeleteFloor")
        .WithOpenApi();
    }
}
