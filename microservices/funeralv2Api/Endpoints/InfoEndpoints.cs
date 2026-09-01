using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 정보 화면 묶음 API — 알림정보 · 호실히스토리 · 고인정보조회 · 나의정보 · 미리보기.
/// </summary>
/// <remarks>
/// 게이트웨이가 <c>/api/funeral</c> 을 떼고 넘기므로 프론트는
/// <c>/funeral/info/...</c> 로 부른다.
/// </remarks>
public static class InfoEndpoints
{
    public static void MapInfoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/info").AddApiResponseWrapper();

        // ── 알림정보 ────────────────────────────────────────────

        group.MapGet("/notice/list", async (
            [FromQuery] string? buildingId,
            [FromQuery] bool includeExpired,
            UserContext? user,
            [FromServices] IInfoService service) =>
        {
            return await service.GetNoticesAsync(RequireUser(user), buildingId, includeExpired);
        })
        .WithName("GetFuneralNotices")
        .WithOpenApi();

        group.MapGet("/notice/{id}", async (
            string id,
            UserContext? user,
            [FromServices] IInfoService service) =>
        {
            var result = await service.GetNoticeByIdAsync(RequireUser(user), id);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<NoticeDto>.Fail("알림을 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("GetFuneralNoticeById")
        .WithOpenApi();

        group.MapPost("/notice", async (
            [FromBody] NoticeCreateDto dto,
            UserContext? user,
            [FromServices] IInfoService service) =>
        {
            return await service.CreateNoticeAsync(RequireUser(user), dto);
        })
        .WithName("CreateFuneralNotice")
        .WithOpenApi();

        group.MapPut("/notice/{id}", async (
            string id,
            [FromBody] NoticeUpdateDto dto,
            UserContext? user,
            [FromServices] IInfoService service) =>
        {
            var result = await service.UpdateNoticeAsync(RequireUser(user), id, dto);
            if (result == null)
            {
                return Results.NotFound(ApiResponse<NoticeDto>.Fail("수정할 알림을 찾을 수 없습니다."));
            }
            return Results.Ok(result);
        })
        .WithName("UpdateFuneralNotice")
        .WithOpenApi();

        group.MapDelete("/notice/{id}", async (string id, [FromServices] IInfoService service) =>
        {
            var ok = await service.DeleteNoticeAsync(id);
            if (!ok)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("삭제할 알림을 찾을 수 없습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("DeleteFuneralNotice")
        .WithOpenApi();

        group.MapPost("/notice/{id}/read", async (
            string id,
            UserContext? user,
            [FromServices] IInfoService service) =>
        {
            var ok = await service.MarkNoticeReadAsync(RequireUser(user), id);
            if (!ok)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("알림을 찾을 수 없습니다."));
            }
            return Results.Ok(true);
        })
        .WithName("MarkFuneralNoticeRead")
        .WithOpenApi();

        group.MapGet("/notice/unread-count", async (
            [FromQuery] string? buildingId,
            UserContext? user,
            [FromServices] IInfoService service) =>
        {
            return await service.CountUnreadNoticesAsync(RequireUser(user), buildingId);
        })
        .WithName("CountUnreadFuneralNotices")
        .WithOpenApi();

        // ── 호실 히스토리 ───────────────────────────────────────

        group.MapGet("/room-history/list", async (
            [FromQuery] string? buildingId,
            [FromQuery] string? roomId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromServices] IInfoService service) =>
        {
            return await service.GetRoomHistoriesAsync(buildingId, roomId, from, to);
        })
        .WithName("GetRoomHistories")
        .WithOpenApi();

        // ── 고인 정보 조회 ──────────────────────────────────────

        group.MapGet("/deceased-search/list", async (
            [FromQuery] string? keyword,
            [FromQuery] string? buildingId,
            [FromQuery] string? roomId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? status,
            [FromServices] IInfoService service) =>
        {
            return await service.SearchDeceasedAsync(keyword, buildingId, roomId, from, to, status);
        })
        .WithName("SearchDeceased")
        .WithOpenApi();

        // ── 나의 정보 ───────────────────────────────────────────

        group.MapGet("/my-info", async (UserContext? user, [FromServices] IInfoService service) =>
        {
            return await service.GetMyInfoAsync(RequireUser(user), user?.Role);
        })
        .WithName("GetFuneralMyInfo")
        .WithOpenApi();

        // ── 미리보기 ────────────────────────────────────────────

        group.MapGet("/preview/list", async (
            [FromQuery] string? buildingId,
            [FromQuery] string? roomId,
            [FromServices] IInfoService service) =>
        {
            return await service.GetDevicePreviewsAsync(buildingId, roomId);
        })
        .WithName("GetDevicePreviews")
        .WithOpenApi();
    }

    /// <summary>
    /// 게이트웨이가 붙여 주는 사용자 아이디. 없으면 익명이라는 뜻인데,
    /// 이 묶음은 전부 로그인 뒤 화면이므로 빈 문자열로 두면 아무것도 못 본다.
    /// </summary>
    private static string RequireUser(UserContext? user) => user?.UserId ?? string.Empty;
}
