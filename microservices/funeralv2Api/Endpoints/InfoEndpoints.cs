using Microsoft.AspNetCore.Mvc;
using funeralv2Api.Services;
using funeralv2Api.DTOs;
using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Filters;

namespace funeralv2Api.Endpoints;

/// <summary>
/// 정보 화면 묶음 API — 호실히스토리 · 고인정보조회 · 나의정보 · 미리보기.
/// </summary>
/// <remarks>
/// 게이트웨이가 <c>/api/funeral</c> 을 떼고 넘기므로 프론트는
/// <c>/funeral/info/...</c> 로 부른다.
///
/// <para>
/// <b>알림정보(<c>/notice/*</c>)는 2026-09-03 에 걷어냈다.</b> 쓰지 않는 화면이었고
/// 그 자리는 포털 공지(AuthServer)와 알림 설정(NotificationServer)이 채우고 있다.
/// 표 둘(<c>funeral_notices</c> · <c>funeral_notice_reads</c>)도 함께 지웠다 —
/// 행이 하나도 없었다. 마이그레이션은 <c>RemoveFuneralNotices</c> 다.
/// </para>
/// </remarks>
public static class InfoEndpoints
{
    public static void MapInfoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/info").AddApiResponseWrapper();

        // ── 호실 히스토리 ───────────────────────────────────────

        group.MapGet("/room-history/list", async (
            [FromQuery] string? buildingId,
            [FromQuery] string? roomId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            // 고인 성명 일부. 호실을 몰라도 이름으로 바로 찾을 수 있게 두었다.
            [FromQuery] string? keyword,
            // 사용 중 / 출상 가리기. 값이 없으면 둘 다 준다.
            [FromQuery] bool? inUse,
            [FromServices] IInfoService service) =>
        {
            return await service.GetRoomHistoriesAsync(
                buildingId, roomId, from, to, keyword, inUse);
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
