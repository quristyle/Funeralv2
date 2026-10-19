using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using JSini.Shared.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 차단 접근기록 — 쌓는 쪽과 보는 쪽.
/// </summary>
/// 쌓는 길은 RecordAsync 하나, 보는 길은 셋(화면별 · 사람별 · 기록)이다 — 같은 자료에 묻는 물음이 다르기 때문이다.
/// 새 화면이 메뉴에도 없고 [ScreenAccess] 신고도 없으면 아무 말 없이 안 열리는데, 그 사실이 드러나는 자리가 여기뿐이다.
public interface IBlockedAccessService
{
    /// <summary>차단 한 건을 적는다. 적을 것이 못 되면 <c>false</c>.</summary>
    Task<bool> RecordAsync(string loginId, BlockedAccessRecordDto record, CancellationToken ct = default);

    /// <summary>기록 목록. 늦은 것부터고 서버가 <paramref name="take"/> 줄에서 자른다.</summary>
    Task<List<BlockedAccessDto>> GetLogsAsync(
        DateOnly? from, DateOnly? to, string? userId, string? reason, string? keyword, int take,
        CancellationToken ct = default);

    /// <summary>화면별 집계. 많이 막힌 화면부터다.</summary>
    Task<List<BlockedAccessByScreenDto>> GetByScreenAsync(
        DateOnly? from, DateOnly? to, string? userId, string? reason, string? keyword,
        CancellationToken ct = default);

    /// <summary>사람별 집계. 많이 막힌 사람부터다.</summary>
    Task<List<BlockedAccessByUserDto>> GetByUserAsync(
        DateOnly? from, DateOnly? to, string? reason, string? keyword,
        CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class BlockedAccessService(
    AppDbContext db,
    ILogger<BlockedAccessService> logger) : IBlockedAccessService
{
    /// <summary>
    /// 얼마나 오래 들고 있을지. 넘은 줄은 적을 때 하루 한 번 걷어낸다.
    /// </summary>
    /// 메뉴 사용기록(menu_usage_logs)과 같은 값이어야 한다 — 보관 기간이 다르면 같은 날의 두 기록을 대조할 수 없게 된다.
    private const int RetentionDays = 365;

    /// <summary>마지막으로 오래된 줄을 걷어낸 때(UTC).</summary>
    private static DateTime _lastSweepUtc = DateTime.MinValue;

    /// <summary>적어 두는 까닭. 모르는 값이 오면 이것으로 떨어뜨린다.</summary>
    private const string ReasonNoPermission = "NO_PERMISSION";
    private const string ReasonUnmanaged = "UNMANAGED";

    /// <inheritdoc />
    public async Task<bool> RecordAsync(
        string loginId, BlockedAccessRecordDto record, CancellationToken ct = default)
    {
        var href = Cut(record.Href?.Trim(), 512);

        // 주소가 없으면 무엇을 막은 것인지 알 수 없다. 받아 두면 영영 안 읽힐
        // 줄만 쌓인다(`MenuUsageService.RecordAsync` 와 같은 자리).
        if (string.IsNullOrEmpty(href) || string.IsNullOrWhiteSpace(loginId))
        {
            return false;
        }

        db.Set<BlockedAccessLog>().Add(new BlockedAccessLog
        {
            OccurredAt = AppTime.UtcNow,
            UserId = Cut(loginId, 128)!,
            Href = href,
            RouteKey = Cut(record.RouteKey?.Trim(), 128),
            ScreenType = Cut(record.ScreenType?.Trim(), 256),
            MenuPath = Cut(record.MenuPath?.Trim(), 512),

            // **모르는 값을 그대로 적지 않는다.** 까닭 칸으로 조회를 거르므로
            // 오타 하나가 「어느 묶음에도 안 드는 줄」을 만든다.
            Reason = record.Reason == ReasonNoPermission ? ReasonNoPermission : ReasonUnmanaged,

            FromPath = Cut(record.FromPath?.Trim(), 512),
            ClientIp = Cut(record.ClientIp?.Trim(), 64),
        });

        await db.SaveChangesAsync(ct);
        await SweepAsync(ct);

        return true;
    }

    /// <inheritdoc />
    public async Task<List<BlockedAccessDto>> GetLogsAsync(
        DateOnly? from, DateOnly? to, string? userId, string? reason, string? keyword, int take,
        CancellationToken ct = default)
    {
        var rows = await Filtered(from, to, userId, reason, keyword)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new BlockedAccessDto
            {
                Id = x.Id,
                OccurredAt = x.OccurredAt,
                UserId = x.UserId,
                Href = x.Href,
                RouteKey = x.RouteKey,
                ScreenType = x.ScreenType,
                MenuPath = x.MenuPath,
                Reason = x.Reason,
                FromPath = x.FromPath,
                ClientIp = x.ClientIp,
            })
            .ToListAsync(ct);

        var titles = await TitleMapAsync(
            rows.Select(r => r.MenuPath), rows.Select(r => r.RouteKey), ct);

        foreach (var row in rows)
        {
            row.MenuTitle = titles.Title(row.MenuPath, row.RouteKey);
        }

        var who = await WhoMapAsync(rows.Select(r => r.UserId), ct);

        foreach (var row in rows)
        {
            var (name, dept) = who.GetValueOrDefault(row.UserId);
            row.UserName = name;
            row.DepartmentName = dept;
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<List<BlockedAccessByScreenDto>> GetByScreenAsync(
        DateOnly? from, DateOnly? to, string? userId, string? reason, string? keyword,
        CancellationToken ct = default)
    {
        // **묶는 열쇠가 주소가 아니다**(DTO 머리말). 열쇠가 없는 화면만 주소로 떨어진다.
        var rows = await Filtered(from, to, userId, reason, keyword)
            .GroupBy(x => x.RouteKey ?? x.Href)
            .Select(g => new
            {
                ScreenKey = g.Key,
                Blocks = g.Count(),
                Users = g.Select(x => x.UserId).Distinct().Count(),
                LastBlockedAt = g.Max(x => x.OccurredAt),
                RouteKey = g.Max(x => x.RouteKey),
                MenuPath = g.Max(x => x.MenuPath),
            })
            .ToListAsync(ct);

        if (rows.Count == 0) return [];

        // 까닭과 마지막 주소는 **줄 단위**로 따로 읽는다. 한 화면에 둘이 섞일 수
        // 있어서(권한을 준 뒤에도 지난 줄이 남는다) 많은 쪽을 골라야 한다.
        var detail = await Filtered(from, to, userId, reason, keyword)
            .GroupBy(x => new { Key = x.RouteKey ?? x.Href, x.Reason })
            .Select(g => new
            {
                g.Key.Key,
                g.Key.Reason,
                Count = g.Count(),
                LastAt = g.Max(x => x.OccurredAt),
            })
            .ToListAsync(ct);

        var lastHref = await Filtered(from, to, userId, reason, keyword)
            .GroupBy(x => x.RouteKey ?? x.Href)
            .Select(g => new
            {
                Key = g.Key,
                Href = g.OrderByDescending(x => x.OccurredAt).Select(x => x.Href).First(),
            })
            .ToListAsync(ct);

        var topReason = detail
            .GroupBy(d => d.Key, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.Count).ThenByDescending(d => d.LastAt).First().Reason,
                StringComparer.Ordinal);

        var hrefs = lastHref.ToDictionary(x => x.Key, x => x.Href, StringComparer.Ordinal);

        var titles = await TitleMapAsync(
            rows.Select(r => r.MenuPath), rows.Select(r => r.RouteKey), ct);

        return [.. rows
            .Select(r => new BlockedAccessByScreenDto
            {
                ScreenKey = r.ScreenKey,
                RouteKey = r.RouteKey,
                MenuPath = r.MenuPath,
                MenuTitle = titles.Title(r.MenuPath, r.RouteKey),
                LastHref = hrefs.GetValueOrDefault(r.ScreenKey),
                Reason = topReason.GetValueOrDefault(r.ScreenKey),
                Blocks = r.Blocks,
                Users = r.Users,
                LastBlockedAt = r.LastBlockedAt,
            })
            .OrderByDescending(r => r.Blocks)
            .ThenByDescending(r => r.LastBlockedAt)];
    }

    /// <inheritdoc />
    public async Task<List<BlockedAccessByUserDto>> GetByUserAsync(
        DateOnly? from, DateOnly? to, string? reason, string? keyword,
        CancellationToken ct = default)
    {
        var query = Filtered(from, to, null, reason, keyword);

        var totals = await query
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Blocks = g.Count(),
                Screens = g.Select(x => x.RouteKey ?? x.Href).Distinct().Count(),
                FirstBlockedAt = g.Min(x => x.OccurredAt),
                LastBlockedAt = g.Max(x => x.OccurredAt),
            })
            .ToListAsync(ct);

        if (totals.Count == 0) return [];

        // 사람마다 가장 많이 막힌 화면. DB 가 (사람, 화면)까지 줄여 준다.
        var perScreen = await query
            .GroupBy(x => new { x.UserId, Key = x.RouteKey ?? x.Href })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Key,
                MenuPath = g.Max(x => x.MenuPath),
                RouteKey = g.Max(x => x.RouteKey),
                Count = g.Count(),
                LastAt = g.Max(x => x.OccurredAt),
            })
            .ToListAsync(ct);

        var titles = await TitleMapAsync(
            perScreen.Select(p => p.MenuPath), perScreen.Select(p => p.RouteKey), ct);

        var top = perScreen
            .GroupBy(p => p.UserId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.Count).ThenByDescending(p => p.LastAt).First(),
                StringComparer.Ordinal);

        var result = totals
            .Select(t => new BlockedAccessByUserDto
            {
                UserId = t.UserId,
                Blocks = t.Blocks,
                Screens = t.Screens,

                // 제목을 못 찾으면 **열쇠(또는 주소)를 그대로 보여 준다.** null 로
                // 두면 화면이 「–」을 그려, 옆 칸의 횟수와 어긋나 보인다.
                TopScreen = top.TryGetValue(t.UserId, out var s)
                    ? titles.Title(s.MenuPath, s.RouteKey) ?? s.Key
                    : null,
                TopScreenBlocks = top.TryGetValue(t.UserId, out var s2) ? s2.Count : 0,
                FirstBlockedAt = t.FirstBlockedAt,
                LastBlockedAt = t.LastBlockedAt,
            })
            .OrderByDescending(r => r.Blocks)
            .ThenByDescending(r => r.LastBlockedAt)
            .ToList();

        var who = await WhoMapAsync(result.Select(r => r.UserId), ct);

        foreach (var row in result)
        {
            var (name, dept) = who.GetValueOrDefault(row.UserId);
            row.UserName = name;
            row.DepartmentName = dept;
        }

        return result;
    }

    /// <summary>
    /// 기간·사람·까닭·검색어로 좁힌 줄들.
    /// </summary>
    /// 세 길이 같은 조건을 써야 한다 — 갈라 적으면 집계와 목록의 건수가 어긋난다.
    private IQueryable<BlockedAccessLog> Filtered(
        DateOnly? from, DateOnly? to, string? userId, string? reason, string? keyword)
    {
        var query = db.Set<BlockedAccessLog>().AsNoTracking().AsQueryable();

        // 화면이 보내는 것은 **사람이 고른 한국 달력의 날짜**다. 저장값은 UTC 라
        // 그대로 비교하면 하루가 아홉 시간 어긋난다(docs/utc-time.md).
        if (from is { } f)
        {
            var start = AppTime.StartOfDayUtc(f);
            query = query.Where(x => x.OccurredAt >= start);
        }

        if (to is { } t)
        {
            var end = AppTime.StartOfDayUtc(t.AddDays(1));
            query = query.Where(x => x.OccurredAt < end);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(x => x.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            query = query.Where(x => x.Reason == reason);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Href, $"%{k}%")
                || EF.Functions.ILike(x.RouteKey ?? "", $"%{k}%")
                || EF.Functions.ILike(x.MenuPath ?? "", $"%{k}%")
                || EF.Functions.ILike(x.ScreenType ?? "", $"%{k}%"));
        }

        return query;
    }

    /// <summary>
    /// 경로·열쇠 → <b>지금의</b> 메뉴 제목.
    /// </summary>
    /// 열쇠로도 찾는다 — 관리 밖으로 막힌 줄은 경로가 비어 있고, 나중에 메뉴가 생긴 화면의 지난 줄도 이름을 얻어야 한다.
    private async Task<MenuTitleLookup> TitleMapAsync(
        IEnumerable<string?> paths, IEnumerable<string?> routeKeys, CancellationToken ct)
    {
        var pathKeys = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var keyKeys = routeKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (pathKeys.Length == 0 && keyKeys.Length == 0)
        {
            return new MenuTitleLookup([], []);
        }

        var rows = await db.SystemMenus
            .AsNoTracking()
            .Where(m => !m.IsDeleted
                && (pathKeys.Contains(m.Path)
                    || (m.RouteKey != null && keyKeys.Contains(m.RouteKey))))
            .ToListAsync(ct);

        // **번역 키를 그대로 내보내지 않는다.** 저장된 제목 179건 중 열몇은
        // 키다(`menu.account_management`). 그대로 주면 화면에 그 글자가 뜨고,
        // 사이드바에서는 「계정 관리」로 보이는 것이 여기서만 다른 이름이 된다
        // (`MenuTitleTranslator` 머리말이 말하는 그 갈라짐이다).
        var i18n = await MenuTitleTranslator.LoadAsync(db, rows, null);

        var byPath = new Dictionary<string, string?>(StringComparer.Ordinal);
        var byKey = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var title = MenuTitleTranslator.Display(row, i18n);

            if (!string.IsNullOrWhiteSpace(row.Path))
            {
                byPath[row.Path] = title;
            }

            if (!string.IsNullOrWhiteSpace(row.RouteKey))
            {
                byKey[row.RouteKey!] = title;
            }
        }

        return new MenuTitleLookup(byPath, byKey);
    }

    /// <summary>
    /// 로그인 아이디 → 이름·부서. <b>못 찾는 아이디가 있는 것이 정상이다</b> —
    /// 계정을 지워도 그 사람이 막힌 기록은 남는다.
    /// </summary>
    private async Task<Dictionary<string, (string? Name, string? Dept)>> WhoMapAsync(
        IEnumerable<string> loginIds, CancellationToken ct)
    {
        var ids = loginIds.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return [];

        var rows = await db.Accounts
            .AsNoTracking()
            .Where(a => ids.Contains(a.UserId))
            .Select(a => new
            {
                a.UserId,
                a.UserName,
                DeptName = a.Department != null ? a.Department.Name : null,
            })
            .ToListAsync(ct);

        var map = new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            map[row.UserId] = (row.UserName, row.DeptName);
        }

        return map;
    }

    /// <summary>
    /// 오래된 줄을 걷어낸다. 하루 한 번만 실제로 돈다.
    /// </summary>
    /// 실패해도 삼킨다 — 청소가 안 돼서 방금 막힌 줄을 못 적으면 본말이 뒤집힌다.
    private async Task SweepAsync(CancellationToken ct)
    {
        var now = AppTime.UtcNow;
        if (now - _lastSweepUtc < TimeSpan.FromDays(1)) return;
        _lastSweepUtc = now;

        try
        {
            await db.Set<BlockedAccessLog>()
                .Where(x => x.OccurredAt < now.AddDays(-RetentionDays))
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "오래된 차단 기록을 걷어내지 못했습니다.");
        }
    }

    private static string? Cut(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }

    /// <summary>경로나 열쇠로 지금 제목을 찾는다. 못 찾으면 <c>null</c>.</summary>
    private sealed class MenuTitleLookup(
        Dictionary<string, string?> byPath, Dictionary<string, string?> byKey)
    {
        public string? Title(string? path, string? routeKey)
        {
            if (!string.IsNullOrWhiteSpace(path) && byPath.TryGetValue(path!, out var byP))
            {
                return byP;
            }

            return !string.IsNullOrWhiteSpace(routeKey) && byKey.TryGetValue(routeKey!, out var byK)
                ? byK
                : null;
        }
    }
}
