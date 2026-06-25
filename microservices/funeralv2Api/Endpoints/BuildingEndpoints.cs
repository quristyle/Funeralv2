using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;
using Funeralv2.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 건물 관련 API 엔드포인트 정의
/// </summary>
public static class BuildingEndpoints
{
    public static void MapBuildingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/info").AddApiResponseWrapper();

        // 건물 목록 조회 (회사 필터 적용)
        group.MapGet("/list", async ([FromQuery] string? companyId, [FromServices] IBuildingService buildingService) =>
        {
            return await buildingService.GetBuildingsAsync(companyId);
        })
        .WithName("GetBuildings")
        .WithOpenApi();

        // 건물 상세 조회
        group.MapGet("/{id}", async (string id, [FromServices] IBuildingService buildingService) =>
        {
            var result = await buildingService.GetBuildingByIdAsync(id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<BuildingDto>.Fail("건물을 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("GetBuildingById")
        .WithOpenApi();

        // 건물 생성
        group.MapPost("/", async ([FromBody] BuildingCreateDto dto, [FromServices] IBuildingService buildingService) =>
        {
            return await buildingService.CreateBuildingAsync(dto);
        })
        .WithName("CreateBuilding")
        .WithOpenApi();

        // 건물 수정
        group.MapPut("/{id}", async (string id, [FromBody] BuildingUpdateDto dto, [FromServices] IBuildingService buildingService) =>
        {
            var result = await buildingService.UpdateBuildingAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<BuildingDto>.Fail("수정할 건물을 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("UpdateBuilding")
        .WithOpenApi();

        // 건물 삭제
        group.MapDelete("/{id}", async (string id, [FromServices] IBuildingService buildingService) =>
        {
            var success = await buildingService.DeleteBuildingAsync(id);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 건물을 찾을 수 없습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("DeleteBuilding")
        .WithOpenApi();
    }
}
