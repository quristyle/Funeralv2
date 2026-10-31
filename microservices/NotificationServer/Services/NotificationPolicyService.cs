using JSini.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Entities;

namespace NotificationServer.Services;

/// <summary>정책의 판정. Blocked 는 이벤트를 끈 것, Unrestricted 는 걸린 정책이 없어 지금처럼 보내는 것이다.
/// TargetsRoles 가 참이면 RoleIds 가 받는 사람, 거짓이면 거름막이다.
/// EventCode 는 정책이 걸리는 이벤트로 풀린 코드다 — 본인의 이벤트별 설정을 거는 열쇠이고, 정책 밖이면 비어 있다.</summary>
public sealed record NotificationPolicyDecision(
    bool Blocked,
    bool Unrestricted,
    IReadOnlyList<string> RoleIds,
    bool TargetsRoles,
    bool MailsFromPush = false,
    string? EventCode = null)
{
    /// <summary>정책을 못 찾았거나 걸 것이 없을 때의 값.</summary>
    public static readonly NotificationPolicyDecision Free =
        new(Blocked: false, Unrestricted: true, RoleIds: [], TargetsRoles: false);
}

/// <summary>알림 정책을 읽고 쓰고, 발송 직전에 묻는다. 정책 줄이 없는 이벤트는 제한이 없다.
/// 순서는 정책 → 본인 설정 → 구독이고, 역할표는 포털 계정(jsini)만 물을 수 있다.</summary>
public interface INotificationPolicyService
{
    /// <summary>이번 발송에 걸리는 정책. 발송 경로에서 부른다. 이벤트 코드가 없으면 category 를 쓴다.</summary>
    Task<NotificationPolicyDecision> ResolveAsync(
        string? eventCode, string? category, NotificationChannel channel, CancellationToken ct = default);

    /// <summary>그 역할들에 걸린 포털 로그인 아이디 전부.</summary>
    Task<HashSet<string>> GetLoginIdsInRolesAsync(
        IReadOnlyCollection<string> roleIds, CancellationToken ct = default);

    /// <summary>화면이 그릴 이벤트 목록(정책 줄까지 함께).</summary>
    Task<List<NotificationEventDto>> GetEventsAsync(
        string? keyword, bool activeOnly, CancellationToken ct = default);

    /// <summary>고를 수 있는 역할들. 걸린 사람 수를 함께 센다.</summary>
    Task<List<NotificationRoleDto>> GetRolesAsync(CancellationToken ct = default);

    /// <summary>이벤트 하나의 정책을 통째로 바꾼다.</summary>
    Task<NotificationEventDto> SaveAsync(
        string eventCode, SaveNotificationPolicyDto request, string actor, CancellationToken ct = default);

    /// <summary>「지금 이 설정이면 누구에게 가나」.</summary>
    Task<NotificationPolicyPreviewDto?> PreviewAsync(string eventCode, CancellationToken ct = default);
}

/// <inheritdoc cref="INotificationPolicyService" />
public sealed class NotificationPolicyService(
    AppDbContext db,
    IMemoryCache cache,
    ILogger<NotificationPolicyService> logger) : INotificationPolicyService
{
    /// <summary>포털 계정 주인 종류. 역할을 물을 수 있는 유일한 종류다.</summary>
    public const string OwnerTypePortal = "jsini";

    /// <summary>들고 있는 시간. 저장할 때 Drop 으로 바로 버리고, 이 값은 다른 인스턴스가 따라잡는 최대 시간이다.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private const string CacheKey = "notification-policy:snapshot";

    /// <summary>정책 전체를 한 벌 들고 있는 통. 발송 경로에서 DB 를 안 타게 한다.</summary>
    private sealed record Snapshot(
        Dictionary<string, NotificationEventRow> Events,
        ILookup<string, NotificationPolicy> Policies);

    // ── 발송 경로 ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task<NotificationPolicyDecision> ResolveAsync(
        string? eventCode, string? category, NotificationChannel channel, CancellationToken ct = default)
    {
        Snapshot snapshot;

        try
        {
            snapshot = await SnapshotAsync(ct);
        }
        catch (Exception ex)
        {
            // 정책 표를 못 읽으면 보낸다(「아무도 못 받음」으로 다루면 DB 가 흔들릴 때 알림이 통째로 멎는다).
            logger.LogWarning(ex, "알림 정책을 읽지 못해 제한 없이 보냅니다.");
            return NotificationPolicyDecision.Free;
        }

        var row = Find(snapshot, eventCode, category);

        // 표에 없는 코드로 보낸 것이다. 조용히 막지 않는다.
        if (row is null) return NotificationPolicyDecision.Free;

        if (!row.IsActive)
        {
            return new NotificationPolicyDecision(
                Blocked: true, Unrestricted: false, RoleIds: [], TargetsRoles: false);
        }

        // 정책 문을 지나지 않는 이벤트다(설정은 받아 두되 발송은 건드리지 않는다).
        if (!row.Governed) return NotificationPolicyDecision.Free;

        var roleIds = snapshot.Policies[row.Id]
            .Where(p => p.IsActive && !p.IsDeleted)
            .Where(p => channel == NotificationChannel.Push ? p.PushEnabled : p.EmailEnabled)
            .Select(p => p.RoleId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 역할이 안 걸려도 본인 설정은 걸려야 한다 — 「제한 없음」이 「끌 수 없음」이 되면 안 된다.
        if (roleIds.Count == 0) return NotificationPolicyDecision.Free with { EventCode = row.Id };

        return new NotificationPolicyDecision(
            Blocked: false,
            Unrestricted: false,
            RoleIds: roleIds,
            TargetsRoles: string.Equals(
                row.TargetKind, NotificationTargetKinds.Role, StringComparison.OrdinalIgnoreCase),

            // 메일 곁가지의 열쇠다 — 이 값이 참인 이벤트만 푸시 경로가 메일을 함께 낸다.
            MailsFromPush: row.EmailFromPush,
            EventCode: row.Id);
    }

    /// <summary>이벤트 줄을 찾는다: 정확한 줄 → 앞머리 줄 → 구분 줄. 잘게 나눈 이벤트에 줄이 없어도 구분 줄로 막는다.</summary>
    private static NotificationEventRow? Find(Snapshot snapshot, string? eventCode, string? category)
    {
        foreach (var key in new[]
                 {
                     NotificationEvents.Normalize(eventCode),
                     NotificationEvents.RootOf(eventCode),
                     PushCategories.Normalize(category),
                 })
        {
            if (key is not null && snapshot.Events.TryGetValue(key, out var row)) return row;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetLoginIdsInRolesAsync(
        IReadOnlyCollection<string> roleIds, CancellationToken ct = default)
    {
        if (roleIds.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ids = roleIds.ToList();

        var loginIds = await (
            from ra in db.RoleAccounts
            where ids.Contains(ra.RoleId) && !ra.IsDeleted
            join a in db.Accounts on ra.AccountId equals a.Id
            where !a.IsDeleted
            select a.UserId
        ).Distinct().ToListAsync(ct);

        // 대소문자를 가리지 않는다(못 알아보면 받아야 할 사람이 빠진다).
        return loginIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // ── 화면 ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<NotificationEventDto>> GetEventsAsync(
        string? keyword, bool activeOnly, CancellationToken ct = default)
    {
        var events = await db.NotificationEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.OrderNo).ThenBy(e => e.Id)
            .ToListAsync(ct);

        if (activeOnly) events = events.Where(e => e.IsActive).ToList();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var needle = keyword.Trim();
            events = events
                .Where(e => e.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                            || e.Id.Contains(needle, StringComparison.OrdinalIgnoreCase)
                            || (e.Description ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase)
                            || (e.Source ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var codes = events.Select(e => e.Id).ToList();

        var policies = await db.NotificationPolicies
            .AsNoTracking()
            .Where(p => codes.Contains(p.EventCode) && !p.IsDeleted)
            .ToListAsync(ct);

        var roleNames = await RoleNamesAsync(ct);
        var counts = await RoleAccountCountsAsync(ct);
        var categoryNames = await CategoryNamesAsync(ct);

        return events.Select(e =>
        {
            var rows = policies.Where(p => p.EventCode == e.Id).ToList();

            return new NotificationEventDto
            {
                Code = e.Id,
                Name = e.Name,
                Description = e.Description,
                Category = e.Category,
                CategoryName = e.Category is not null && categoryNames.TryGetValue(e.Category, out var cn)
                    ? cn
                    : e.Category,
                Source = e.Source,
                TargetKind = e.TargetKind,
                SupportsPush = e.SupportsPush,
                SupportsEmail = e.SupportsEmail,
                EmailFromPush = e.EmailFromPush,
                Governed = e.Governed,
                PolicyRequired = e.PolicyRequired,
                IsActive = e.IsActive,
                OrderNo = e.OrderNo,
                Policies = [.. rows
                    .OrderBy(p => roleNames.TryGetValue(p.RoleId, out var n) ? n : p.RoleId,
                        StringComparer.CurrentCulture)
                    .Select(p => new NotificationPolicyDto
                    {
                        RoleId = p.RoleId,
                        RoleName = roleNames.TryGetValue(p.RoleId, out var name) ? name : null,
                        PushEnabled = p.PushEnabled,
                        EmailEnabled = p.EmailEnabled,
                        IsActive = p.IsActive,
                        AccountCount = counts.TryGetValue(p.RoleId, out var c) ? c : 0,
                    })],

                // 역할 0개면 아무도 못 받는다(서버가 셈해 내려보내고 화면이 그 줄을 띄운다).
                Unrestricted = !rows.Any(p => p.IsActive && (p.PushEnabled || p.EmailEnabled)),

                UpdatedAt = rows.Count == 0
                    ? null
                    : rows.Max(p => p.UpdatedAt ?? p.CreatedAt),
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<NotificationRoleDto>> GetRolesAsync(CancellationToken ct = default)
    {
        var counts = await RoleAccountCountsAsync(ct);

        var roles = await db.Roles
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.Status == 1)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(ct);

        return [.. roles
            .OrderBy(r => r.Name, StringComparer.CurrentCulture)
            .Select(r => new NotificationRoleDto
            {
                Id = r.Id,
                Name = r.Name,
                AccountCount = counts.TryGetValue(r.Id, out var c) ? c : 0,
            })];
    }

    /// <inheritdoc />
    public async Task<NotificationEventDto> SaveAsync(
        string eventCode, SaveNotificationPolicyDto request, string actor, CancellationToken ct = default)
    {
        var code = NotificationEvents.Normalize(eventCode)
                   ?? throw new InvalidOperationException("이벤트 코드가 없습니다.");

        var row = await db.NotificationEvents
                      .FirstOrDefaultAsync(e => e.Id == code && !e.IsDeleted, ct)
                  ?? throw new InvalidOperationException($"「{code}」 이벤트를 찾을 수 없습니다.");

            // 없는 역할은 받지 않는다(이름이 틀리면 정책이 조용히 아무에게도 안 걸린다).
        var known = (await db.Roles
                .AsNoTracking()
                .Where(r => !r.IsDeleted)
                .Select(r => r.Id)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var wanted = request.Policies
            .Where(p => !string.IsNullOrWhiteSpace(p.RoleId))
            .Select(p => new SaveNotificationPolicyRowDto
            {
                RoleId = p.RoleId.Trim(),
                PushEnabled = p.PushEnabled && row.SupportsPush,
                EmailEnabled = p.EmailEnabled && row.SupportsEmail,
                IsActive = p.IsActive,
            })
            .DistinctBy(p => p.RoleId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wanted.FirstOrDefault(p => !known.Contains(p.RoleId)) is { } unknown)
        {
            throw new InvalidOperationException($"「{unknown.RoleId}」 은 없는 역할입니다.");
        }

            // 길을 하나도 안 켠 줄은 받지 않는다(「제한 없음」과 「다 꺼짐」을 화면에서 가를 수 없게 된다).
        wanted = wanted.Where(p => p.PushEnabled || p.EmailEnabled).ToList();

        row.IsActive = request.IsActive;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = actor;

        var existing = await db.NotificationPolicies
            .Where(p => p.EventCode == code)
            .ToListAsync(ct);

        foreach (var want in wanted)
        {
            var hit = existing.FirstOrDefault(
                p => string.Equals(p.RoleId, want.RoleId, StringComparison.OrdinalIgnoreCase));

            if (hit is null)
            {
                db.NotificationPolicies.Add(new NotificationPolicy
                {
                    EventCode = code,
                    RoleId = want.RoleId,
                    PushEnabled = want.PushEnabled,
                    EmailEnabled = want.EmailEnabled,
                    IsActive = want.IsActive,
                    CreatedBy = actor,
                });
                continue;
            }

            hit.PushEnabled = want.PushEnabled;
            hit.EmailEnabled = want.EmailEnabled;
            hit.IsActive = want.IsActive;
            hit.IsDeleted = false;
            hit.UpdatedAt = DateTime.UtcNow;
            hit.UpdatedBy = actor;
        }

        // 저장은 보낸 것이 전부다(체크를 푼 역할 줄을 남기면 화면과 표가 어긋난다).
        var keep = wanted.Select(p => p.RoleId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var gone in existing.Where(p => !keep.Contains(p.RoleId)))
        {
            db.NotificationPolicies.Remove(gone);
        }

        await db.SaveChangesAsync(ct);

        // 고친 것이 다음 발송부터 걸리도록 캐시를 버린다.
        Drop();

        var list = await GetEventsAsync(keyword: null, activeOnly: false, ct);
        return list.First(e => e.Code == code);
    }

    /// <inheritdoc />
    public async Task<NotificationPolicyPreviewDto?> PreviewAsync(
        string eventCode, CancellationToken ct = default)
    {
        var code = NotificationEvents.Normalize(eventCode);
        if (code is null) return null;

        var row = await db.NotificationEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == code && !e.IsDeleted, ct);

        if (row is null) return null;

        var policies = await db.NotificationPolicies
            .AsNoTracking()
            .Where(p => p.EventCode == code && !p.IsDeleted && p.IsActive)
            .ToListAsync(ct);

        var result = new NotificationPolicyPreviewDto
        {
            EventCode = code,
            EventDisabled = !row.IsActive,
            Unrestricted = !policies.Any(p => p.PushEnabled || p.EmailEnabled),

            // 역할 0개가 뜻이 정반대인 이벤트가 있다(기본 역할을 안 적어 보내면 한 통도 안 나간다 — PolicyRequired).
            PolicyRequired = row.PolicyRequired,
        };

        if (result.EventDisabled || result.Unrestricted) return result;

        var roleIds = policies.Select(p => p.RoleId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var members = await (
            from ra in db.RoleAccounts
            where roleIds.Contains(ra.RoleId) && !ra.IsDeleted
            join a in db.Accounts on ra.AccountId equals a.Id
            where !a.IsDeleted
            select new { ra.RoleId, a.Id, a.UserId, a.UserName, a.RealName }
        ).ToListAsync(ct);

        if (members.Count == 0) return result;

        var accountIds = members.Select(m => m.Id).Distinct().ToList();

        var emails = (await db.AccountProfileDetails
                .AsNoTracking()
                .Where(d => accountIds.Contains(d.AccountId) && !d.IsDeleted && d.DetailType == "Email")
                .OrderByDescending(d => d.IsPrimary)
                .Select(d => new { d.AccountId, d.Content })
                .ToListAsync(ct))
            .GroupBy(d => d.AccountId)
            .ToDictionary(g => g.Key, g => g.First().Content, StringComparer.Ordinal);

        var loginIds = members.Select(m => m.UserId).Distinct(StringComparer.Ordinal).ToList();

        var devices = (await db.PushSubscriptions
                .AsNoTracking()
                .Where(s => s.OwnerType == OwnerTypePortal && loginIds.Contains(s.OwnerKey))
                .Select(s => s.OwnerKey)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var prefs = await db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.OwnerType == OwnerTypePortal && loginIds.Contains(p.OwnerKey))
            .Select(p => new { p.OwnerKey, p.PushEnabled, p.EmailEnabled })
            .ToListAsync(ct);

        var pushOff = prefs.Where(p => !p.PushEnabled).Select(p => p.OwnerKey)
            .ToHashSet(StringComparer.Ordinal);
        var mailOff = prefs.Where(p => !p.EmailEnabled).Select(p => p.OwnerKey)
            .ToHashSet(StringComparer.Ordinal);

        // 「내 알림 설정」에서 이 알림만 끈 사람도 못 받는 사람이다(발송 경로와 같은 셈).
        var eventOff = await db.NotificationUserEventPrefs
            .AsNoTracking()
            .Where(p => p.OwnerType == OwnerTypePortal && p.EventCode == code
                        && !p.IsDeleted && loginIds.Contains(p.OwnerKey))
            .Select(p => new { p.OwnerKey, p.PushEnabled, p.EmailEnabled })
            .ToListAsync(ct);

        pushOff.UnionWith(eventOff.Where(p => !p.PushEnabled).Select(p => p.OwnerKey));
        mailOff.UnionWith(eventOff.Where(p => !p.EmailEnabled).Select(p => p.OwnerKey));

        var roleNames = await RoleNamesAsync(ct);

        var pushRoles = policies.Where(p => p.PushEnabled)
            .Select(p => p.RoleId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mailRoles = policies.Where(p => p.EmailEnabled)
            .Select(p => p.RoleId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        result.Recipients =
        [
            .. members
                .GroupBy(m => m.UserId, StringComparer.Ordinal)
                .Select(g =>
                {
                    var first = g.First();
                    var mine = g.Select(m => m.RoleId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    return new NotificationPolicyRecipientDto
                    {
                        UserId = g.Key,
                        UserName = string.IsNullOrWhiteSpace(first.UserName) ? first.RealName : first.UserName,
                        RoleText = string.Join(" · ", mine.Select(r =>
                            roleNames.TryGetValue(r, out var n) ? n : r)),
                        Email = emails.TryGetValue(first.Id, out var mail) ? mail : null,
                        HasDevice = devices.Contains(g.Key),
                        PushAllowed = mine.Any(pushRoles.Contains),
                        EmailAllowed = mine.Any(mailRoles.Contains),
                        PushOptedOut = pushOff.Contains(g.Key),
                        EmailOptedOut = mailOff.Contains(g.Key),
                    };
                })
                .OrderBy(r => r.UserName ?? r.UserId, StringComparer.CurrentCulture)
        ];

        // 닿는다는 것은 정책 · 본인 설정 · 받을 자리가 다 선 것이다.
        result.PushReachable = result.Recipients.Count(r => r.PushAllowed && !r.PushOptedOut && r.HasDevice);
        result.EmailReachable = result.Recipients.Count(
            r => r.EmailAllowed && !r.EmailOptedOut && !string.IsNullOrWhiteSpace(r.Email));

        return result;
    }

    // ── 안쪽 ────────────────────────────────────────────────

    private async Task<Snapshot> SnapshotAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out Snapshot? hit) && hit is not null) return hit;

        var events = await db.NotificationEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .ToListAsync(ct);

        var policies = await db.NotificationPolicies
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .ToListAsync(ct);

        var snapshot = new Snapshot(
            events.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase),
            policies.ToLookup(p => p.EventCode, StringComparer.OrdinalIgnoreCase));

        cache.Set(CacheKey, snapshot, Ttl);
        return snapshot;
    }

    /// <summary>들고 있던 것을 버린다. 저장한 뒤 바로 부른다.</summary>
    private void Drop() => cache.Remove(CacheKey);

    private async Task<Dictionary<string, string>> RoleNamesAsync(CancellationToken ct)
        => (await db.Roles
                .AsNoTracking()
                .Where(r => !r.IsDeleted)
                .Select(r => new { r.Id, r.Name })
                .ToListAsync(ct))
            .ToDictionary(r => r.Id, r => r.Name, StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, int>> RoleAccountCountsAsync(CancellationToken ct)
        => (await (
                from ra in db.RoleAccounts
                where !ra.IsDeleted
                join a in db.Accounts on ra.AccountId equals a.Id
                where !a.IsDeleted
                select new { ra.RoleId, a.UserId }
            ).Distinct().ToListAsync(ct))
            .GroupBy(x => x.RoleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    /// <summary>알림구분의 사람이 읽는 이름. 공통코드가 정본이고, 못 찾으면 코드값을 그대로 쓴다.</summary>
    private async Task<Dictionary<string, string>> CategoryNamesAsync(CancellationToken ct)
    {
        try
        {
            var rows = await db.Database
                .SqlQuery<CategoryNameRow>($"""
                    SELECT c.code_value AS "CodeValue", c.code_name AS "CodeName"
                      FROM scom.common_codes c
                      JOIN scom.common_code_groups g ON g.id = c.group_id
                     WHERE g.group_code = {PushCategories.GroupCode}
                       AND NOT c.is_deleted
                    """)
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.CodeValue, r => r.CodeName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            // 이름이 없어도 화면은 선다(코드값이 그대로 보일 뿐이다).
            logger.LogWarning(ex, "알림구분 이름을 읽지 못했습니다.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>공통코드 조회 결과 한 줄. 엔티티로 올리지 않으려고 둔다.</summary>
    private sealed record CategoryNameRow(string CodeValue, string CodeName);
}
