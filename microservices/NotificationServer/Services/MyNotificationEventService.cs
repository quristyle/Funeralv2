using JSini.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Entities;

namespace NotificationServer.Services;

/// <summary>「내 알림 설정」의 이벤트 목록 — 내가 받을 수 있는 알림과 길마다의 본인 설정.
/// 받을 수 있는지는 알림관리(정책)가 정하고, 여기서는 그 안에서 끄기만 한다.</summary>
public interface IMyNotificationEventService
{
    Task<List<MyNotificationEventDto>> GetAsync(string loginId, CancellationToken ct = default);

    /// <summary>이벤트 하나를 저장한다. 정책이 안 걸리거나 없는 이벤트면 <c>null</c>.</summary>
    Task<MyNotificationEventDto?> SaveAsync(
        string loginId, string eventCode, SaveMyNotificationEventDto request, CancellationToken ct = default);
}

/// <inheritdoc cref="IMyNotificationEventService" />
public sealed class MyNotificationEventService(
    AppDbContext db,
    INotificationPreferenceService preferences) : IMyNotificationEventService
{
    private const string OwnerTypePortal = "jsini";

    public async Task<List<MyNotificationEventDto>> GetAsync(string loginId, CancellationToken ct = default)
    {
        var events = await db.NotificationEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.IsActive && e.Governed)
            .OrderBy(e => e.OrderNo)
            .ToListAsync(ct);

        var codes = events.Select(e => e.Id).ToList();

        var policies = await db.NotificationPolicies
            .AsNoTracking()
            .Where(p => codes.Contains(p.EventCode) && !p.IsDeleted && p.IsActive)
            .ToListAsync(ct);

        var myRoles = (await MyRolesAsync(loginId, ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mine = await db.NotificationUserEventPrefs
            .AsNoTracking()
            .Where(p => p.OwnerType == OwnerTypePortal && p.OwnerKey == loginId && !p.IsDeleted)
            .ToListAsync(ct);

        var pref = await preferences.GetAsync(OwnerTypePortal, loginId, ct);

        var result = new List<MyNotificationEventDto>();

        foreach (var e in events)
        {
            var rows = policies
                .Where(p => string.Equals(p.EventCode, e.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var dto = new MyNotificationEventDto
            {
                Code = e.Id,
                Name = e.Name,
                Description = e.Description,
                TargetKind = e.TargetKind,
                PushAllowed = e.SupportsPush && Allowed(e, rows.Where(p => p.PushEnabled), myRoles, email: false),
                EmailAllowed = e.SupportsEmail && Allowed(e, rows.Where(p => p.EmailEnabled), myRoles, email: true),
            };

            // 받을 길이 하나도 없는 알림은 목록에 안 둔다 — 켜도 안 오는 스위치는 「고장」으로 읽힌다.
            if (!dto.PushAllowed && !dto.EmailAllowed) continue;

            var row = mine.FirstOrDefault(p => string.Equals(p.EventCode, e.Id, StringComparison.OrdinalIgnoreCase));

            dto.PushEnabled = row?.PushEnabled ?? true;
            dto.EmailEnabled = row?.EmailEnabled ?? true;

            // 이 둘은 원래 있던 스위치가 정본이다(보내는 쪽이 그 칸을 읽는다). 같은 뜻의 칸을 둘 두면 한쪽만 껐을 때 엇갈린다.
            if (Is(e.Id, NotificationEvents.HelpDeskComment))
            {
                dto.PushEnabled = pref.CommentPushEnabled;
                dto.EmailEnabled = pref.CommentEmailEnabled;
            }
            else if (Is(e.Id, NotificationEvents.Note))
            {
                dto.EmailEnabled = pref.NoteEmailEnabled;
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<MyNotificationEventDto?> SaveAsync(
        string loginId, string eventCode, SaveMyNotificationEventDto request, CancellationToken ct = default)
    {
        var e = await db.NotificationEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == eventCode && !x.IsDeleted && x.Governed, ct);

        if (e is null) return null;

        var push = request.PushEnabled;
        var email = request.EmailEnabled;

        // 원래 스위치가 정본인 칸은 그쪽에 적는다(GetAsync 와 같은 갈래).
        if (Is(e.Id, NotificationEvents.HelpDeskComment))
        {
            await preferences.SaveAsync(OwnerTypePortal, loginId, new UpdateNotificationPreferenceDto
            {
                CommentPushEnabled = push,
                CommentEmailEnabled = email,
            }, loginId, ct);

            push = null;
            email = null;
        }
        else if (Is(e.Id, NotificationEvents.Note) && email is not null)
        {
            await preferences.SaveAsync(OwnerTypePortal, loginId, new UpdateNotificationPreferenceDto
            {
                NoteEmailEnabled = email,
            }, loginId, ct);

            email = null;
        }

        if (push is not null || email is not null)
        {
            var row = await db.NotificationUserEventPrefs
                .FirstOrDefaultAsync(p => p.OwnerType == OwnerTypePortal
                                          && p.OwnerKey == loginId
                                          && p.EventCode == e.Id, ct);

            if (row is null)
            {
                row = new NotificationUserEventPref
                {
                    OwnerType = OwnerTypePortal,
                    OwnerKey = loginId,
                    EventCode = e.Id,
                    CreatedBy = loginId,
                };
                db.NotificationUserEventPrefs.Add(row);
            }
            else
            {
                row.IsDeleted = false;
                row.UpdatedAt = DateTime.UtcNow;
                row.UpdatedBy = loginId;
            }

            if (push is { } p) row.PushEnabled = p;
            if (email is { } m) row.EmailEnabled = m;

            await db.SaveChangesAsync(ct);
        }

        return (await GetAsync(loginId, ct))
            .FirstOrDefault(x => string.Equals(x.Code, e.Id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>정책으로 보아 이 사람이 이 길로 받을 수 있나. 걸린 역할이 없을 때는 발송 경로(PushSender · EmailEndpoints)의 「제한 없음」을 따른다 —
    /// 나를 짚어 오는 알림은 그대로 오고, 역할로 오는 알림은 보내는 쪽이 고른 역할이라 여기서는 알 수 없어 뺀다. 푸시가 함께 내는 메일은 역할이 없으면 안 나간다.</summary>
    private static bool Allowed(
        NotificationEventRow e, IEnumerable<NotificationPolicy> rows, HashSet<string> myRoles, bool email)
    {
        var roleIds = rows.Select(p => p.RoleId).ToList();

        if (roleIds.Count > 0) return roleIds.Any(myRoles.Contains);

        if (e.PolicyRequired) return false;

        if (!string.Equals(e.TargetKind, NotificationTargetKinds.User, StringComparison.OrdinalIgnoreCase)) return false;

        return !email || !e.EmailFromPush;
    }

    private async Task<List<string>> MyRolesAsync(string loginId, CancellationToken ct)
    {
        var key = loginId.ToLower();

        return await (
            from ra in db.RoleAccounts
            where !ra.IsDeleted
            join a in db.Accounts on ra.AccountId equals a.Id
            where !a.IsDeleted && a.UserId.ToLower() == key
            select ra.RoleId
        ).Distinct().ToListAsync(ct);
    }

    private static bool Is(string code, string expected)
        => string.Equals(code, expected, StringComparison.OrdinalIgnoreCase);
}
