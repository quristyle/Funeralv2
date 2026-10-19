using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using JSini.Shared.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>메뉴 사용기록 — 쌓는 길은 하나, 보는 길은 넷이다. 집계는 DB 가 한다.</summary>
public interface IMenuUsageService
{
    /// <summary>열람 한 건을 적는다. 적을 것이 못 되면 <c>false</c> 를 돌려준다.</summary>
    Task<bool> RecordAsync(string loginId, MenuUsageRecordDto record, CancellationToken ct = default);

    /// <summary>기록 목록. 늦은 것부터고 서버가 <paramref name="take"/> 줄에서 자른다.</summary>
    Task<List<MenuUsageDto>> GetLogsAsync(
        DateOnly? from, DateOnly? to, string? userId, string? keyword, int take,
        CancellationToken ct = default);

    /// <summary>사람별 집계. 많이 본 사람부터다.</summary>
    Task<List<MenuUsageByUserDto>> GetByUserAsync(
        DateOnly? from, DateOnly? to, string? keyword, CancellationToken ct = default);

    /// <summary>화면별 집계. 많이 열린 화면부터다.</summary>
    Task<List<MenuUsageByMenuDto>> GetByMenuAsync(
        DateOnly? from, DateOnly? to, string? userId, string? keyword, CancellationToken ct = default);

    /// <summary>일자별 타임라인. 늦은 날부터고 하루 안은 시간순이다.</summary>
    Task<List<MenuUsageTimelineDayDto>> GetTimelineAsync(
        DateOnly? from, DateOnly? to, string? userId, string? keyword, int take,
        CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class MenuUsageService(
    AppDbContext db,
    IAccountActivityService activity,
    ILogger<MenuUsageService> logger) : IMenuUsageService
{
    /// <summary>한 해 보관한다. 넘은 줄은 적을 때 하루 한 번 걷어낸다.</summary>
    private const int RetentionDays = 365;

    /// <summary>마지막으로 오래된 줄을 걷어낸 때(UTC). 적는 길에 얹어 두고 별도 배경 작업은 두지 않는다.</summary>
    private static DateTime _lastSweepUtc = DateTime.MinValue;

    /// <inheritdoc />
    public async Task<bool> RecordAsync(
        string loginId, MenuUsageRecordDto record, CancellationToken ct = default)
    {
        var path = Cut(record.MenuPath?.Trim(), 512);

        // 경로가 없으면 무엇을 본 것인지 알 수 없다. 받아 두면 영영 안 읽힐
        // 줄만 쌓인다 — 포털 오류 기록이 추적 번호 없는 보고를 버리는 자리와 같다.
        if (string.IsNullOrEmpty(path) || string.IsNullOrWhiteSpace(loginId))
        {
            return false;
        }

        var routeKey = Cut(record.RouteKey?.Trim(), 128);

        // 메뉴 식별자는 **여기서 찾는다.** 보내는 쪽(포털 셸)이 아는 것은 경로와
        // 열쇠뿐이고(메뉴 조회 응답에 식별자가 없다), 몸체로 받으면 아무 값이나
        // 적힌다. 못 찾아도 적는다 — 메뉴에서 빠진 화면의 기록도 기록이다.
        var menuId = await db.SystemMenus
            .AsNoTracking()
            .Where(m => !m.IsDeleted
                && ((routeKey != null && m.RouteKey == routeKey) || m.Path == path))
            .Select(m => m.Id)
            .FirstOrDefaultAsync(ct);

        db.Set<MenuUsageLog>().Add(new MenuUsageLog
        {
            OccurredAt = AppTime.UtcNow,
            UserId = Cut(loginId, 128)!,
            MenuId = menuId,
            RouteKey = routeKey,
            MenuPath = path,
            MenuTitle = Cut(record.MenuTitle?.Trim(), 256),
            Href = Cut(record.Href?.Trim(), 512),
            FromPath = Cut(record.FromPath?.Trim(), 512),
        });

        await db.SaveChangesAsync(ct);

        // 화면 이동이 「쓰고 있다」의 신호다. 포털은 라우팅만 바뀌어 서버로 요청이 안 나가므로 이 길이 유일한 자리다. 갱신은 포털이 1분 간격으로 거른다.
        await activity.TouchAsync(loginId, ct);

        await SweepAsync(ct);

        return true;
    }

    /// <inheritdoc />
    public async Task<List<MenuUsageDto>> GetLogsAsync(
        DateOnly? from, DateOnly? to, string? userId, string? keyword, int take,
        CancellationToken ct = default)
    {
        var rows = await Filtered(from, to, userId, keyword)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new MenuUsageDto
            {
                Id = x.Id,
                OccurredAt = x.OccurredAt,
                UserId = x.UserId,
                MenuPath = x.MenuPath,
                RouteKey = x.RouteKey,
                MenuTitle = x.MenuTitle,
                Href = x.Href,
                FromPath = x.FromPath,
            })
            .ToListAsync(ct);

        await FillWhoAsync(rows.Select(r => r.UserId), (id, name, dept) =>
        {
            foreach (var row in rows.Where(r => r.UserId == id))
            {
                row.UserName = name;
                row.DepartmentName = dept;
            }
        }, ct);

        return rows;
    }

    /// <inheritdoc />
    public async Task<List<MenuUsageByUserDto>> GetByUserAsync(
        DateOnly? from, DateOnly? to, string? keyword, CancellationToken ct = default)
    {
        var query = Filtered(from, to, null, keyword);

        var totals = await query
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Views = g.Count(),

                // `COUNT(DISTINCT menu_path)` 로 간다. 건수만으로는 「한 화면을
                // 백 번 새로고친 사람」과 「백 개를 돌아본 사람」이 같아 보인다.
                Screens = g.Select(x => x.MenuPath).Distinct().Count(),

                FirstViewAt = g.Min(x => x.OccurredAt),
                LastViewAt = g.Max(x => x.OccurredAt),
            })
            .ToListAsync(ct);

        if (totals.Count == 0) return [];

        // 사람마다 가장 많이 본 화면. **DB 가 (사람, 화면)까지 줄여 준다** —
        // 줄 수가 사람 수 × 화면 수라 회로로 옮겨도 가볍다.
        var perMenu = await query
            .GroupBy(x => new { x.UserId, x.MenuPath })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.MenuPath,
                Views = g.Count(),
                LastViewAt = g.Max(x => x.OccurredAt),
            })
            .ToListAsync(ct);

        var days = await DayCountsAsync(query, ct);
        var titles = await TitleMapAsync(perMenu.Select(p => p.MenuPath), ct);

        var top = perMenu
            .GroupBy(p => p.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.Views).ThenByDescending(p => p.LastViewAt).First(),
                StringComparer.Ordinal);

        var result = totals
            .Select(t => new MenuUsageByUserDto
            {
                UserId = t.UserId,
                Views = t.Views,
                Screens = t.Screens,
                Days = days.GetValueOrDefault(t.UserId),
                // 메뉴가 지워져 제목을 못 찾으면 **경로를 그대로 보여 준다.**
                // null 로 두면 화면이 「–」을 그려, 가장 많이 본 화면이 없는
                // 것처럼 보인다(옆 칸의 횟수와 어긋난다).
                TopMenuTitle = top.TryGetValue(t.UserId, out var m)
                    ? titles.Title(m.MenuPath) ?? m.MenuPath
                    : null,
                TopMenuViews = top.TryGetValue(t.UserId, out var m2) ? m2.Views : 0,
                FirstViewAt = t.FirstViewAt,
                LastViewAt = t.LastViewAt,
            })
            .OrderByDescending(r => r.Views)
            .ThenByDescending(r => r.LastViewAt)
            .ToList();

        await FillWhoAsync(result.Select(r => r.UserId), (id, name, dept) =>
        {
            foreach (var row in result.Where(r => r.UserId == id))
            {
                row.UserName = name;
                row.DepartmentName = dept;
            }
        }, ct);

        return result;
    }

    /// <inheritdoc />
    public async Task<List<MenuUsageByMenuDto>> GetByMenuAsync(
        DateOnly? from, DateOnly? to, string? userId, string? keyword,
        CancellationToken ct = default)
    {
        var rows = await Filtered(from, to, userId, keyword)
            .GroupBy(x => x.MenuPath)
            .Select(g => new
            {
                MenuPath = g.Key,
                Views = g.Count(),
                Users = g.Select(x => x.UserId).Distinct().Count(),
                LastViewAt = g.Max(x => x.OccurredAt),

                // 메뉴가 지워졌을 때만 쓰는 대비책이다. 같은 경로에 서로 다른
                // 제목이 쌓여 있으면 그중 하나가 나온다 — 살아 있는 메뉴는
                // 아래에서 지금 제목으로 덮어쓰므로 거기서는 보이지 않는다.
                SnapshotTitle = g.Max(x => x.MenuTitle),
                RouteKey = g.Max(x => x.RouteKey),
            })
            .ToListAsync(ct);

        var titles = await TitleMapAsync(rows.Select(r => r.MenuPath), ct);

        return [.. rows
            .Select(r => new MenuUsageByMenuDto
            {
                MenuPath = r.MenuPath,
                RouteKey = r.RouteKey,
                MenuTitle = titles.Title(r.MenuPath) ?? r.SnapshotTitle,
                Views = r.Views,
                Users = r.Users,
                LastViewAt = r.LastViewAt,
            })
            .OrderByDescending(r => r.Views)
            .ThenByDescending(r => r.LastViewAt)];
    }

    /// <inheritdoc />
    public async Task<List<MenuUsageTimelineDayDto>> GetTimelineAsync(
        DateOnly? from, DateOnly? to, string? userId, string? keyword, int take,
        CancellationToken ct = default)
    {
        // 타임라인은 집계가 아니라 **줄 그대로**다. 묶는 일은 아래에서 하되
        // 가져오는 줄 수는 서버가 자른다 — 사람을 안 고르면 하루에도 수천 줄이다.
        var rows = await Filtered(from, to, userId, keyword)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new
            {
                x.OccurredAt,
                x.UserId,
                x.MenuPath,
                x.RouteKey,
                x.MenuTitle,
                x.Href,
            })
            .ToListAsync(ct);

        if (rows.Count == 0) return [];

        var titles = await TitleMapAsync(rows.Select(r => r.MenuPath), ct);
        var who = await WhoMapAsync(rows.Select(r => r.UserId), ct);

        // **하루의 경계는 한국 달력이다.** UTC 자정으로 끊으면 한국의 하루가
        // 아침 아홉 시에 갈려, 오전에 본 화면이 전날 칸으로 밀린다.
        var days = rows
            .GroupBy(r => DateOnly.FromDateTime(AppTime.ToKorea(r.OccurredAt)))
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.OccurredAt).ToList();

                var items = ordered
                    .Select((r, i) => new MenuUsageTimelineItemDto
                    {
                        Seq = i + 1,
                        OccurredAt = r.OccurredAt,
                        MenuPath = r.MenuPath,
                        RouteKey = r.RouteKey,
                        MenuTitle = titles.Title(r.MenuPath) ?? r.MenuTitle,
                        Href = r.Href,
                        UserId = r.UserId,
                        UserName = who.GetValueOrDefault(r.UserId).Name,

                        // 앞 칸과의 사이. **머문 시간이 아니다**(DTO 머리말).
                        GapMinutes = i == 0
                            ? 0
                            : (int)Math.Round((r.OccurredAt - ordered[i - 1].OccurredAt).TotalMinutes),
                    })
                    .ToList();

                return new MenuUsageTimelineDayDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Views = items.Count,
                    Screens = ordered.Select(r => r.MenuPath).Distinct(StringComparer.Ordinal).Count(),
                    FirstAt = ordered[0].OccurredAt,
                    LastAt = ordered[^1].OccurredAt,
                    Items = items,
                };
            })
            .ToList();

        return days;
    }

    /// <summary>기간·사람·검색어로 좁힌 줄들. 네 길이 같은 조건을 써야 집계와 목록의 건수가 맞는다.</summary>
    private IQueryable<MenuUsageLog> Filtered(
        DateOnly? from, DateOnly? to, string? userId, string? keyword)
    {
        var query = db.Set<MenuUsageLog>().AsNoTracking().AsQueryable();

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

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.MenuTitle ?? "", $"%{k}%")
                || EF.Functions.ILike(x.MenuPath, $"%{k}%")
                || EF.Functions.ILike(x.RouteKey ?? "", $"%{k}%"));
        }

        return query;
    }

    /// <summary>한국 표준시의 치우침(시간). 한국은 서머타임이 없어 늘 이 값이다.</summary>
    /// AppTime 을 쓸 수 없는 자리다 — 날 자르기를 DB 에 시켜야 해서 TimeZoneInfo 가 SQL 로 번역되지 않는다.
    private const int KoreaOffsetHours = 9;

    /// <summary>사람마다 기록이 있는 날의 수. 한국 달력으로 센다.</summary>
    /// 날 자르기는 DB 가 한다 — 줄을 받아다 세면 한 달치 수만 줄이 회로로 넘어온다.
    private static async Task<Dictionary<string, int>> DayCountsAsync(
        IQueryable<MenuUsageLog> query, CancellationToken ct)
    {
        var pairs = await query
            .Select(x => new { x.UserId, Day = x.OccurredAt.AddHours(KoreaOffsetHours).Date })
            .Distinct()
            .ToListAsync(ct);

        return pairs
            .GroupBy(p => p.UserId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    /// <summary>경로 → 지금의 메뉴 제목. 메뉴 표의 값을 쓰고, 지워진 경로만 적어 둔 제목으로 떨어진다.</summary>
    private async Task<MenuTitleMap> TitleMapAsync(
        IEnumerable<string> paths, CancellationToken ct)
    {
        var keys = paths.Distinct(StringComparer.Ordinal).ToArray();
        if (keys.Length == 0) return new MenuTitleMap([]);

        var rows = await db.SystemMenus
            .AsNoTracking()
            .Where(m => !m.IsDeleted && keys.Contains(m.Path))
            .Select(m => new { m.Path, m.Title, m.Name })
            .ToListAsync(ct);

        var map = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            map[row.Path] = string.IsNullOrWhiteSpace(row.Title) ? row.Name : row.Title;
        }

        return new MenuTitleMap(map);
    }

    /// <summary>로그인 아이디 → 이름·부서. 못 찾는 아이디가 있는 것이 정상이다 — 계정을 지워도 기록은 남는다.</summary>
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

    /// <summary>이름·부서를 줄에 채워 넣는다. 조회 셋이 같은 길을 쓴다.</summary>
    private async Task FillWhoAsync(
        IEnumerable<string> loginIds, Action<string, string?, string?> apply, CancellationToken ct)
    {
        var map = await WhoMapAsync(loginIds, ct);

        foreach (var (id, (name, dept)) in map)
        {
            apply(id, name, dept);
        }
    }

    /// <summary>오래된 줄을 걷어낸다. 하루 한 번만 실제로 돈다.</summary>
    /// 실패해도 삼킨다 — 청소 실패로 방금 본 화면의 기록을 못 적으면 본말이 뒤집힌다.
    private async Task SweepAsync(CancellationToken ct)
    {
        var now = AppTime.UtcNow;
        if (now - _lastSweepUtc < TimeSpan.FromDays(1)) return;
        _lastSweepUtc = now;

        try
        {
            await db.Set<MenuUsageLog>()
                .Where(x => x.OccurredAt < now.AddDays(-RetentionDays))
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "오래된 메뉴 사용기록을 걷어내지 못했습니다.");
        }
    }

    private static string? Cut(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }

    /// <summary>경로 → 지금 제목. 못 찾으면 <c>null</c> 이고 부르는 쪽이 대비책을 쓴다.</summary>
    private sealed class MenuTitleMap(Dictionary<string, string?> map)
    {
        public string? Title(string path) => map.GetValueOrDefault(path);
    }
}
