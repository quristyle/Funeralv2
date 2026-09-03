using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 호실 관련 API 엔드포인트 정의
/// </summary>
public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/room").AddApiResponseWrapper();

        // 호실 목록 조회 (회사, 건물, 층 필터 적용)
        group.MapGet("/list", async (
            [FromQuery] string? companyId, 
            [FromQuery] string? buildingId, 
            [FromQuery] string? floorId, 
            [FromServices] IRoomService roomService) =>
        {
            return await roomService.GetRoomsAsync(companyId, buildingId, floorId);
        })
        .WithName("GetRooms")
        .WithOpenApi();

        // 배정(이동) 가능한 호실 목록 — ACTIVE + 미점유. 빈소현황의 호실 변경이 쓴다.
        group.MapGet("/available", async (
            [FromQuery] string? companyId,
            [FromQuery] string? buildingId,
            [FromQuery] string? excludeRoomId,
            [FromServices] IRoomService roomService) =>
        {
            return await roomService.GetAvailableRoomsAsync(companyId, buildingId, excludeRoomId);
        })
        .WithName("GetAvailableRooms")
        .WithOpenApi();

        // 호실 상세 조회
        group.MapGet("/{id}", async (string id, [FromServices] IRoomService roomService) =>
        {
            var result = await roomService.GetRoomByIdAsync(id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<RoomDto>.Fail("호실 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("GetRoomById")
        .WithOpenApi();

        // 호실 생성
        group.MapPost("/", async ([FromBody] RoomCreateDto dto, [FromServices] IRoomService roomService) =>
        {
            return await roomService.CreateRoomAsync(dto);
        })
        .WithName("CreateRoom")
        .WithOpenApi();

        // 호실 수정
        group.MapPut("/{id}", async (string id, [FromBody] RoomUpdateDto dto, [FromServices] IRoomService roomService) =>
        {
            var result = await roomService.UpdateRoomAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<RoomDto>.Fail("수정할 호실 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("UpdateRoom")
        .WithOpenApi();

        // 호실 삭제
        group.MapDelete("/{id}", async (string id, [FromServices] IRoomService roomService) =>
        {
            var success = await roomService.DeleteRoomAsync(id);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 호실 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("DeleteRoom")
        .WithOpenApi();
    }
}
