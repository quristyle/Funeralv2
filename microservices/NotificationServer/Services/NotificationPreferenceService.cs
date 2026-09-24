using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>
/// 사람별 알림 수신 설정을 읽고 쓴다.
/// </summary>
public interface INotificationPreferenceService
{
    /// <summary>한 사람의 설정. 저장한 적이 없으면 기본값을 <c>Saved = false</c> 로 준다.</summary>
    Task<NotificationPreferenceDto> GetAsync(
        string ownerType, string ownerKey, CancellationToken ct = default);

    /// <summary>준 항목만 바꾼다. 행이 없으면 만든다.</summary>
    Task<NotificationPreferenceDto> SaveAsync(
        string ownerType, string ownerKey,
        UpdateNotificationPreferenceDto request,
        string actor, CancellationToken ct = default);

    /// <summary>
    /// 푸시를 <b>끈</b> 주인들. 발송 직전에 걸러내려고 부른다.
    /// </summary>
    /// <remarks>
    /// "켠 사람" 이 아니라 "끈 사람" 을 가져오는 것이 중요하다 — 행이 없으면 켜짐이므로
    /// 켠 사람을 물으면 표에 없는 대다수가 빠진다.
    /// </remarks>
    Task<HashSet<(string OwnerType, string OwnerKey)>> GetPushDisabledAsync(
        IEnumerable<OwnerRefDto> owners, CancellationToken ct = default);

    /// <summary>이메일을 <b>끈</b> 포털 로그인 아이디들.</summary>
    Task<HashSet<string>> GetEmailDisabledLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default);

    /// <summary>
    /// 쪽지 메일받기를 <b>켠</b> 포털 로그인 아이디들.
    /// </summary>
    /// <remarks>
    /// 위의 둘과 방향이 반대다. 푸시·업무 메일은 <b>행이 없으면 켜짐</b>이라 "끈
    /// 사람" 을 물어야 하지만, 쪽지 메일은 <b>행이 없으면 꺼짐</b>이다 — 일부러
    /// 켠 사람에게만 간다.
    /// </remarks>
    Task<HashSet<string>> GetNoteEmailEnabledLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default);

    /// <summary>
    /// 「내 위치 날씨」를 켜고 <b>위치까지 잡아 둔</b> 사람들.
    /// </summary>
    /// <remarks>
    /// 위경도가 없는 행은 뺀다 — 켜 두기만 하고 위치를 안 준 사람에게는 보낼 곳이
    /// 없고, 그것을 목록에 남기면 받아 가는 쪽이 매번 걸러야 한다.
    /// </remarks>
    Task<List<LocalWeatherSubscriberDto>> GetLocalWeatherSubscribersAsync(
        CancellationToken ct = default);

    /// <summary>
    /// 「내 위치 날씨」를 보낸 시각을 찍는다. 지역 이름을 함께 주면 그것도 적어 둔다.
    /// </summary>
    Task MarkLocalWeatherSentAsync(
        string ownerType, string ownerKey, string? place, CancellationToken ct = default);
}

/// <inheritdoc cref="INotificationPreferenceService" />
/// <remarks>
/// <b>이 서비스가 D8-A("보내는 일만 한다")를 어기는 것이 아니다.</b>
/// 부르는 쪽은 여전히 "누구에게" 를 정한다 — 헬프데스크는 팀을 알고, 장례식장은
/// 담당자를 안다. 다만 <b>받는 사람 본인이 껐다</b> 는 것은 대상 선택이 아니라
/// 수신자의 속성이고, 기기 목록이 이미 여기 있으므로 여기서 지키는 것이 맞다.
/// 부르는 쪽마다 "이 사람이 껐나" 를 기억하게 하면 한 곳만 잊어도 새는 설정이 된다.
/// </remarks>
public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly AppDbContext _db;

    public NotificationPreferenceService(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<NotificationPreferenceDto> GetAsync(
        string ownerType, string ownerKey, CancellationToken ct = default)
    {
        var row = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.OwnerType == ownerType && p.OwnerKey == ownerKey, ct);

        // 행이 없으면 엔티티의 기본값(푸시·이메일 켜짐, 날씨·쪽지 메일 꺼짐)을 그대로 쓴다.
        return row is null
            ? new NotificationPreferenceDto { Saved = false }
            : ToDto(row);
    }

    /// <inheritdoc />
    public async Task<NotificationPreferenceDto> SaveAsync(
        string ownerType, string ownerKey,
        UpdateNotificationPreferenceDto request,
        string actor, CancellationToken ct = default)
    {
        var row = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.OwnerType == ownerType && p.OwnerKey == ownerKey, ct);

        if (row is null)
        {
            row = new Entities.NotificationPreference
            {
                OwnerType = ownerType,
                OwnerKey = ownerKey,
                CreatedBy = actor
            };
            _db.NotificationPreferences.Add(row);
        }
        else
        {
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = actor;
        }

        // 준 것만 바꾼다. 스위치 하나를 눌렀을 때 나머지가 기본값으로 되돌아가면 안 된다.
        if (request.PushEnabled.HasValue) row.PushEnabled = request.PushEnabled.Value;
        if (request.EmailEnabled.HasValue) row.EmailEnabled = request.EmailEnabled.Value;
        if (request.WeatherEnabled.HasValue) row.WeatherEnabled = request.WeatherEnabled.Value;
        if (request.NoteEmailEnabled.HasValue) row.NoteEmailEnabled = request.NoteEmailEnabled.Value;
        if (request.WeatherLocalEnabled.HasValue) row.WeatherLocalEnabled = request.WeatherLocalEnabled.Value;
        if (request.WeatherHours is not null) row.WeatherHours = NormalizeHours(request.WeatherHours);

        // 위경도는 **짝으로만** 받는다. 하나만 바꾸면 위도는 새 곳, 경도는 옛 곳인
        // 지점이 만들어져 엉뚱한 동네의 날씨가 간다 — 그 값은 아무도 의심하지 않는다.
        if (request.WeatherLat.HasValue && request.WeatherLon.HasValue)
        {
            // **좌표가 실제로 달라졌을 때만 「잡은 때」를 다시 찍는다.** 화면은 스위치
            // 하나를 눌러도 설정 전체를 보내므로(ToggleAsync), 무조건 찍으면 푸시
            // 스위치를 만질 때마다 위치를 방금 잡은 것이 된다 — 그 값은 「이사한 뒤에도
            // 옛 동네 날씨가 오는가」를 가리는 유일한 단서라, 늘 오늘이면 쓸모가 없다.
            var moved = row.WeatherLat != request.WeatherLat.Value
                        || row.WeatherLon != request.WeatherLon.Value;

            row.WeatherLat = request.WeatherLat.Value;
            row.WeatherLon = request.WeatherLon.Value;

            // **자리가 안 바뀌었으면 이름을 지우지 않는다.** 저절로 다시 재는 쪽은
            // (`GeoLocator` 의 조용한 확인) 좌표만 보내는데, 무조건 덮으면 그때마다
            // 지역 이름이 사라져 설정 화면이 위경도 숫자만 보여 준다. 자리가 바뀌었을
            // 때는 반대로 **지워야 한다** — 옛 동네 이름이 새 좌표에 붙어 남는다.
            if (moved || request.WeatherPlace is not null) row.WeatherPlace = request.WeatherPlace;

            if (moved || row.WeatherLocatedAt is null) row.WeatherLocatedAt = DateTime.UtcNow;

            // **「확인한 때」는 재어 보낸 쪽만 찍는다.** 좌표가 실려 있다고 찍으면
            // 스위치 하나를 눌러도(설정 전체가 간다) 방금 확인한 것이 된다.
            if (request.WeatherLocated == true) row.WeatherSyncedAt = DateTime.UtcNow;
        }
        else if (request.WeatherPlace is not null)
        {
            // 이름만 오는 경우가 있다 — 발송기가 격자표에서 찾아 채워 보낼 때다.
            row.WeatherPlace = request.WeatherPlace;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    /// <inheritdoc />
    public async Task<HashSet<(string OwnerType, string OwnerKey)>> GetPushDisabledAsync(
        IEnumerable<OwnerRefDto> owners, CancellationToken ct = default)
    {
        var list = owners
            .Where(o => !string.IsNullOrWhiteSpace(o.OwnerType) && !string.IsNullOrWhiteSpace(o.OwnerKey))
            .ToList();

        var disabled = new HashSet<(string, string)>();
        if (list.Count == 0) return disabled;

        // 구독 조회와 같은 이유로 종류별로 나눠 IN 질의를 돌린다 (PushSender 주석 참고).
        foreach (var group in list.GroupBy(o => o.OwnerType))
        {
            var keys = group.Select(o => o.OwnerKey).Distinct().ToList();
            var rows = await _db.NotificationPreferences
                .Where(p => p.OwnerType == group.Key && keys.Contains(p.OwnerKey) && !p.PushEnabled)
                .Select(p => new { p.OwnerType, p.OwnerKey })
                .ToListAsync(ct);

            foreach (var r in rows) disabled.Add((r.OwnerType, r.OwnerKey));
        }

        return disabled;
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetEmailDisabledLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default)
    {
        var keys = loginIds.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keys.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

        var rows = await _db.NotificationPreferences
            .Where(p => p.OwnerType == "jsini" && keys.Contains(p.OwnerKey) && !p.EmailEnabled)
            .Select(p => p.OwnerKey)
            .ToListAsync(ct);

        return rows.ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetNoteEmailEnabledLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default)
    {
        var keys = loginIds.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keys.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

        var rows = await _db.NotificationPreferences
            .Where(p => p.OwnerType == "jsini" && keys.Contains(p.OwnerKey) && p.NoteEmailEnabled)
            .Select(p => p.OwnerKey)
            .ToListAsync(ct);

        return rows.ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<List<LocalWeatherSubscriberDto>> GetLocalWeatherSubscribersAsync(
        CancellationToken ct = default)
    {
        return await _db.NotificationPreferences
            .Where(p => p.WeatherLocalEnabled
                        && !p.IsDeleted
                        && p.WeatherLat != null
                        && p.WeatherLon != null)
            .Select(p => new LocalWeatherSubscriberDto
            {
                OwnerType = p.OwnerType,
                OwnerKey = p.OwnerKey,
                Lat = p.WeatherLat!.Value,
                Lon = p.WeatherLon!.Value,
                Place = p.WeatherPlace,
                Hours = p.WeatherHours,
                LastSentAt = p.WeatherLocalSentAt
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task MarkLocalWeatherSentAsync(
        string ownerType, string ownerKey, string? place, CancellationToken ct = default)
    {
        var row = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.OwnerType == ownerType && p.OwnerKey == ownerKey, ct);

        if (row is null) return;

        row.WeatherLocalSentAt = DateTime.UtcNow;

        // 이름은 **비어 있을 때만** 채운다. 사람이 화면에서 고쳐 둔 이름을
        // 발송기가 매번 덮으면 고친 것이 남지 않는다.
        if (!string.IsNullOrWhiteSpace(place) && string.IsNullOrWhiteSpace(row.WeatherPlace))
        {
            row.WeatherPlace = place;
        }

        // **UpdatedAt 을 건드리지 않는다.** 화면이 그것을 「마지막 저장」으로 보여 주는데,
        // 알림이 나갈 때마다 바뀌면 사람이 고친 적 없는 설정이 방금 고쳐진 것처럼 보인다.
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 시각 목록을 <c>7,18</c> 꼴로 다듬는다. 범위를 벗어난 값과 중복을 버린다.
    /// </summary>
    /// <remarks>
    /// 비어 있으면 <c>null</c> 이다 — 「고르지 않았다」와 「하나도 안 고름」을 같게
    /// 둔다. 하나도 없으면 보낼 시각이 없어 알림이 조용히 멎는데, 그것은 스위치를
    /// 끄는 일이지 시각을 고르는 일이 아니다.
    /// </remarks>
    private static string? NormalizeHours(string raw)
    {
        var hours = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var h) ? h : -1)
            .Where(h => h is >= 0 and <= 23)
            .Distinct()
            .OrderBy(h => h)
            .ToList();

        return hours.Count == 0 ? null : string.Join(",", hours);
    }

    private static NotificationPreferenceDto ToDto(Entities.NotificationPreference row) => new()
    {
        PushEnabled = row.PushEnabled,
        EmailEnabled = row.EmailEnabled,
        WeatherEnabled = row.WeatherEnabled,
        NoteEmailEnabled = row.NoteEmailEnabled,
        WeatherLocalEnabled = row.WeatherLocalEnabled,
        WeatherLat = row.WeatherLat,
        WeatherLon = row.WeatherLon,
        WeatherPlace = row.WeatherPlace,
        WeatherHours = row.WeatherHours,
        WeatherLocatedAt = row.WeatherLocatedAt,
        WeatherSyncedAt = row.WeatherSyncedAt,
        Saved = true,
        UpdatedAt = row.UpdatedAt ?? row.CreatedAt
    };
}
