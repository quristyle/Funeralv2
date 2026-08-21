using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 장비 기본 설정 관련 API 엔드포인트 정의
/// </summary>
public static class DeviceConfigEndpoints
{
    public static void MapDeviceConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/device-config")
            .WithTags("DeviceConfigs")
            .AddApiResponseWrapper();

        group.MapGet("/list", async (
            [FromQuery] string? deviceId,
            [FromServices] IDeviceConfigService service) =>
        {
            var result = await service.GetListByDeviceIdAsync(deviceId);
            return Results.Ok(result);
        }).WithName("GetDeviceConfigList").WithOpenApi();

        group.MapGet("/{deviceId}", async (
            string deviceId,
            [FromServices] IDeviceConfigService service) =>
        {
            var result = await service.GetByDeviceIdAsync(deviceId);
            //if (result == null)
            //{
            //    return Results.NotFound(ApiResponse<DeviceConfigDto>.Fail("장비 기본 설정 정보를 찾을 수 없습니다."));
            //}
            return Results.Ok(result);
        }).WithName("GetDeviceConfigByDeviceId").WithOpenApi();

        group.MapPut("/", async (
            [FromBody] DeviceConfigUpsertDto dto,
            [FromServices] IDeviceConfigService service) =>
        {
            var result = await service.UpsertAsync(dto);
            return Results.Ok(result);
        }).WithName("UpsertDeviceConfig").WithOpenApi();

        group.MapPut("/{id}", async (
            string id,
            [FromBody] DeviceConfigUpsertDto dto,
            [FromServices] IDeviceConfigService service) =>
        {
            var success = await service.UpdateAsync(id, dto);
            if (!success)
            {
                return Results.NotFound(ApiResponse<DeviceConfigDto>.Fail("수정할 장비 기본 설정을 찾을 수 없습니다."));
            }

            var result = await service.GetByDeviceIdAsync(dto.DeviceId);
            return Results.Ok(result);
        }).WithName("UpdateDeviceConfig").WithOpenApi();

        group.MapDelete("/{deviceId}", async (
            string deviceId,
            [FromServices] IDeviceConfigService service) =>
        {
            var success = await service.DeleteByDeviceIdAsync(deviceId);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 장비 기본 설정이 없습니다."));
            }
            return Results.Ok(success);
        }).WithName("DeleteDeviceConfig").WithOpenApi();
    }
}
