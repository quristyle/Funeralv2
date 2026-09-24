using Microsoft.EntityFrameworkCore;

using NotificationServer.Data;
using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>
/// 사람이 적어 넣은 값으로 <b>받는 사람</b>을 찾는다 — 로그인 아이디든 이메일이든.
/// </summary>
public interface INoteRecipientResolver
{
    /// <summary>
    /// 적어 넣은 값들을 푼다.
    /// </summary>
    /// <returns>
    /// 찾은 사람들과, <b>못 찾은 값 그대로</b>. 뒤엣것을 조용히 버리지 않는 것이
    /// 이 메서드의 요점이다 — 「열 명에게 보냈는데 여덟만 받았다」를 그 자리에서
    /// 말할 수 있어야 한다.
    ///
    /// <para>
    /// 찾은 사람 중에도 <b>쪽지를 받을 수 없는 사람</b>이 있다
    /// (<see cref="NoteRecipientDto.CanReceive"/> 가 거짓 — 앱 푸시도 쪽지 메일도
    /// 닿지 않는다). 여기서 빼지 않고 표시만 해 준다 — 「없는 아이디」와 「받을
    /// 길이 없는 사람」은 보내는 쪽이 할 일이 서로 달라서다.
    /// </para>
    /// </returns>
    Task<(List<NoteRecipientDto> Found, List<string> Unknown)> ResolveAsync(
        IEnumerable<string> tokens, CancellationToken ct = default);

    /// <summary>
    /// 아이디 · 이름 · 이메일 어느 것으로 쳐도 걸리는 찾기. 보내기 전에 화면이
    /// 「이 사람이 맞나」를 확인하는 자리에 쓴다. <b>쪽지를 받을 길이 있는 사람만</b>
    /// 돌려준다(<see cref="NoteRecipientDto.CanReceive"/>).
    /// </summary>
    Task<List<NoteRecipientDto>> SearchAsync(string? query, int take, CancellationToken ct = default);
}

/// <inheritdoc cref="INoteRecipientResolver" />
/// <remarks>
/// <para>
/// <b>왜 알림 서비스가 계정을 뒤지는가.</b> <c>scom</c> 은 이 서비스가 원래 접속하는
/// DB 이고(<c>Data/AppDbContext</c> 머리말), 이미 같은 이유로 이메일과 프로필 사진을
/// 여기서 푼다(<c>EmailEndpoints.ResolveUserEmailsAsync</c> · <c>AvatarIconResolver</c>).
/// 쪽지를 받을 사람을 푸는 것도 같은 자리다 — 쓰지는 않고 읽기만 한다.
/// </para>
///
/// <para>
/// [<b>화면이 푼 결과를 믿지 않는다</b>]
/// </para>
///
/// <para>
/// 찾기 API 가 있다고 해서 보내기가 그 결과를 받아 쓰면, 브라우저에서 아이디를
/// 갈아 끼워 <b>아무에게나 남의 이름으로 쪽지를 보낼 수</b> 있게 된다. 보내기도
/// 사람이 적은 글자에서 다시 푼다 — 같은 규칙이므로 결과는 늘 같다.
/// </para>
/// </remarks>
public sealed class NoteRecipientResolver(AppDbContext db, INotificationPreferenceService prefs)
    : INoteRecipientResolver
{
    /// <summary>
    /// 한 번에 풀 수 있는 값의 수. 넘으면 앞에서 자른다.
    /// </summary>
    /// <remarks>
    /// 쪽지는 「아는 몇 사람에게 한 통」이다. 수백 명에게 한꺼번에 보내야 하면
    /// 그 화면은 포털관리의 「메시지 발송」이다.
    /// </remarks>
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

        // 아이디로 걸리는 것과 이메일로 걸리는 것을 **따로 묻고 합친다.**
        // 하나의 질의로 묶으면 이메일 쪽 조인 때문에 메일을 등록하지 않은
        // 계정이 아이디로도 안 걸린다.
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
    public async Task<List<NoteRecipientDto>> SearchAsync(
        string? query, int take, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        var limit = Math.Clamp(take, 1, 50);

        if (q.Length == 0)
        {
            // 조건 없이 전 직원을 흘려보내지 않는다. 화면은 이 빈 목록을 보고
            // 「두 글자 이상 치십시오」를 말한다.
            return [];
        }

        var lowered = q.ToLowerInvariant();

        // 이메일로 친 것도 걸리게 한다 — 사람이 아는 것이 주소뿐인 경우가 있다.
        var emailAccountIds = await db.AccountProfileDetails
            .Where(d => d.DetailType == "Email" && !d.IsDeleted
                        && d.Content.ToLower().Contains(lowered))
            .Select(d => d.AccountId)
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

        // **쪽지를 받을 길이 있는 사람만 찾는다 (2026-09-24).** 앱 푸시(기기가
        // 있고 끄지 않았다) 또는 쪽지 메일(켜 두었고 주소가 있다) 중 하나는 있어야
        // 한다 — 둘 다 없으면 쪽지함에만 쌓이고 본인은 왔다는 것조차 모른다.
        //
        // 예전에는 푸시를 끈 사람만 회색으로 잠그고 나머지는 다 보였다. 그런데
        // 사람 대부분이 **기기를 등록한 적이 없어서**(설정 행이 없으면 「켜짐」이다)
        // 거의 모두가 고를 수 있게 보였고, 보내면 두드림이 한 군데도 안 갔다.
        //
        // 거르는 일은 **질의 안에서** 한다. 읽은 뒤에 거르면 `take` 가 먼저
        // 잘라서, 받을 수 있는 사람이 뒤에 있으면 목록이 비어 보인다.
        // 판정식은 `LoadAsync` 가 매기는 `PushReachable` · `EmailReachable` 과 같다.
        var rows = await LoadAsync(
            a => (a.UserId.ToLower().Contains(lowered)
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

    /// <summary>
    /// 계정 · 부서 · 대표 이메일을 한 번에 읽어 <see cref="NoteRecipientDto"/> 로 만든다.
    /// </summary>
    /// <remarks>
    /// 이메일은 <b>바깥 조인</b>이다. 안쪽으로 묶으면 메일을 등록하지 않은 계정이
    /// 목록에서 통째로 사라지는데, 그런 사람에게도 <b>쪽지는 보낼 수 있다</b> —
    /// 메일만 못 보낼 뿐이다. 화면이 그것을 말해 주려면 줄이 있어야 한다.
    /// </remarks>
    private async Task<List<NoteRecipientDto>> LoadAsync(
        System.Linq.Expressions.Expression<Func<Entities.AccountRow, bool>> where,
        CancellationToken ct,
        int? take = null)
    {
        var query = db.Accounts.Where(a => !a.IsDeleted).Where(where);

        if (take is { } n)
        {
            query = query.Take(n);
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

        // **푸시를 끈 사람은 쪽지를 받지 못한다.** 판정은 설정 서비스가 한다 —
        // 「행이 없으면 켜짐」이라는 규칙이 거기 한 곳에만 있어야 새지 않는다
        // (`NotificationPreferenceService.GetPushDisabledAsync` 머리말).
        var pushOff = await prefs.GetPushDisabledAsync(
            [.. accounts.Select(a => new OwnerRefDto { OwnerType = "jsini", OwnerKey = a.UserId })],
            ct);

        // **기기가 있어야 앱 푸시가 닿는다.** 설정이 「켜짐」이어도 구독이 없으면
        // 보낼 곳이 없다. 죽은 구독은 발송 때 지워지므로(`PushSender`) 남은 줄은
        // 살아 있는 기기로 본다.
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
