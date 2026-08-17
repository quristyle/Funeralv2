using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.Hubs;
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

        // 원격 모니터 전원 제어 (기기코드 기준)
        //
        // 관리자가 웹에서 특정 사이니지의 화면을 끄거나 켠다.
        // DB 에 저장하지 않는 즉시 실행 명령이며, 장비가 SignalR 로 접속해 있어야 전달된다.
        // 대상 장비가 오프라인이면 전달되지 않으므로 그 사실을 응답으로 알려준다.
        group.MapPost("/screen-power/{code}", async (
            string code,
            [FromQuery] string state,
            [FromServices] IDeviceService service,
            [FromServices] IDeviceHubSender hubSender) =>
        {
            var normalized = (state ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized != "ON" && normalized != "OFF")
            {
                return Results.BadRequest(
                    ApiResponse<bool>.Fail("state 는 ON 또는 OFF 여야 합니다.", "ERR_INVALID_STATE"));
            }

            // SignalR 그룹 전송은 수신자가 없어도 예외 없이 성공한다.
            // 그대로 두면 오프라인 장비에 명령을 보내고도 화면에는 "전송 완료"가 떠서
            // 왜 안 되는지 알 수 없다. 대상 장비의 존재와 접속 상태를 먼저 확인한다.
            var device = await service.GetByCodeAsync(code);
            if (device == null)
            {
                return Results.NotFound(
                    ApiResponse<bool>.Fail($"장비를 찾을 수 없습니다: {code}", "ERR_DEVICE_NOT_FOUND"));
            }

            // DB 의 status 가 아니라 실제 SignalR 연결 여부를 본다.
            // 서버가 재기동되면 연결은 전부 끊기지만 DB 는 ONLINE 인 채로 남아 있어,
            // status 만 믿으면 "전송 성공"이라고 답하고 명령은 사라진다.
            if (!DeviceHub.IsDeviceConnected(code))
            {
                return Results.Ok(
                    ApiResponse<bool>.Fail(
                        "장비가 실시간 연결되어 있지 않아 명령이 전달되지 않았습니다. 잠시 후 다시 시도해 주세요.",
                        "ERR_DEVICE_OFFLINE"));
            }

            await hubSender.SendScreenPowerAsync(code, normalized == "ON");
            return Results.Ok(ApiResponse<bool>.Ok(true));
        }).WithName("SetDeviceScreenPower").WithOpenApi();
    }
}
