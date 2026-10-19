using JSini.Shared.Infrastructure.Time;

using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Entities;

namespace NotificationServer.Services;

/// <summary>지나온 자리를 쌓고 하루치로 묶어 낸다. 설정 저장과 갈라 둔다(기록 쌓기가 깨질 때 증상이 「스위치가 안 눌린다」로 나타나지 않게).
/// 묶는 자는 서버에 하나다(화면이 묶으면 지도·목록·요약의 셈이 갈린다).</summary>
public interface ILocationTrackService
{
    /// <summary>방금 잰 좌표 한 점을 쌓는다. 너무 촘촘한 중복은 삼킨다.</summary>
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

    /// <summary>이 안쪽의 점들을 한 자리로 본다(m). 저장하는 쪽의 300m 보다 좁게 둔다 — 300m 면 같은 동네의 사무실과 식당이 한 점이 되고,
    /// 50m 로 좁히면 GPS 흔들림에 책상에 앉은 사람이 하루에 열 번 옮긴 것이 된다.</summary>
    public const double StayRadiusMeters = 200;

    /// <summary>이보다 촘촘히 들어온 같은 자리 좌표는 새 줄로 쌓지 않는다(분). 한 번 재면 저장이 두 번 일어나므로 그대로 두면 관측 수만 두 배로 부푼다.</summary>
    private const double MergeWindowMinutes = 3;

    /// <summary>그 창 안에서 같은 자리로 칠 거리(m). 몇 초 사이의 GPS 흔들림만 덮는다.</summary>
    private const double MergeRadiusMeters = 60;

    /// <inheritdoc />
    public async Task RecordAsync(
        string ownerType, string ownerKey,
        double lat, double lon, double? accuracy, string? place, string actor,
        CancellationToken ct = default)
    {
        var now = AppTime.UtcNow;

        // 바로 앞 점 한 줄만 읽는다 — 쌓기는 좌표가 올 때마다 도므로 하루치를 훑으면 비용이 반복된다.
        var last = await _db.Set<LocationTrack>()
            .Where(t => t.OwnerType == ownerType && t.OwnerKey == ownerKey && !t.IsDeleted)
            .OrderByDescending(t => t.RecordedAt)
            .FirstOrDefaultAsync(ct);

        if (last is not null
            && (now - last.RecordedAt).TotalMinutes < MergeWindowMinutes
            && Distance(last.Lat, last.Lon, lat, lon) < MergeRadiusMeters)
        {
            // 두 번째 저장은 지역 이름만 채운다 — 덮어쓰지 않고 빈 칸만 채운다.
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

            // 저장하는 쪽과 같은 자(GeoLocator.MoveThresholdMeters)로 적는다. 앞 점이 없으면 처음 본 자리라 옮긴 것으로 친다.
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
        // 하루의 경계는 한국 자정이다 — UTC 자정으로 끊으면 한국의 하루가 아침 아홉 시에 갈린다.
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
        // 날짜는 DB 에서 세지 않는다 — 한국 달력 식을 DB 에 씌우면 색인을 못 타므로 기간을 잘라 읽고 메모리에서 묶는다.
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

    /// <summary>점들을 머문 자리로 묶는다. 견주는 상대는 직전 점이 아니라 묶음의 첫 점이다(직전과 견주면 이어 걸은 사람이 하루 종일 한 자리가 된다). 묶음 좌표는 평균이다.</summary>
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

    /// <summary>두 지점 사이의 거리(m). 하버사인이다. 위경도 차를 그대로 재면 경도 1도의 길이가 위도에 따라 달라 동서 움직임을 실제보다 크게 본다.</summary>
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
