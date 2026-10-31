using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>사람별 알림 수신 설정을 읽고 쓴다.</summary>
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

    /// <summary>푸시를 끈 주인들. 발송 직전에 거르려고 부른다 — 행이 없으면 켜짐이라 켠 사람을 물으면 대다수가 빠진다.</summary>
    Task<HashSet<(string OwnerType, string OwnerKey)>> GetPushDisabledAsync(
        IEnumerable<OwnerRefDto> owners, CancellationToken ct = default);

    /// <summary>이메일을 <b>끈</b> 포털 로그인 아이디들.</summary>
    Task<HashSet<string>> GetEmailDisabledLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default);

    /// <summary>댓글 앱 푸시를 끈 주인들. 푸시 자체를 끈 사람 위에 한 번 더 걸러 낸다.</summary>
    Task<HashSet<(string OwnerType, string OwnerKey)>> GetCommentPushDisabledAsync(
        IEnumerable<OwnerRefDto> owners, CancellationToken ct = default);

    /// <summary>댓글 이메일을 끈 포털 로그인 아이디들. 업무 메일 스위치와 갈래가 달라 따로 본다.</summary>
    Task<HashSet<string>> GetCommentEmailDisabledLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default);

    /// <summary>쪽지 메일받기를 켠 포털 로그인 아이디들. 행이 없으면 꺼짐이라 켠 사람만 준다(위 둘과 방향이 반대다).</summary>
    Task<HashSet<string>> GetNoteEmailEnabledLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default);

    /// <summary>이 이벤트의 앱 푸시를 본인이 끈 주인들(이벤트별 설정). 정책이 안 걸리는 이벤트는 부르지 않는다.</summary>
    Task<HashSet<(string OwnerType, string OwnerKey)>> GetEventPushDisabledAsync(
        string eventCode, IEnumerable<OwnerRefDto> owners, CancellationToken ct = default);

    /// <summary>이 이벤트의 메일을 본인이 끈 포털 로그인 아이디들. 대소문자를 가리지 않는다.</summary>
    Task<HashSet<string>> GetEventEmailDisabledLoginIdsAsync(
        string eventCode, IEnumerable<string> loginIds, CancellationToken ct = default);

    /// <summary>내 위치 날씨를 켜고 위치까지 잡아 둔 사람들. 위경도가 없는 행은 뺀다 — 보낼 곳이 없다.</summary>
    Task<List<LocalWeatherSubscriberDto>> GetLocalWeatherSubscribersAsync(
        CancellationToken ct = default);

    /// <summary>「내 위치 날씨」를 보낸 시각을 찍는다. 지역 이름을 함께 주면 그것도 적는다.</summary>
    Task MarkLocalWeatherSentAsync(
        string ownerType, string ownerKey, string? place, CancellationToken ct = default);
}

/// <inheritdoc cref="INotificationPreferenceService" />
/// 받는 사람 본인이 끈 것은 수신자의 속성이라 여기서 지킨다.
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
        if (request.CommentPushEnabled.HasValue) row.CommentPushEnabled = request.CommentPushEnabled.Value;
        if (request.CommentEmailEnabled.HasValue) row.CommentEmailEnabled = request.CommentEmailEnabled.Value;
        if (request.WeatherLocalEnabled.HasValue) row.WeatherLocalEnabled = request.WeatherLocalEnabled.Value;
        if (request.WeatherHours is not null) row.WeatherHours = NormalizeHours(request.WeatherHours);

        // 위경도는 짝으로만 받는다 — 하나만 바꾸면 엉뚱한 동네의 날씨가 간다.
        if (request.WeatherLat.HasValue && request.WeatherLon.HasValue)
        {
            // 좌표가 실제로 달라졌을 때만 「잡은 때」를 다시 찍는다(설정 전체가 같이 오므로 무조건 찍으면 방금 잡은 것처럼 보인다).
            var moved = row.WeatherLat != request.WeatherLat.Value
                        || row.WeatherLon != request.WeatherLon.Value;

            row.WeatherLat = request.WeatherLat.Value;
            row.WeatherLon = request.WeatherLon.Value;

            // 자리가 안 바뀌었으면 지역 이름을 지우지 않는다. 바뀌었을 때는 옛 동네 이름이 새 좌표에 붙지 않게 지운다.
            if (moved || request.WeatherPlace is not null) row.WeatherPlace = request.WeatherPlace;

            if (moved || row.WeatherLocatedAt is null) row.WeatherLocatedAt = DateTime.UtcNow;

            // 「확인한 때」는 재어 보낸 쪽만 찍는다(좌표가 실려 있다고 찍으면 스위치 하나에도 확인한 것이 된다).
            if (request.WeatherLocated == true) row.WeatherSyncedAt = DateTime.UtcNow;
        }
        else if (request.WeatherPlace is not null)
        {
            // 이름만 오는 경우가 있다(발송기가 격자표에서 찾아 채워 보낼 때다).
            row.WeatherPlace = request.WeatherPlace;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    /// <inheritdoc />
    public Task<HashSet<(string OwnerType, string OwnerKey)>> GetPushDisabledAsync(
        IEnumerable<OwnerRefDto> owners, CancellationToken ct = default)
        => DisabledOwnersAsync(owners, p => !p.PushEnabled, ct);

    /// <inheritdoc />
    public Task<HashSet<(string OwnerType, string OwnerKey)>> GetCommentPushDisabledAsync(
        IEnumerable<OwnerRefDto> owners, CancellationToken ct = default)
        => DisabledOwnersAsync(owners, p => !p.CommentPushEnabled, ct);

    /// <summary>스위치 하나를 끈 주인들을 고른다. 행이 없으면 켜짐이므로 켠 사람이 아니라 끈 사람을 묻는다.</summary>
    private async Task<HashSet<(string OwnerType, string OwnerKey)>> DisabledOwnersAsync(
        IEnumerable<OwnerRefDto> owners,
        Expression<Func<Entities.NotificationPreference, bool>> isOff,
        CancellationToken ct)
    {
        var list = owners
            .Where(o => !string.IsNullOrWhiteSpace(o.OwnerType) && !string.IsNullOrWhiteSpace(o.OwnerKey))
            .ToList();

        var disabled = new HashSet<(string, string)>();
        if (list.Count == 0) return disabled;

        // 구독 조회와 같은 이유로 종류별로 나눠 IN 질의를 돈다(PushSender 참고).
        foreach (var group in list.GroupBy(o => o.OwnerType))
        {
            var keys = group.Select(o => o.OwnerKey).Distinct().ToList();
            var rows = await _db.NotificationPreferences
                .Where(p => p.OwnerType == group.Key && keys.Contains(p.OwnerKey))
                .Where(isOff)
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
    public async Task<HashSet<string>> GetCommentEmailDisabledLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default)
    {
        var keys = loginIds.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (keys.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

        var rows = await _db.NotificationPreferences
            .Where(p => p.OwnerType == "jsini" && keys.Contains(p.OwnerKey) && !p.CommentEmailEnabled)
            .Select(p => p.OwnerKey)
            .ToListAsync(ct);

        // 대소문자를 가리지 않는다(못 알아보면 껐는데도 간다).
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
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
    public async Task<HashSet<(string OwnerType, string OwnerKey)>> GetEventPushDisabledAsync(
        string eventCode, IEnumerable<OwnerRefDto> owners, CancellationToken ct = default)
    {
        var disabled = new HashSet<(string, string)>();

        var list = owners
            .Where(o => !string.IsNullOrWhiteSpace(o.OwnerType) && !string.IsNullOrWhiteSpace(o.OwnerKey))
            .ToList();

        if (string.IsNullOrWhiteSpace(eventCode) || list.Count == 0) return disabled;

        foreach (var group in list.GroupBy(o => o.OwnerType))
        {
            var keys = group.Select(o => o.OwnerKey).Distinct().ToList();
            var rows = await _db.NotificationUserEventPrefs
                .Where(p => p.EventCode == eventCode && !p.IsDeleted && !p.PushEnabled)
                .Where(p => p.OwnerType == group.Key && keys.Contains(p.OwnerKey))
                .Select(p => new { p.OwnerType, p.OwnerKey })
                .ToListAsync(ct);

            foreach (var r in rows) disabled.Add((r.OwnerType, r.OwnerKey));
        }

        return disabled;
    }

    public async Task<HashSet<string>> GetEventEmailDisabledLoginIdsAsync(
        string eventCode, IEnumerable<string> loginIds, CancellationToken ct = default)
    {
        var keys = loginIds.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (string.IsNullOrWhiteSpace(eventCode) || keys.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await _db.NotificationUserEventPrefs
            .Where(p => p.EventCode == eventCode && !p.IsDeleted && !p.EmailEnabled)
            .Where(p => p.OwnerType == "jsini" && keys.Contains(p.OwnerKey))
            .Select(p => p.OwnerKey)
            .ToListAsync(ct);

        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

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

        // 이름은 비어 있을 때만 채운다(발송기가 매번 덮으면 화면에서 고친 이름이 남지 않는다).
        if (!string.IsNullOrWhiteSpace(place) && string.IsNullOrWhiteSpace(row.WeatherPlace))
        {
            row.WeatherPlace = place;
        }

        // UpdatedAt 은 건드리지 않는다(알림이 나갈 때마다 바뀌면 고친 적 없는 설정이 방금 고쳐진 것처럼 보인다).
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>시각 목록을 7,18 꼴로 다듬는다. 범위를 벗어난 값과 중복을 버린다. 하나도 없으면 null 이다.</summary>
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
        CommentPushEnabled = row.CommentPushEnabled,
        CommentEmailEnabled = row.CommentEmailEnabled,
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
