using JSini.Shared.DTOs;
using JSini.Shared.Infrastructure.Time;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>위치 엔드포인트(/api/notification/locations). 좌표는 이 서비스의 notification_preferences 에 있다.
/// 남의 자리를 내주므로 포털관리 메뉴 권한 안에서만 열리고, 나의 이동 경로는 주인을 인자로 받지 않는다.</summary>
public static class LocationEndpoints
{
    /// <summary>좌표의 주인 종류. 포털 계정은 jsini 다(로그인 아이디가 열쇠).</summary>
    private const string PortalOwnerType = "jsini";

    public static void MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/locations").WithTags("Locations");

        // 위치를 허용한 계정만 낸다(좌표 없이 실으면 지도는 못 찍고 목록만 길어진다).
        group.MapGet("", async (
            UserContext? user,
            [FromServices] AppDbContext db,
            [FromServices] INoteRecipientResolver resolver,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var rows = await db.NotificationPreferences
                .AsNoTracking()
                .Where(p => p.OwnerType == PortalOwnerType
                            && !p.IsDeleted
                            && p.WeatherLat != null
                            && p.WeatherLon != null)
                .Select(p => new
                {
                    p.OwnerKey,
                    Lat = p.WeatherLat!.Value,
                    Lon = p.WeatherLon!.Value,
                    p.WeatherPlace,
                    p.WeatherLocatedAt,
                    p.WeatherSyncedAt,
                    p.WeatherLocalEnabled,
                })
                .ToListAsync(ct);

            if (rows.Count == 0)
            {
                return Results.Ok(ApiResponse<List<AccountLocationDto>>.Ok([]));
            }

            // 이름·부서·이메일과 쪽지 가능 여부를 한 번에 읽는다(판정은 쪽지 보내기·찾기와 같은 자리에서 가져온다).
            var people = await resolver.LoadByLoginIdsAsync(rows.Select(r => r.OwnerKey), ct);

            var byLogin = people.ToDictionary(p => p.LoginId, StringComparer.OrdinalIgnoreCase);

            var items = new List<AccountLocationDto>(rows.Count);

            foreach (var r in rows)
            {
                // 계정이 없는 좌표는 버린다(이름 없는 점은 쪽지를 보낼 곳도 없다).
                if (!byLogin.TryGetValue(r.OwnerKey, out var who)) continue;

                items.Add(new AccountLocationDto
                {
                    LoginId = who.LoginId,
                    Name = who.Name,
                    Affiliation = who.Affiliation,
                    Email = who.Email,
                    Lat = r.Lat,
                    Lon = r.Lon,
                    Place = r.WeatherPlace,
                    LocatedAt = r.WeatherLocatedAt,
                    SyncedAt = r.WeatherSyncedAt,
                    WeatherLocalEnabled = r.WeatherLocalEnabled,
                    PushReachable = who.PushReachable,
                    EmailReachable = who.EmailReachable,
                });
            }

            // 이름순으로 낸다(옆 목록과 자리가 매번 달라지지 않게).
            var ordered = items
                .OrderBy(i => i.Name ?? i.LoginId, StringComparer.CurrentCulture)
                .ToList();

            return Results.Ok(ApiResponse<List<AccountLocationDto>>.Ok(ordered));
        })
        .WithName("GetAccountLocations");

        // ── 나의 하루치 이동 경로 ───────────────────────────
        // 주인을 인자로 받지 않는다(/me 로만 연다 — 가장 확실한 방어다).
        group.MapGet("/me/track", async (
            UserContext? user,
            [FromQuery] string? date,
            [FromServices] ILocationTrackService tracks,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            // 날짜는 한국 달력이다. 못 읽으면 오늘로 본다(400 이면 화면이 처음 열릴 때 빈손이 된다).
            var day = DateOnly.TryParse(date, out var parsed) ? parsed : AppTime.TodayInKorea;

            var result = await tracks.GetDayAsync(PortalOwnerType, user.UserId, day, ct);

            return Results.Ok(ApiResponse<MyLocationTrackDto>.Ok(result));
        })
        .WithName("GetMyLocationTrack");

        // ── 기록이 있는 날들 ────────────────────────────────
        // 기록이 있는 날만 내준다(날짜 고르개가 빈 날을 피하게 한다).
        group.MapGet("/me/track/days", async (
            UserContext? user,
            [FromQuery] int? take,
            [FromServices] ILocationTrackService tracks,
            CancellationToken ct = default) =>
        {
            if (user is null) return Results.Unauthorized();

            var days = await tracks.GetDaysAsync(
                PortalOwnerType, user.UserId, Math.Clamp(take ?? 30, 1, 180), ct);

            return Results.Ok(ApiResponse<List<LocationTrackDayDto>>.Ok(days));
        })
        .WithName("GetMyLocationTrackDays");
    }
}
