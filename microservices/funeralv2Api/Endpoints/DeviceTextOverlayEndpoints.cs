using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;
using Funeralv2.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 장비 텍스트 오버레이 관련 API 엔드포인트 정의
/// </summary>
public static class DeviceTextOverlayEndpoints
{
    public static void MapDeviceTextOverlayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/device-text-overlay")
            .WithTags("DeviceTextOverlays")
            .AddApiResponseWrapper();

        // 장비 ID로 텍스트 오버레이 목록 조회
        group.MapGet("/by-device/{deviceId}", async (
            string deviceId,
            [FromServices] IDeviceTextOverlayService service) =>
        {
            var result = await service.GetByDeviceIdAsync(deviceId);
            return Results.Ok(ApiResponse<List<DeviceTextOverlayDto>>.Ok(result));
        }).WithName("GetDeviceTextOverlaysByDeviceId").WithOpenApi();

        // 텍스트 오버레이 단건 조회
        group.MapGet("/{id}", async (
            string id,
            [FromServices] IDeviceTextOverlayService service) =>
        {
            var result = await service.GetByIdAsync(id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeviceTextOverlayDto>.Fail("텍스트 오버레이 설정을 찾을 수 없습니다."));
            }
            return Results.Ok(ApiResponse<DeviceTextOverlayDto>.Ok(result));
        }).WithName("GetDeviceTextOverlay").WithOpenApi();

        // 텍스트 오버레이 단건 생성
        group.MapPost("/", async (
            [FromBody] DeviceTextOverlayUpsertDto dto,
            [FromServices] IDeviceTextOverlayService service) =>
        {
            var result = await service.CreateAsync(dto);
            return Results.Created($"/building/device-text-overlay/{result.Id}", ApiResponse<DeviceTextOverlayDto>.Ok(result));
        }).WithName("CreateDeviceTextOverlay").WithOpenApi();

        // 텍스트 오버레이 단건 수정
        group.MapPut("/{id}", async (
            string id,
            [FromBody] DeviceTextOverlayUpsertDto dto,
            [FromServices] IDeviceTextOverlayService service) =>
        {
            var result = await service.UpdateAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeviceTextOverlayDto>.Fail("수정할 텍스트 오버레이 설정을 찾을 수 없습니다."));
            }
            return Results.Ok(ApiResponse<DeviceTextOverlayDto>.Ok(result));
        }).WithName("UpdateDeviceTextOverlay").WithOpenApi();

        // 텍스트 오버레이 단건 삭제
        group.MapDelete("/{id}", async (
            string id,
            [FromServices] IDeviceTextOverlayService service) =>
        {
            var success = await service.DeleteAsync(id);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 텍스트 오버레이 설정이 없습니다."));
            }
            return Results.Ok(ApiResponse<bool>.Ok(true));
        }).WithName("DeleteDeviceTextOverlay").WithOpenApi();

        // 장비 텍스트 오버레이 목록 일괄 저장 (전체 교체)
        group.MapPut("/bulk-save", async (
            [FromBody] DeviceTextOverlayBulkSaveDto dto,
            [FromServices] IDeviceTextOverlayService service) =>
        {
            var result = await service.BulkSaveAsync(dto);
            return Results.Ok(ApiResponse<List<DeviceTextOverlayDto>>.Ok(result));
        }).WithName("BulkSaveDeviceTextOverlays").WithOpenApi();
    }
}
