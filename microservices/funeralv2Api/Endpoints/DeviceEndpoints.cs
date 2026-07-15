using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;
using Funeralv2.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 장비 관련 API 엔드포인트 정의
/// </summary>
public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/device").WithTags("Devices").AddApiResponseWrapper();

        // 장비 목록 조회 (회사, 건물, 층, 호실 필터 적용)
        group.MapGet("/list", async (
            [FromQuery] string? companyId,
            [FromQuery] string? buildingId,
            [FromQuery] string? floorId,
            [FromQuery] string? roomId,
            [FromServices] IDeviceService service) =>
        {
            if (string.IsNullOrEmpty(companyId) && string.IsNullOrEmpty(buildingId) && string.IsNullOrEmpty(floorId) && string.IsNullOrEmpty(roomId))
            {
                var result = await service.GetAllAsync();
                return Results.Ok(result);
            }

            var filteredResult = await service.GetByFilterAsync(companyId, buildingId, floorId, roomId);
            return Results.Ok(filteredResult);

        }).WithName("GetDevices").WithOpenApi();

        // 장비 상세 조회
        group.MapGet("/{id}", async (string id, [FromServices] IDeviceService service) =>
        {
            var result = await service.GetByIdAsync(id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeviceDto>.Fail("장비 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        }).WithName("GetDeviceById").WithOpenApi();

        // 장비 코드로 상세 조회
        group.MapGet("/code/{code}", async (string code, [FromServices] IDeviceService service) =>
        {
            var result = await service.GetByCodeAsync(code);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeviceDto>.Fail("장비 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        }).WithName("GetDeviceByCode").WithOpenApi();

        // 장비 생성
        group.MapPost("/", async ([FromBody] DeviceCreateDto dto, [FromServices] IDeviceService service) =>
        {
            var newDevice = await service.CreateAsync(dto);
            return Results.Ok(newDevice);
        }).WithName("CreateDevice").WithOpenApi();

        // 장비 수정
        group.MapPut("/{id}", async (string id, [FromBody] DeviceUpdateDto dto, [FromServices] IDeviceService service) =>
        {
            var updatedDevice = await service.UpdateAsync(id, dto);
            if (updatedDevice == null)
            {
                return Results.NotFound(ApiResponse<DeviceDto>.Fail("수정할 장비 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(updatedDevice);
        }).WithName("UpdateDevice").WithOpenApi();

        // 장비 삭제
        group.MapDelete("/{id}", async (string id, [FromServices] IDeviceService service) =>
        {
            var success = await service.DeleteAsync(id);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 장비 정보를 찾을 수 없습니다."));
            }
            return Results.Ok(success);
        }).WithName("DeleteDevice").WithOpenApi();

        // 장비 상태 직접 업데이트 (기기코드 기준)
        group.MapPut("/status/{code}", async (
            string code,
            [FromQuery] string status,
            [FromServices] IDeviceService service) =>
        {
            var success = await service.UpdateStatusAsync(code, status);
            return Results.Ok(ApiResponse<bool>.Ok(success));
        }).WithName("UpdateDeviceStatus").WithOpenApi();
    }
}
