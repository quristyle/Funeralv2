using JSini.Shared.DTOs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Services;

namespace NotificationServer.Endpoints;

/// <summary>
/// 위치 엔드포인트 (<c>/api/notification/locations</c>)
/// </summary>
/// <remarks>
/// <para>
/// [왜 알림 서비스에 있나]
/// </para>
///
/// <para>
/// 좌표가 여기 있기 때문이다. 브라우저가 준 위경도는 「내 위치 날씨」를 보내려고
/// <c>scom.notification_preferences</c> 에 받아 두었고(<c>WeatherLat</c>), 그것을
/// 저장하는 길도 읽는 길도 전부 이 서비스다. 표를 새로 만들어 옮기면 <b>사람의
/// 자리를 적는 칸이 둘</b>이 되고, 한쪽만 갱신되는 날이 반드시 온다.
/// </para>
///
/// <para>
/// 게다가 이 화면이 쓰려는 나머지 절반 — 「이 사람에게 쪽지가 닿는가」 — 도
/// 이미 여기 있다(<c>NoteRecipientResolver</c>). 두 조각이 서로 다른 서비스에
/// 있었다면 화면이 둘을 맞추어야 했다.
/// </para>
///
/// <para>
/// [「보내는 일만 한다」는 규칙을 어기는가]
/// </para>
///
/// <para>
/// 어기지 않는다. 그 규칙이 막는 것은 <b>업무 판정</b>(어느 팀이 이 일을 맡나)을
/// 이 서비스가 하는 것이다. 여기서 하는 일은 <b>자기가 저장한 값을 그대로 읽어
/// 주는 것</b>뿐이고, 누구에게 쪽지를 보낼지는 지도를 보는 사람이 정한다.
/// </para>
///
/// <para>
/// [남의 위치를 읽는 자리다 — 권한은 메뉴가 지킨다]
/// </para>
///
/// <para>
/// 자기 것만 보는 길(<c>/notifications/preferences/me</c>)과 다르다. 이쪽은
/// <b>전 직원의 자리</b>를 한 번에 내주므로 포털관리 메뉴 권한 안에서만 열린다 —
/// <c>/preferences</c>(여러 사람의 알림 상태)와 같은 방식이고, 게이트웨이가
/// 로그인 여부를 본다.
/// </para>
/// </remarks>
public static class LocationEndpoints
{
    /// <summary>
    /// 좌표의 주인 종류. 포털 계정은 <c>jsini</c> 다(로그인 아이디가 열쇠).
    /// </summary>
    private const string PortalOwnerType = "jsini";

    public static void MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/locations").WithTags("Locations");

        // ── 위치를 허용한 계정들 ────────────────────────────
        //
        // **좌표가 있는 줄만 낸다.** 「위치를 허용한 계정」이 이 목록의 뜻이라,
        // 허용하지 않은 사람을 좌표 없이 실으면 지도는 못 찍으면서 목록만
        // 길어진다. 화면이 거르게 두면 그 규칙이 화면마다 갈라진다.
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

            // 이름 · 부서 · 이메일과 「쪽지가 닿는가」를 한 번에 읽는다.
            // **판정을 여기서 다시 쓰지 않는다** — 쪽지 보내기·찾기가 쓰는 것과
            // 같은 자리에서 가져와야 세 곳이 갈라지지 않는다.
            var people = await resolver.LoadByLoginIdsAsync(rows.Select(r => r.OwnerKey), ct);

            var byLogin = people.ToDictionary(p => p.LoginId, StringComparer.OrdinalIgnoreCase);

            var items = new List<AccountLocationDto>(rows.Count);

            foreach (var r in rows)
            {
                // **계정이 없는 좌표는 버린다.** 지워진 사람의 설정 행이 남아
                // 있는 경우가 있는데, 이름 없는 점을 지도에 찍으면 누구인지
                // 물을 곳이 없고 쪽지도 보낼 수 없다.
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

            // 이름순. 지도는 순서를 안 보지만 옆의 목록이 본다 — 자리가 매번
            // 달라지면 조회를 다시 할 때마다 눈으로 사람을 다시 찾아야 한다.
            var ordered = items
                .OrderBy(i => i.Name ?? i.LoginId, StringComparer.CurrentCulture)
                .ToList();

            return Results.Ok(ApiResponse<List<AccountLocationDto>>.Ok(ordered));
        })
        .WithName("GetAccountLocations");
    }
}
