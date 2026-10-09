using JSini.Shared.Infrastructure.Time;

using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Entities;

namespace NotificationServer.Services;

/// <summary>
/// 지나온 자리를 <b>쌓고</b> 하루치로 <b>묶어 내는</b> 자리.
/// </summary>
/// <remarks>
/// <para>
/// [왜 설정 저장과 갈라 두었나]
/// </para>
///
/// <para>
/// 좌표가 들어오는 길은 <c>PUT /notifications/preferences/me</c> 하나뿐이고
/// 그 길의 본업은 <b>설정을 고치는 것</b>이다(<see cref="NotificationPreferenceService"/>).
/// 기록 쌓기를 그 안에 넣으면 설정 저장이 실패할 때 기록도 함께 날아가고,
/// 반대로 기록 쌓기가 깨지면 <b>스위치가 안 눌리는 증상</b>으로 나타난다 —
/// 원인에서 가장 먼 모양이다.
/// </para>
///
/// <para>
/// [묶는 자가 서버에 하나 있는 까닭]
/// </para>
///
/// <para>
/// 화면은 같은 묶음을 <b>세 군데</b>에 그린다 — 지도의 점, 목록의 줄, 머리의
/// 요약. 화면이 묶으면 그 셋이 같은 셈을 하리라는 보장이 코드 읽기뿐이다.
/// 게다가 묶는 자(<c>LocationTrackService.StayRadiusMeters</c>)를 고치는 날 고쳐야 할 곳이
/// 둘로 늘어난다.
/// </para>
/// </remarks>
public interface ILocationTrackService
{
    /// <summary>
    /// 방금 잰 좌표 한 점을 쌓는다. <b>너무 촘촘한 중복은 삼킨다</b>.
    /// </summary>
    Task RecordAsync(
        string ownerType, string ownerKey,
        double lat, double lon, double? accuracy, string? place, string actor,
        CancellationToken ct = default);

    /// <summary>한국 달력 하루치. 점 · 머문 자리 · 요약 한 벌이다.</summary>
    Task<MyLocationTrackDto> GetDayAsync(
        string ownerType, string ownerKey, DateOnly koreaDate, CancellationToken ct = default);

    /// <summary>기록이 있는 날들. 최근 것부터다.</summary>
    Task<List<LocationTrackDayDto>> GetDaysAsync(
        string ownerType, string ownerKey, int take, CancellationToken ct = default);
}

/// <inheritdoc cref="ILocationTrackService" />
public class LocationTrackService : ILocationTrackService
{
    private readonly AppDbContext _db;

    public LocationTrackService(AppDbContext db) => _db = db;

    /// <summary>
    /// 이 안쪽의 점들을 <b>한 자리</b>로 본다(m).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>저장하는 쪽의 자(300m)보다 좁다.</b> 저쪽이 묻는 것은 「날씨가 달라질
    /// 만큼 옮겼나」이고(기상청 격자가 5km 칸이다) 여기서 묻는 것은 「같은 자리에
    /// 있었나」다. 300m 로 묶으면 같은 동네의 사무실과 식당이 한 점이 된다 —
    /// 그러면 점심을 나갔다 온 날의 이동이 통째로 사라진다.
    /// </para>
    /// <para>
    /// 더 좁히지 않는 까닭은 GPS 다. 실내에서 받은 좌표는 백 미터쯤 예사로
    /// 흔들어서, 50m 로 묶으면 <b>책상에 앉아 있는 사람이 하루에 열 번 옮겨
    /// 다닌 것</b>이 된다.
    /// </para>
    /// </remarks>
    public const double StayRadiusMeters = 200;

    /// <summary>
    /// 이보다 촘촘히 들어온 같은 자리 좌표는 <b>새 줄로 쌓지 않는다</b>(분).
    /// </summary>
    /// <remarks>
    /// 한 번 재면 저장이 <b>두 번</b> 일어난다 — 좌표를 먼저 넣고, 지역 이름을
    /// 알아낸 뒤 한 번 더다(<c>GeoLocator.SaveAsync</c> 머리말의 순서 둘).
    /// 그대로 두면 잴 때마다 줄이 둘씩 생기고, 둘째 줄은 첫째와 몇 초 차이라
    /// 「머문 시간」에는 아무것도 보태지 못하면서 관측 수만 두 배로 부풀린다.
    /// </remarks>
    private const double MergeWindowMinutes = 3;

    /// <summary>
    /// 그 창 안에서 <b>같은 자리</b>로 칠 거리(m). 몇 초 사이의 GPS 흔들림만 덮는다.
    /// </summary>
    private const double MergeRadiusMeters = 60;

    /// <inheritdoc />
    public async Task RecordAsync(
        string ownerType, string ownerKey,
        double lat, double lon, double? accuracy, string? place, string actor,
        CancellationToken ct = default)
    {
        var now = AppTime.UtcNow;

        // 바로 앞 점. **한 줄만 읽는다** — 쌓기는 좌표가 올 때마다 도는 길이라
        // 여기서 하루치를 훑으면 재는 간격만큼 그 비용이 반복된다.
        var last = await _db.Set<LocationTrack>()
            .Where(t => t.OwnerType == ownerType && t.OwnerKey == ownerKey && !t.IsDeleted)
            .OrderByDescending(t => t.RecordedAt)
            .FirstOrDefaultAsync(ct);

        if (last is not null
            && (now - last.RecordedAt).TotalMinutes < MergeWindowMinutes
            && Distance(last.Lat, last.Lon, lat, lon) < MergeRadiusMeters)
        {
            // **덮어쓰지 않고 채워 넣는다.** 이 두 번째 저장이 들고 오는 것은
            // 대개 지역 이름 하나뿐이라(좌표는 같다), 이름만 받고 물러난다.
            if (!string.IsNullOrWhiteSpace(place) && string.IsNullOrWhiteSpace(last.Place))
            {
                last.Place = place;
                last.UpdatedAt = now;
                last.UpdatedBy = actor;
                await _db.SaveChangesAsync(ct);
            }

            return;
        }

        _db.Set<LocationTrack>().Add(new LocationTrack
        {
            OwnerType = ownerType,
            OwnerKey = ownerKey,
            Lat = lat,
            Lon = lon,
            Accuracy = accuracy,
            Place = string.IsNullOrWhiteSpace(place) ? null : place,

            // 저장하는 쪽과 같은 자로 적어 둔다(`GeoLocator.MoveThresholdMeters`).
            // 앞 점이 없으면 「처음 본 자리」라 옮긴 것으로 친다.
            Moved = last is null || Distance(last.Lat, last.Lon, lat, lon) >= 300,

            RecordedAt = now,
            CreatedAt = now,
            CreatedBy = actor,
        });

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<MyLocationTrackDto> GetDayAsync(
        string ownerType, string ownerKey, DateOnly koreaDate, CancellationToken ct = default)
    {
        // 하루의 경계는 **한국 자정**이다. UTC 자정으로 끊으면 한국의 하루가
        // 아침 아홉 시에 갈려 전날 저녁이 오늘로 넘어온다(docs/utc-time.md).
        var from = AppTime.StartOfDayUtc(koreaDate);
        var to = AppTime.StartOfDayUtc(koreaDate.AddDays(1));

        var rows = await _db.Set<LocationTrack>()
            .AsNoTracking()
            .Where(t => t.OwnerType == ownerType && t.OwnerKey == ownerKey && !t.IsDeleted
                        && t.RecordedAt >= from && t.RecordedAt < to)
            .OrderBy(t => t.RecordedAt)
            .Select(t => new LocationTrackPointDto
            {
                RecordedAt = t.RecordedAt,
                Lat = t.Lat,
                Lon = t.Lon,
                Accuracy = t.Accuracy,
                Place = t.Place,
                Moved = t.Moved,
            })
            .ToListAsync(ct);

        var stays = Cluster(rows);

        return new MyLocationTrackDto
        {
            Date = koreaDate.ToString("yyyy-MM-dd"),
            Points = rows,
            Stays = stays,
            TotalMeters = stays.Sum(s => s.MovedMeters),
            LongestMinutes = stays.Count == 0 ? 0 : stays.Max(s => s.Minutes),
            StayRadiusMeters = StayRadiusMeters,
        };
    }

    /// <inheritdoc />
    public async Task<List<LocationTrackDayDto>> GetDaysAsync(
        string ownerType, string ownerKey, int take, CancellationToken ct = default)
    {
        // **날짜를 DB 에서 세지 않는다.** 한국 달력으로 묶어야 하는데
        // (`at time zone`) 그 식을 EF 로 옮기면 공급자마다 모양이 달라지고,
        // 무엇보다 **색인을 못 탄다** — 칸에 함수를 씌우는 순간이다.
        // 대신 기간을 잘라 읽고 메모리에서 묶는다. 한 사람의 몇 달치다.
        var since = AppTime.StartOfDayUtc(AppTime.TodayInKorea.AddDays(-Math.Max(take, 1) * 3));

        var stamps = await _db.Set<LocationTrack>()
            .AsNoTracking()
            .Where(t => t.OwnerType == ownerType && t.OwnerKey == ownerKey && !t.IsDeleted
                        && t.RecordedAt >= since)
            .Select(t => t.RecordedAt)
            .ToListAsync(ct);

        return [.. stamps
            .GroupBy(at => DateOnly.FromDateTime(AppTime.ToKorea(at)))
            .OrderByDescending(g => g.Key)
            .Take(take)
            .Select(g => new LocationTrackDayDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Count = g.Count(),
            })];
    }

    /// <summary>
    /// 점들을 <b>머문 자리</b>로 묶는다. 시간순으로 한 번 훑는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 견주는 상대는 <b>묶음의 첫 점</b>이지 직전 점이 아니다. 직전 점과 견주면
    /// 백 미터씩 이어 걸은 사람이 하루 종일 한 자리에 있는 것이 된다 —
    /// 묶음이 끊기지 않고 땅을 따라 끌려간다.
    /// </para>
    /// <para>
    /// 묶음의 좌표는 <b>평균</b>이다. 첫 점을 쓰면 들어서는 순간의 흔들린
    /// 좌표가 그 자리의 이름이 되는데, 실내에서 처음 잡은 좌표가 가장 많이
    /// 흔들린다.
    /// </para>
    /// </remarks>
    public static List<LocationStayDto> Cluster(IReadOnlyList<LocationTrackPointDto> points)
    {
        var stays = new List<LocationStayDto>();

        if (points.Count == 0) return stays;

        var group = new List<LocationTrackPointDto>();

        void Flush()
        {
            if (group.Count == 0) return;

            var lat = group.Average(p => p.Lat);
            var lon = group.Average(p => p.Lon);

            var moved = stays.Count == 0
                ? 0
                : Distance(stays[^1].Lat, stays[^1].Lon, lat, lon);

            stays.Add(new LocationStayDto
            {
                Seq = stays.Count + 1,
                Lat = lat,
                Lon = lon,
                Place = group.Select(p => p.Place).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
                ArrivedAt = group[0].RecordedAt,
                LeftAt = group[^1].RecordedAt,
                Minutes = (int)Math.Round((group[^1].RecordedAt - group[0].RecordedAt).TotalMinutes),
                Samples = group.Count,
                MovedMeters = Math.Round(moved),
            });

            group = [];
        }

        foreach (var p in points)
        {
            if (group.Count > 0 && Distance(group[0].Lat, group[0].Lon, p.Lat, p.Lon) > StayRadiusMeters)
            {
                Flush();
            }

            group.Add(p);
        }

        Flush();

        return stays;
    }

    /// <summary>
    /// 두 지점 사이의 거리(m). 하버사인이다.
    /// </summary>
    /// <remarks>
    /// <b>위경도 차를 그대로 재면 안 된다.</b> 경도 1도의 길이는 위도에 따라
    /// 달라서(적도에서 111km, 우리 위도에서 약 89km) 남북과 동서를 같은 자로
    /// 재면 동서 방향 움직임을 실제보다 크게 본다. 화면 쪽의
    /// <c>GeoLocator.DistanceMeters</c> 와 같은 셈이다 — 둘 다 자기 쪽에서
    /// 쓰는 자리라 서로를 참조할 길이 없다.
    /// </remarks>
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6_371_000d;

        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLon = (lon2 - lon1) * Math.PI / 180d;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * earthRadius * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }
}
