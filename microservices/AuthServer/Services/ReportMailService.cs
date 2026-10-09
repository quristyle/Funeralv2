using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using JSini.Shared.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 보고서 메일 배치의 조회·저장과 한 건 보내기.
/// </summary>
/// <remarks>
/// <para>
/// [고를 수 있는 보고서를 코드에 적지 않는다]
/// </para>
///
/// <para>
/// 목록은 <c>scom.system_menus</c> 에서 읽는다(<see cref="ReportKeyPrefix"/>).
/// 운영 리포트 화면이 하나 늘면 이 화면의 목록도 그날부터 함께 는다 —
/// 코드에 여덟 개를 적어 두면 아홉 번째가 생기는 날 아무도 여기를 안 고친다.
/// </para>
///
/// <para>
/// [받는 사람을 푸는 규칙은 <b>알림 서비스와 같아야 한다</b>]
/// </para>
///
/// <para>
/// 실제로 보내는 쪽은 NotificationServer 의 <c>/emails/send</c> 이고, 그쪽은
/// 역할을 <c>scom.role_accounts</c> → 계정 → <c>Email</c> 속성으로 푼다
/// (<c>EmailEndpoints.ResolveRoleEmailsAsync</c>). 미리보기가 다른 규칙으로
/// 세면 화면이 「네 명에게 갑니다」라고 말해 놓고 두 명에게 가는 일이 생긴다.
/// <b>그래서 여기서도 같은 표를 같은 차례로 본다.</b> 회사·부서에 걸린 역할은
/// 둘 다 보지 않는다 — 그쪽을 세려면 보내는 쪽부터 함께 고쳐야 한다.
/// </para>
///
/// <para>
/// [<c>created_by</c> · <c>updated_by</c> 를 여기서 적지 않는다]
/// </para>
///
/// <para>
/// <see cref="AppDbContext.SaveChangesAsync"/> 의 <c>HandleAuditing</c> 이
/// 저장 직전에 <b>덮어쓴다</b> — 로그인 주체(없으면 <c>System</c>)로. 그래서
/// 여기서 사람 이름을 넣어 봐야 사라지고, 넣어 둔 줄은 「되는 줄 알았는데
/// 안 되는」 코드가 된다. 누가 했는지는 <b>로그와
/// <see cref="ReportMailSchedule.LastResult"/></b> 가 들고 있다.
/// </para>
///
/// <para>
/// 다만 <b>본인이 꺼 둔 메일 갈래</b>(<c>scom.notification_preferences</c>)는
/// 보내는 쪽만 본다. 그 표는 알림 서비스의 것이고 여기서 읽으면 같은 설정을
/// 두 서비스가 알게 된다 — 미리보기에 그 한 줄을 적어 둔다.
/// </para>
/// </remarks>
public class ReportMailService(
    AppDbContext db,
    ReportMailSender sender,
    ILogger<ReportMailService> logger) : IReportMailService
{
    /// <summary>
    /// 보고서로 고를 수 있는 화면의 열쇠 앞머리.
    /// </summary>
    /// <remarks>
    /// 헬프데스크의 <b>운영 리포트</b> 묶음이다(<c>/helpdesk/report/*</c>).
    /// 묶음 식별자(<c>HD_RPT</c>)로 묶지 않는 까닭은 메뉴를 옮기면 그 값이
    /// 바뀌기 때문이고, 열쇠는 「한 번 정하면 안 바꾸는 값」이다
    /// (<c>web/CLAUDE.md</c>).
    /// </remarks>
    private const string ReportKeyPrefix = "helpdesk.report.";

    /// <summary>
    /// 묶음 밖에 있지만 같은 종류인 화면. 지금은 「SM 모니터링」 하나다.
    /// </summary>
    private static readonly string[] ExtraReportKeys = ["helpdesk.monitor.sm"];

    /// <inheritdoc />
    public async Task<List<ReportCatalogItemDto>> GetCatalogAsync(CancellationToken ct = default)
    {
        var rows = await db.SystemMenus
            .AsNoTracking()
            .Where(m => !m.IsDeleted
                && m.Status == 1
                && m.RouteKey != null
                && (m.RouteKey.StartsWith(ReportKeyPrefix) || ExtraReportKeys.Contains(m.RouteKey)))
            .OrderBy(m => m.OrderNo)
            .ThenBy(m => m.Name)
            .Select(m => new ReportCatalogItemDto
            {
                RouteKey = m.RouteKey!,
                MenuId = m.Id,
                Title = m.Title ?? m.Name,
                Path = m.Path,
                Icon = m.Icon,
            })
            .ToListAsync(ct);

        return rows;
    }

    /// <inheritdoc />
    public async Task<List<ReportMailScheduleDto>> GetSchedulesAsync(
        string? keyword, bool activeOnly, CancellationToken ct = default)
    {
        var query = db.Set<ReportMailSchedule>()
            .AsNoTracking()
            .Include(s => s.Reports)
            .Include(s => s.Roles)
            .Where(s => !s.IsDeleted);

        if (activeOnly)
        {
            query = query.Where(s => s.IsActive);
        }

        var trimmed = keyword?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, $"%{trimmed}%")
                || (s.Remark != null && EF.Functions.ILike(s.Remark, $"%{trimmed}%")));
        }

        var rows = await query
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        return await ToDtosAsync(rows, ct);
    }

    /// <inheritdoc />
    public async Task<ReportMailScheduleDto?> GetScheduleAsync(string id, CancellationToken ct = default)
    {
        var row = await db.Set<ReportMailSchedule>()
            .AsNoTracking()
            .Include(s => s.Reports)
            .Include(s => s.Roles)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

        if (row is null) return null;

        return (await ToDtosAsync([row], ct)).FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<ReportMailScheduleDto> CreateAsync(
        SaveReportMailScheduleDto request, string actor, CancellationToken ct = default)
    {
        Validate(request);

        var entity = new ReportMailSchedule { Id = Guid.NewGuid().ToString("N") };

        Apply(entity, request);

        db.Add(entity);
        ReplaceChildren(entity, request);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "보고서 메일 배치 등록: {Name} ({Id}) — 보고서 {Reports}건 · 역할 {Roles}개, {Cycle}",
            entity.Name, entity.Id, request.ReportKeys.Count, request.RoleIds.Count,
            ReportMailSchedulePlan.Describe(entity));

        return await GetScheduleAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("방금 만든 배치를 다시 읽지 못했습니다.");
    }

    /// <inheritdoc />
    public async Task<ReportMailScheduleDto?> UpdateAsync(
        string id, SaveReportMailScheduleDto request, string actor, CancellationToken ct = default)
    {
        Validate(request);

        var entity = await db.Set<ReportMailSchedule>()
            .Include(s => s.Reports)
            .Include(s => s.Roles)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

        if (entity is null) return null;

        Apply(entity, request);
        ReplaceChildren(entity, request);

        await db.SaveChangesAsync(ct);

        return await GetScheduleAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, string actor, CancellationToken ct = default)
    {
        var entity = await db.Set<ReportMailSchedule>()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

        if (entity is null) return false;

        // **지운 표시만 한다.** 자식 줄은 그대로 둔다 — 되살릴 일이 있을 때
        // 고른 보고서·역할이 함께 돌아와야 하고, 발송기는 부모의 깃발만 본다.
        entity.IsDeleted = true;
        entity.IsActive = false;

        await db.SaveChangesAsync(ct);

        // 누가 지웠는지는 로그에만 남는다 — 감사 칸은 DbContext 가 덮어쓴다(머리말).
        logger.LogInformation(
            "보고서 메일 배치 삭제: {Name} ({Id}) — 요청: {Actor}", entity.Name, entity.Id, actor);

        return true;
    }

    /// <inheritdoc />
    public async Task<ReportMailRecipientsDto?> GetRecipientsAsync(string id, CancellationToken ct = default)
    {
        var roleIds = await db.Set<ReportMailScheduleRole>()
            .AsNoTracking()
            .Where(r => r.ScheduleId == id && !r.IsDeleted)
            .Select(r => r.RoleId)
            .ToListAsync(ct);

        var exists = await db.Set<ReportMailSchedule>()
            .AnyAsync(s => s.Id == id && !s.IsDeleted, ct);

        if (!exists) return null;

        return await ResolveRecipientsAsync(roleIds, ct);
    }

    /// <summary>
    /// 역할들에 걸린 사람과 그 대표 이메일. 미리보기와 「지금 보내기」가 함께 쓴다.
    /// </summary>
    internal async Task<ReportMailRecipientsDto> ResolveRecipientsAsync(
        IReadOnlyCollection<string> roleIds, CancellationToken ct)
    {
        var result = new ReportMailRecipientsDto();

        if (roleIds.Count == 0) return result;

        var names = await db.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var links = await (
            from ra in db.RoleAccounts.AsNoTracking()
            where roleIds.Contains(ra.RoleId) && !ra.IsDeleted
            join a in db.Accounts.AsNoTracking() on ra.AccountId equals a.Id
            where !a.IsDeleted
            select new { a.Id, a.UserId, a.UserName, ra.RoleId })
            .ToListAsync(ct);

        if (links.Count == 0) return result;

        var accountIds = links.Select(l => l.Id).Distinct().ToList();

        // 대표 이메일. 같은 계정에 여러 줄이 있으면 is_primary 를 먼저 본다 —
        // 알림 서비스가 고르는 차례와 같다.
        var emails = (await db.AccountProfileDetails
                .AsNoTracking()
                .Where(d => accountIds.Contains(d.AccountId)
                    && d.DetailType == "Email"
                    && !d.IsDeleted
                    && d.Content != "")
                .Select(d => new { d.AccountId, d.Content, d.IsPrimary })
                .ToListAsync(ct))
            .GroupBy(d => d.AccountId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.IsPrimary).First().Content.Trim());

        result.Recipients =
        [
            .. links
                .GroupBy(l => l.Id)
                .Select(g => new ReportMailRecipientDto
                {
                    UserId = g.First().UserId,
                    UserName = g.First().UserName,
                    RoleNames = [.. g.Select(x => names.TryGetValue(x.RoleId, out var n) ? n : x.RoleId).Distinct()],
                    Email = emails.TryGetValue(g.Key, out var mail) ? mail : null,
                })
                .OrderBy(r => r.UserName ?? r.UserId, StringComparer.CurrentCulture),
        ];

        result.DeliverableCount = result.Recipients.Count(r => !string.IsNullOrWhiteSpace(r.Email));

        return result;
    }

    /// <summary>
    /// 지금 한 번 보낸다.
    /// </summary>
    /// <remarks>
    /// <b><c>LastSentAt</c> 을 찍지 않는다.</b> 그 값은 「주기의 이 칸에 이미
    /// 보냈나」를 가리는 데 쓰이므로, 사람이 눌러 보낸 한 통 때문에 오늘 아침
    /// 정기 발송이 건너뛰어지면 안 된다. 대신
    /// <see cref="ReportMailSchedule.LastResult"/> 에는 적는다 — 눌러 본 결과를
    /// 화면이 그 자리에서 읽을 수 있어야 하기 때문이다.
    /// </remarks>
    public async Task<(bool Ok, string Message)> SendNowAsync(
        string id, string actor, CancellationToken ct = default)
    {
        var entity = await db.Set<ReportMailSchedule>()
            .Include(s => s.Reports)
            .Include(s => s.Roles)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

        if (entity is null) return (false, "배치를 찾을 수 없습니다.");

        var catalog = await GetCatalogAsync(ct);
        var outcome = await sender.SendAsync(entity, catalog, actor, ct);

        entity.LastResult = outcome.Message;
        await db.SaveChangesAsync(ct);

        return (outcome.Ok, outcome.Message);
    }

    /// <summary>
    /// 「미리받아보기」 — 지금 고른 보고서를 부른 사람 본인에게 한 통 보낸다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [<b>DB 를 한 칸도 건드리지 않는다</b>]
    /// </para>
    /// <para>
    /// 「지금 보내기」는 적어도 <see cref="ReportMailSchedule.LastResult"/> 에
    /// 자국을 남긴다 — 그 한 통이 <b>남에게</b> 갔기 때문이다. 이쪽은 누른
    /// 사람 본인에게만 가므로 배치의 기록에 남길 것이 없다. 남기면 「마지막
    /// 결과」가 아무도 못 받은 미리보기로 덮여, 정작 어젯밤 정기 발송이
    /// 실패했다는 줄이 사라진다.
    /// </para>
    /// <para>
    /// 그래서 <b>저장하지 않은 배치</b>로도 부를 수 있다. 받는 역할을 정하기
    /// 전에 메일 꼴부터 보는 것이 가장 흔한 차례다.
    /// </para>
    /// </remarks>
    public async Task<(bool Ok, string Message, ReportMailPreviewResultDto? Result)> SendPreviewAsync(
        ReportMailPreviewDto request, string actor, CancellationToken ct = default)
    {
        var keys = (request.ReportKeys ?? [])
            .Select(k => k?.Trim())
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (keys.Count == 0)
        {
            return (false, "미리 받아 볼 보고서를 하나 이상 고르십시오.", null);
        }

        var email = await FindOwnEmailAsync(actor, ct);

        if (string.IsNullOrWhiteSpace(email))
        {
            // **막는 자리를 짚어 준다.** 「보내지 못했습니다」만 띄우면 메일
            // 서버를 보러 가지만, 실제로 할 일은 내 계정에 주소를 넣는 것이다.
            logger.LogWarning(
                "보고서 메일 미리받아보기: 로그인 계정에 이메일이 없다 ({Actor}).", actor);

            return (false,
                "로그인한 계정에 이메일이 없습니다. 「계정 관리」에서 본인 이메일을 넣으십시오.",
                null);
        }

        // 저장하지 않는 임시 줄. 메일 본문이 보는 것(이름·주기·머리말·보고서)만
        // 채운다 — db 에 더하지 않으므로 식별자도 필요 없다.
        var draft = new ReportMailSchedule
        {
            Id = string.Empty,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "미리받아보기" : request.Name.Trim(),
            Frequency = ReportMailFrequency.IsKnown(request.Frequency)
                ? request.Frequency!
                : ReportMailFrequency.Daily,
            DayOfWeek = request.DayOfWeek,
            DayOfMonth = request.DayOfMonth,
            SendHourKst = Math.Clamp(request.SendHourKst, 0, 23),
            SendMinuteKst = Math.Clamp(request.SendMinuteKst, 0, 59),
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            Reports = [.. keys.Select(k => new ReportMailScheduleReport { ReportKey = k! })],
            Roles = [],
        };

        var catalog = await GetCatalogAsync(ct);
        var (ok, message, count, missing) = await sender.SendPreviewAsync(draft, catalog, email!, actor, ct);

        if (!ok) return (false, message, null);

        return (true, message, new ReportMailPreviewResultDto
        {
            Email = email!,
            ReportCount = count,
            MissingReportKeys = missing,
            Message = message,
        });
    }

    // ── 안쪽 ────────────────────────────────────────────────

    /// <summary>
    /// 로그인한 사람의 대표 이메일. 없으면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 계정을 <c>user_id</c> 로도 <c>id</c> 로도 찾는다 — 게이트웨이가 실어
    /// 보내는 <c>X-User-Id</c> 가 어느 쪽인지는 토큰을 만든 자리에 달려 있고,
    /// 이 서버의 다른 조회들도 둘을 함께 본다(<c>UserService</c>).
    /// </para>
    /// <para>
    /// 주소를 고르는 차례는 <see cref="ResolveRecipientsAsync"/> 와 <b>같아야
    /// 한다</b> — 미리받아보기가 다른 주소로 가면 「받는 사람」 창이 보여 준
    /// 것과 실제로 받은 메일이 어긋난다. 프로필 상세는 값을 고칠 때 옛 줄을
    /// 지움 표시만 하고 새 줄을 더하는 자리라, 지운 것을 안 거르면 <b>옛
    /// 주소</b>로 간다.
    /// </para>
    /// </remarks>
    private async Task<string?> FindOwnEmailAsync(string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor)) return null;

        var accountId = await db.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted && (a.UserId == actor || a.Id == actor))
            .Select(a => a.Id)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(accountId)) return null;

        var mails = await db.AccountProfileDetails
            .AsNoTracking()
            .Where(d => d.AccountId == accountId
                && d.DetailType == "Email"
                && !d.IsDeleted
                && d.Content != "")
            .Select(d => new { d.Content, d.IsPrimary })
            .ToListAsync(ct);

        return mails
            .OrderByDescending(m => m.IsPrimary)
            .Select(m => m.Content.Trim())
            .FirstOrDefault(m => m.Length > 0);
    }

    /// <summary>
    /// 받은 값이 쓸 수 있는 것인가. <b>여기서 막지 않으면 조용히 안 보내는
    /// 배치가 생긴다</b> — 보고서를 하나도 안 고른 배치가 그렇다.
    /// </summary>
    private static void Validate(SaveReportMailScheduleDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("배치 이름을 적어야 합니다.");
        }

        if (!ReportMailFrequency.IsKnown(request.Frequency))
        {
            throw new InvalidOperationException("주기를 고르십시오.");
        }

        if (request.ReportKeys.Count == 0)
        {
            throw new InvalidOperationException("보낼 보고서를 하나 이상 고르십시오.");
        }

        if (request.RoleIds.Count == 0)
        {
            throw new InvalidOperationException("받을 권한 역할을 하나 이상 고르십시오.");
        }

        if (request.Frequency == ReportMailFrequency.Weekly
            && request.DayOfWeek is not (>= 1 and <= 7))
        {
            throw new InvalidOperationException("주마다 보내려면 요일을 고르십시오.");
        }

        if (request.Frequency == ReportMailFrequency.Monthly
            && request.DayOfMonth is not (>= 1 and <= 31))
        {
            throw new InvalidOperationException("달마다 보내려면 날짜를 고르십시오.");
        }
    }

    /// <summary>받은 값을 줄에 옮긴다. 주기에 안 쓰는 칸은 비워 둔다.</summary>
    private static void Apply(ReportMailSchedule entity, SaveReportMailScheduleDto request)
    {
        entity.Name = request.Name!.Trim();
        entity.Frequency = request.Frequency!;
        entity.SendHourKst = Math.Clamp(request.SendHourKst, 0, 23);
        entity.SendMinuteKst = Math.Clamp(request.SendMinuteKst, 0, 59);
        entity.IsActive = request.IsActive;
        entity.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();

        // **안 쓰는 칸은 비운다.** 주마다에서 달마다로 바꾼 배치에 옛 요일이
        // 남아 있으면 DB 를 들여다본 사람이 어느 쪽이 사는 값인지 알 수 없다.
        entity.DayOfWeek = request.Frequency == ReportMailFrequency.Weekly ? request.DayOfWeek : null;
        entity.DayOfMonth = request.Frequency == ReportMailFrequency.Monthly ? request.DayOfMonth : null;
    }

    /// <summary>
    /// 고른 보고서·역할을 <b>통째로 갈아 끼운다.</b>
    /// </summary>
    /// <remarks>
    /// 더한 것과 뺀 것을 가려 내지 않는다 — 화면이 보내는 것이 「지금 고른
    /// 전부」라서, 차이를 셈하는 코드는 같은 결과를 더 어렵게 얻는 길이다.
    /// </remarks>
    private void ReplaceChildren(ReportMailSchedule entity, SaveReportMailScheduleDto request)
    {
        if (entity.Reports is { Count: > 0 }) db.RemoveRange(entity.Reports);
        if (entity.Roles is { Count: > 0 }) db.RemoveRange(entity.Roles);

        entity.Reports =
        [
            .. request.ReportKeys
                .Select(k => k?.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct(StringComparer.Ordinal)
                .Select(k => new ReportMailScheduleReport
                {
                    ScheduleId = entity.Id,
                    ReportKey = k!,
                }),
        ];

        entity.Roles =
        [
            .. request.RoleIds
                .Select(r => r?.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .Distinct(StringComparer.Ordinal)
                .Select(r => new ReportMailScheduleRole
                {
                    ScheduleId = entity.Id,
                    RoleId = r!,
                }),
        ];

        db.AddRange(entity.Reports);
        db.AddRange(entity.Roles);
    }

    /// <summary>줄들을 화면이 받는 꼴로. 보고서 제목과 역할 이름을 함께 채운다.</summary>
    private async Task<List<ReportMailScheduleDto>> ToDtosAsync(
        List<ReportMailSchedule> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var catalog = (await GetCatalogAsync(ct)).ToDictionary(c => c.RouteKey, c => c.Title, StringComparer.Ordinal);

        var roleIds = rows.SelectMany(r => r.Roles ?? []).Select(r => r.RoleId).Distinct().ToList();

        var roleNames = await db.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var now = AppTime.UtcNow;

        return
        [
            .. rows.Select(s =>
            {
                var keys = (s.Reports ?? []).Select(r => r.ReportKey).ToList();

                return new ReportMailScheduleDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Frequency = s.Frequency,
                    DayOfWeek = s.DayOfWeek,
                    DayOfMonth = s.DayOfMonth,
                    SendHourKst = s.SendHourKst,
                    SendMinuteKst = s.SendMinuteKst,
                    IsActive = s.IsActive,
                    Remark = s.Remark,
                    LastSentAt = s.LastSentAt,
                    LastResult = s.LastResult,
                    NextRunAt = ReportMailSchedulePlan.NextRunUtc(s, now),
                    ReportKeys = keys,
                    ReportTitles = [.. keys.Where(catalog.ContainsKey).Select(k => catalog[k])],
                    MissingReportKeys = [.. keys.Where(k => !catalog.ContainsKey(k))],
                    RoleIds = [.. (s.Roles ?? []).Select(r => r.RoleId)],
                    RoleNames =
                    [
                        .. (s.Roles ?? [])
                            .Select(r => roleNames.TryGetValue(r.RoleId, out var n) ? n : r.RoleId),
                    ],
                };
            }),
        ];
    }
}
