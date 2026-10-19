using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>사람이 적어 넣은 값(로그인 아이디 · 이메일)으로 받는 사람을 찾는다.</summary>
public interface INoteRecipientResolver
{
    /// <summary>적어 넣은 값들을 푼다. 못 찾은 값은 버리지 않고 돌려준다(몇 명에게 갔는지 말하려고).</summary>
    /// <returns>찾은 사람들과 못 찾은 값. CanReceive 가 거짓인 사람도 빼지 않고 표시한다.</returns>
    Task<(List<NoteRecipientDto> Found, List<string> Unknown)> ResolveAsync(
        IEnumerable<string> tokens, CancellationToken ct = default);

    /// <summary>아이디 · 이름 · 이메일 어느 것으로 쳐도 걸리는 찾기. 쪽지를 받을 길이 있는 사람만 돌려준다.</summary>
    Task<List<NoteRecipientDto>> SearchAsync(string? query, int take, CancellationToken ct = default);

    /// <summary>로그인 아이디로 곧바로 읽는다. 자르지 않고 쪽지 가능 여부를 덧붙인다. 못 찾은 아이디는 돌려주지 않는다.</summary>
    Task<List<NoteRecipientDto>> LoadByLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default);

    /// <summary>쪽지를 받을 수 있는 사람 전부를 이름순으로. 거르는 잣대는 SearchAsync 와 같아야 한다.</summary>
    Task<List<NoteRecipientDto>> ListReachableAsync(int take, CancellationToken ct = default);
}

/// <inheritdoc cref="INoteRecipientResolver" />
/// 보내기는 화면이 푼 결과를 믿지 않고 사람이 적은 글자에서 다시 푼다.
public sealed class NoteRecipientResolver(AppDbContext db, INotificationPreferenceService prefs)
    : INoteRecipientResolver
{
    /// <summary>한 번에 풀 수 있는 값의 수. 넘으면 앞에서 자른다. 쪽지는 아는 몇 사람에게 한 통이다.</summary>
    public const int MaxRecipients = 30;

    /// <inheritdoc />
    public async Task<(List<NoteRecipientDto> Found, List<string> Unknown)> ResolveAsync(
        IEnumerable<string> tokens, CancellationToken ct = default)
    {
        var wanted = tokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecipients)
            .ToList();

        if (wanted.Count == 0)
        {
            return ([], []);
        }

        // 아이디로 걸리는 것과 이메일로 걸리는 것을 따로 묻고 합친다 — 한 질의로 묶으면 메일 없는 계정이 아이디로도 안 걸린다.
        var byId = await LoadAsync(a => wanted.Contains(a.UserId), ct);

        var lowered = wanted.Select(w => w.ToLowerInvariant()).ToList();

        var emailAccountIds = await db.AccountProfileDetails
            .Where(d => d.DetailType == "Email" && !d.IsDeleted && d.Content != ""
                        && lowered.Contains(d.Content.ToLower()))
            .Select(d => d.AccountId)
            .Distinct()
            .ToListAsync(ct);

        var byEmail = emailAccountIds.Count == 0
            ? []
            : await LoadAsync(a => emailAccountIds.Contains(a.Id), ct);

        var found = byId
            .Concat(byEmail)
            .GroupBy(r => r.LoginId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // 못 찾은 것 — 아이디로도 이메일로도 안 걸린 글자.
        var unknown = wanted
            .Where(w => !found.Any(f =>
                string.Equals(f.LoginId, w, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Email, w, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return (found, unknown);
    }

    /// <inheritdoc />
    public Task<List<NoteRecipientDto>> SearchAsync(
        string? query, int take, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();

        if (q.Length == 0)
        {
            // 빈 글자로 전 직원을 흘려보내지 않는다(전체는 ListReachableAsync 로만 받는다).
            return Task.FromResult(new List<NoteRecipientDto>());
        }

        return ReachableAsync(q, Math.Clamp(take, 1, 50), ct);
    }

    /// <inheritdoc />
    public Task<List<NoteRecipientDto>> ListReachableAsync(
        int take, CancellationToken ct = default)
        => ReachableAsync(null, Math.Clamp(take, 1, 500), ct);

    /// <summary>쪽지가 닿는 사람을 이름순으로. query 가 있으면 그 글자로 좁힌다. 찾기와 전체 목록이 이 메서드를 나눠 쓴다.
    /// 거르기는 질의 안에서 한다(읽은 뒤 거르면 take 가 먼저 잘라 빠진다).</summary>
    private async Task<List<NoteRecipientDto>> ReachableAsync(
        string? query, int limit, CancellationToken ct)
    {
            // 글자가 없으면 빈 목록을 준다(null 목록은 EF 번역에서 터지고 null 글자는 조건을 빠뜨린다).
        var lowered = query?.ToLowerInvariant();
        var emailAccountIds = new List<string>();

        if (lowered is not null)
        {
            // 이메일로 친 것도 걸리게 한다 — 사람이 아는 것이 주소뿐인 경우가 있다.
            emailAccountIds = await db.AccountProfileDetails
                .Where(d => d.DetailType == "Email" && !d.IsDeleted
                            && d.Content.ToLower().Contains(lowered))
                .Select(d => d.AccountId)
                .Distinct()
                .Take(limit)
                .ToListAsync(ct);
        }

        var rows = await LoadAsync(
            a => (lowered == null
                  || a.UserId.ToLower().Contains(lowered)
                  || (a.UserName != null && a.UserName.ToLower().Contains(lowered))
                  || emailAccountIds.Contains(a.Id))
                 && ((db.PushSubscriptions.Any(s => s.OwnerType == "jsini" && s.OwnerKey == a.UserId)
                      && !db.NotificationPreferences.Any(p => p.OwnerType == "jsini"
                                                              && p.OwnerKey == a.UserId
                                                              && !p.PushEnabled))
                     || (db.NotificationPreferences.Any(p => p.OwnerType == "jsini"
                                                             && p.OwnerKey == a.UserId
                                                             && p.NoteEmailEnabled)
                         && db.AccountProfileDetails.Any(d => d.AccountId == a.Id
                                                              && d.DetailType == "Email"
                                                              && !d.IsDeleted
                                                              && d.Content != ""))),
            ct,
            limit);

        return [.. rows.OrderBy(r => r.Name ?? r.LoginId, StringComparer.CurrentCulture)];
    }

    /// <inheritdoc />
    public async Task<List<NoteRecipientDto>> LoadByLoginIdsAsync(
        IEnumerable<string> loginIds, CancellationToken ct = default)
    {
        var keys = loginIds
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            return [];
        }

        return await LoadAsync(a => keys.Contains(a.UserId), ct);
    }

    /// <summary>계정·부서·대표 이메일을 한 번에 읽어 NoteRecipientDto 로 만든다. 이메일은 바깥 조인이라 메일 없는 계정도 쪽지는 받는다.</summary>
    private async Task<List<NoteRecipientDto>> LoadAsync(
        System.Linq.Expressions.Expression<Func<Entities.AccountRow, bool>> where,
        CancellationToken ct,
        int? take = null)
    {
        var query = db.Accounts.Where(a => !a.IsDeleted).Where(where);

        if (take is { } n)
        {
            // 자르기 전에 줄을 세운다 — 순서 없이 자르면 같은 질의에 다른 사람이 나온다.
            query = query.OrderBy(a => a.UserId).Take(n);
        }

        var accounts = await query
            .Select(a => new { a.Id, a.UserId, a.UserName, a.RealName, a.DepartmentId })
            .ToListAsync(ct);

        if (accounts.Count == 0)
        {
            return [];
        }

        var ids = accounts.Select(a => a.Id).ToList();

        var emails = await db.AccountProfileDetails
            .Where(d => ids.Contains(d.AccountId) && d.DetailType == "Email"
                        && !d.IsDeleted && d.Content != "")
            .Select(d => new { d.AccountId, d.Content, d.IsPrimary })
            .ToListAsync(ct);

        // 대표가 유일하지 않다(이메일을 풀 때와 같은 사정). 대표를 먼저 고른다.
        var emailOf = emails
            .GroupBy(e => e.AccountId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(e => e.IsPrimary).First().Content.Trim());

        var deptIds = accounts
            .Select(a => a.DepartmentId)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .ToList()!;

        var deptOf = deptIds.Count == 0
            ? []
            : await db.Departments
                .Where(d => deptIds.Contains(d.Id) && !d.IsDeleted)
                .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        // 푸시를 끈 사람은 쪽지를 받지 못한다 — 판정은 설정 서비스가 한다(행이 없으면 켜짐).
        var pushOff = await prefs.GetPushDisabledAsync(
            [.. accounts.Select(a => new OwnerRefDto { OwnerType = "jsini", OwnerKey = a.UserId })],
            ct);

        // 기기가 있어야 앱 푸시가 닿는다 — 죽은 구독은 발송 때 지워지므로 남은 줄은 살아 있는 기기다.
        var loginIds = accounts.Select(a => a.UserId).Distinct().ToList();

        var withDevice = (await db.PushSubscriptions
                .Where(s => s.OwnerType == "jsini" && loginIds.Contains(s.OwnerKey))
                .Select(s => s.OwnerKey)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        // 쪽지 메일은 받는 사람이 켠 경우에만 간다(기본 꺼짐).
        var noteMailOn = await prefs.GetNoteEmailEnabledLoginIdsAsync(loginIds, ct);

        return [.. accounts.Select(a =>
        {
            var email = emailOf.GetValueOrDefault(a.Id);
            var pushEnabled = !pushOff.Contains(("jsini", a.UserId));

            return new NoteRecipientDto
            {
                LoginId = a.UserId,
                Name = Pick(a.UserName, a.RealName, a.UserId),
                Affiliation = a.DepartmentId is { Length: > 0 } dept && deptOf.TryGetValue(dept, out var name)
                    ? name
                    : null,
                Email = email,
                PushEnabled = pushEnabled,
                PushReachable = pushEnabled && withDevice.Contains(a.UserId),
                EmailReachable = noteMailOn.Contains(a.UserId) && !string.IsNullOrWhiteSpace(email),
            };
        })];
    }

    /// <summary>비어 있지 않은 첫 글자.</summary>
    private static string Pick(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? string.Empty;
}
