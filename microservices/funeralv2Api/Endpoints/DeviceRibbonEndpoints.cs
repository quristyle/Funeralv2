using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 장비 리본 설정 관련 API 엔드포인트 정의
/// </summary>
public static class DeviceRibbonEndpoints
{
    public static void MapDeviceRibbonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/device-ribbon")
            .WithTags("DeviceRibbons")
            .AddApiResponseWrapper();

        // 장비 ID로 리본 목록 조회
        group.MapGet("/by-device/{deviceId}", async (
            string deviceId,
            [FromServices] IDeviceRibbonService service) =>
        {
            var result = await service.GetByDeviceIdAsync(deviceId);
            return Results.Ok(ApiResponse<List<DeviceRibbonDto>>.Ok(result));
        }).WithName("GetDeviceRibbonsByDeviceId").WithOpenApi();

        // 리본 단건 조회
        group.MapGet("/{id}", async (
            string id,
            [FromServices] IDeviceRibbonService service) =>
        {
            var result = await service.GetByIdAsync(id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeviceRibbonDto>.Fail("리본 설정을 찾을 수 없습니다."));
            }
            return Results.Ok(ApiResponse<DeviceRibbonDto>.Ok(result));
        }).WithName("GetDeviceRibbon").WithOpenApi();

        // 리본 단건 생성
        group.MapPost("/", async (
            [FromBody] DeviceRibbonUpsertDto dto,
            [FromServices] IDeviceRibbonService service) =>
        {
            var result = await service.CreateAsync(dto);
            return Results.Created($"/building/device-ribbon/{result.Id}", ApiResponse<DeviceRibbonDto>.Ok(result));
        }).WithName("CreateDeviceRibbon").WithOpenApi();

        // 리본 단건 수정
        group.MapPut("/{id}", async (
            string id,
            [FromBody] DeviceRibbonUpsertDto dto,
            [FromServices] IDeviceRibbonService service) =>
        {
            var result = await service.UpdateAsync(id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<DeviceRibbonDto>.Fail("수정할 리본 설정을 찾을 수 없습니다."));
            }
            return Results.Ok(ApiResponse<DeviceRibbonDto>.Ok(result));
        }).WithName("UpdateDeviceRibbon").WithOpenApi();

        // 리본 단건 삭제
        group.MapDelete("/{id}", async (
            string id,
            [FromServices] IDeviceRibbonService service) =>
        {
            var success = await service.DeleteAsync(id);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 리본 설정이 없습니다."));
            }
            return Results.Ok(ApiResponse<bool>.Ok(true));
        }).WithName("DeleteDeviceRibbon").WithOpenApi();

        // 장비 리본 목록 일괄 저장 (전체 교체)
        group.MapPut("/bulk-save", async (
            [FromBody] DeviceRibbonBulkSaveDto dto,
            [FromServices] IDeviceRibbonService service) =>
        {
            var result = await service.BulkSaveAsync(dto);
            return Results.Ok(ApiResponse<List<DeviceRibbonDto>>.Ok(result));
        }).WithName("BulkSaveDeviceRibbons").WithOpenApi();
    }
}
