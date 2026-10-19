using Microsoft.AspNetCore.SignalR;
using funeralv2Api.Hubs;
using funeralv2Api.Services;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 플레이어 원격 업그레이드 지시와 버전 조회. 서버는 "지금 확인해라" 지시만 전하고, 릴리스 조회·설치는 플레이어가 한다.
/// 권한 판정은 헤더로 직접 하며 역할 목록은 UserContext.ControlRoles 와 같은 값이다.
/// </summary>
public static class PlayerUpdateEndpoints
{
    /// <summary>장비 제어가 허용되는 역할 (47번 문서 D-RS4 와 같은 목록).</summary>
    private static readonly string[] ControlRoles =
    [
        "ADMINISTRATOR",
        "SYSTEM_ADMINISTRATOR",
        "PARTNER_ADMINISTRATOR",
    ];

    private static bool CanControl(HttpContext http)
    {
        // 게이트웨이가 검증해 붙인 역할 헤더. X-User-Roles(전체)가 우선,
        // 없으면 X-User-Role(첫 역할)로 판정한다.
        var roles = http.Request.Headers["X-User-Roles"].ToString();
        if (string.IsNullOrEmpty(roles))
        {
            roles = http.Request.Headers["X-User-Role"].ToString();
        }
        return roles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(r => ControlRoles.Contains(r));
    }

    public static void MapPlayerUpdateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/building/device").WithTags("PlayerUpdate").AddApiResponseWrapper();

        // 원격 업그레이드 지시. 즉시 실행 명령이라 저장하지 않으며, 장비 존재와 실시간 연결을 먼저 확인한다.
        group.MapPost("/update-now/{code}", async (
            string code,
            HttpContext http,
            IDeviceService service,
            IHubContext<DeviceHub> hub) =>
        {
            if (!CanControl(http))
            {
                return Results.Json(
                    ApiResponse<bool>.Fail("장비 제어 권한이 없습니다.", "ERR_FORBIDDEN"),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var device = await service.GetByCodeAsync(code);
            if (device == null)
            {
                return Results.NotFound(
                    ApiResponse<bool>.Fail($"장비를 찾을 수 없습니다: {code}", "ERR_DEVICE_NOT_FOUND"));
            }

            if (!DeviceHub.IsDeviceConnected(code))
            {
                return Results.Ok(
                    ApiResponse<bool>.Fail(
                        "장비가 실시간 연결되어 있지 않아 명령이 전달되지 않았습니다. 잠시 후 다시 시도해 주세요.",
                        "ERR_DEVICE_OFFLINE"));
            }

            await hub.Clients.Group(code).SendAsync("UpdateNow");
            return Results.Ok(ApiResponse<bool>.Ok(true,
                "업그레이드 지시를 보냈습니다. 장비가 새 버전을 확인해 설치합니다 (몇 분 걸립니다)."));
        }).WithName("SendPlayerUpdateNow");

        // 장비별 보고된 앱 버전. 플레이어가 접속할 때 보고한 값이다.
        // 메모리 보관이라 서버 재기동 직후에는 비어 있다가 60초 하트비트로 다시 찬다.
        group.MapGet("/player-versions", (HttpContext http) =>
        {
            if (!CanControl(http))
            {
                return Results.Json(
                    ApiResponse<object>.Fail("장비 제어 권한이 없습니다.", "ERR_FORBIDDEN"),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var list = DeviceHub.DeviceVersions()
                .Select(kv => new
                {
                    deviceCode = kv.Key,
                    version = kv.Value.Version,
                    reportedAt = kv.Value.ReportedAt,
                    connected = DeviceHub.IsDeviceConnected(kv.Key),
                })
                .OrderBy(x => x.deviceCode)
                .ToList();
            return Results.Ok(list);
        }).WithName("GetPlayerVersions");
    }
}
