using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 환경설정 API — 계정별 장례식장 업무 설정.
/// </summary>
public static class SettingEndpoints
{
    public static void MapSettingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/setting").AddApiResponseWrapper();

        group.MapGet("/environment/list", async (UserContext? user, [FromServices] ISettingService service) =>
        {
            return await service.GetSettingsAsync(user?.UserId ?? string.Empty);
        })
        .WithName("GetEnvironmentSettings")
        .WithOpenApi();

        group.MapPut("/environment/{code}", async (
            string code,
            [FromBody] AccountSettingUpdateDto dto,
            UserContext? user,
            [FromServices] ISettingService service) =>
        {
            var result = await service.UpdateSettingAsync(user?.UserId ?? string.Empty, code, dto.Enabled);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<AccountSettingDto>.Fail("그런 설정 항목이 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("UpdateEnvironmentSetting")
        .WithOpenApi();

        // 화면의 저장 버튼 하나로 여러 줄을 한 번에 바꾼다.
        group.MapPut("/environment", async (
            [FromBody] AccountSettingBulkUpdateDto dto,
            UserContext? user,
            [FromServices] ISettingService service) =>
        {
            return await service.UpdateSettingsAsync(user?.UserId ?? string.Empty, dto.Settings);
        })
        .WithName("UpdateEnvironmentSettings")
        .WithOpenApi();
    }
}
