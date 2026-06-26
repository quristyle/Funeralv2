using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using Funeralv2.Shared.DTOs;
using Funeralv2.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 장비 속성 관련 API 엔드포인트 정의
/// </summary>
public static class DeviceAttributeEndpoints
{
    public static void MapDeviceAttributeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/device-attribute")
            .WithTags("DeviceAttributes")
            .AddApiResponseWrapper();

        // 장비 속성 조회 (deviceId로 조회)
        group.MapGet("/{deviceId}", async (
            string deviceId,
            [FromServices] IDeviceAttributeService service) =>
        {
            var result = await service.GetByDeviceIdAsync(deviceId);
            //if (result == null)
            //{
            //    return Results.NotFound(ApiResponse<DeviceAttributeDto>.Fail("장비 속성 정보를 찾을 수 없습니다."));
            //}
            return Results.Ok(ApiResponse<DeviceAttributeDto>.Ok(result));
        }).WithName("GetDeviceAttribute").WithOpenApi();

        // 장비 속성 저장 (Upsert: 없으면 생성, 있으면 수정)
        group.MapPut("/", async (
            [FromBody] DeviceAttributeUpsertDto dto,
            [FromServices] IDeviceAttributeService service) =>
        {
            var result = await service.UpsertAsync(dto);
            return Results.Ok(result);
        }).WithName("UpsertDeviceAttribute").WithOpenApi();

        // 장비 속성 삭제 (deviceId 기준)
        group.MapDelete("/{deviceId}", async (
            string deviceId,
            [FromServices] IDeviceAttributeService service) =>
        {
            var success = await service.DeleteByDeviceIdAsync(deviceId);
            if (!success)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 장비 속성이 없습니다."));
            }
            return Results.Ok(success);
        }).WithName("DeleteDeviceAttribute").WithOpenApi();
    }
}
