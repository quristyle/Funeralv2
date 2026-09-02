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

        // 행이 없으면 엔티티의 기본값(푸시·이메일 켜짐, 날씨 꺼짐)을 그대로 쓴다.
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

    private static NotificationPreferenceDto ToDto(Entities.NotificationPreference row) => new()
    {
        PushEnabled = row.PushEnabled,
        EmailEnabled = row.EmailEnabled,
        WeatherEnabled = row.WeatherEnabled,
        Saved = true,
        UpdatedAt = row.UpdatedAt ?? row.CreatedAt
    };
}
