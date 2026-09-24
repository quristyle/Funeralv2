using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Endpoints;
using CargoTrustServer.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Admin;

/// <summary>
/// 관리자 — <c>/admin/*</c>. 관리자가 아니면 403 (<see cref="AdminOnlyFilter"/>).
/// 모든 변경은 audit_log 에 전·후를 남긴다(설계안 30).
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin")
            .WithTags("Admin")
            .AddEndpointFilter<AdminOnlyFilter>();

        admin.MapGet("/dashboard", Dashboard).WithSummary("관리자 대시보드");
        admin.MapGet("/statistics", Statistics).WithSummary("관리자 통계 — 월별 · 지연 많은 거래처 · 표시된 사용자");
        admin.MapGet("/audit", AuditList).WithSummary("감사 기록");

        admin.MapAdminCompanyEndpoints();
        admin.MapAdminTransactionEndpoints();
        admin.MapAdminModerationEndpoints();
        admin.MapAdminUserEndpoints();
    }

    private static async Task<IResult> Dashboard(CargoTrustDbContext db, CompanyStatsService stats, CancellationToken ct)
    {
        var since30 = DateTimeOffset.UtcNow.AddDays(-30);
        var live = db.Transactions.Where(t => !t.IsDeleted);

        // 금액·상태 분포는 통계와 같은 거래(삭제 · HIDDEN 제외)로 센다 — 거래처 화면의 숫자와 맞아야 한다.
        var rows = await stats.LoadAllRowsAsync(null, ct);

        var dto = new AdminDashboardDto(
            await db.Companies.CountAsync(ct),
            await db.Users.CountAsync(ct),
            await live.CountAsync(ct),
            await live.CountAsync(t => t.CreatedAt >= since30, ct),
            await live.CountAsync(t => t.ReviewStatus == ReviewStatus.FLAGGED, ct),
            await db.Reports.CountAsync(r => r.Status == ReportStatus.RECEIVED || r.Status == ReportStatus.REVIEWING, ct),
            await db.Disputes.CountAsync(d => d.Status == DisputeStatus.RECEIVED || d.Status == DisputeStatus.REVIEWING, ct),
            rows.Sum(r => r.Amount),
            rows.Sum(CompanyStatsService.Outstanding),
            Enum.GetValues<PaymentStatus>().Select(s => new StatusCountDto(s, rows.Count(r => r.PaymentStatus == s))).ToList(),
            await db.AuditLogs.AsNoTracking().ToEntriesAsync(db, 10, ct));
        return Results.Ok(dto);
    }

    private static async Task<IResult> Statistics(
        CargoTrustDbContext db, CompanyStatsService stats, int? months, CancellationToken ct)
    {
        var span = Math.Clamp(months ?? 12, 1, 60);
        var today = KstDate.Today;

        var all = await stats.LoadAllRowsAsync(null, ct);
        var monthly = CompanyStatsService.Monthly(all, span, today);

        var top = CompanyStatsService.TopDelay(all, 10, today);
        var topIds = top.Select(x => x.CompanyId).ToList();
        var companies = await db.Companies.AsNoTracking()
            .Where(c => topIds.Contains(c.CompanyId))
            .ToDictionaryAsync(c => c.CompanyId, ct);
        var topDelay = top.Select(x => new DelayCompanyDto(
            x.CompanyId,
            companies.TryGetValue(x.CompanyId, out var c) ? c.CompanyName : "",
            companies.TryGetValue(x.CompanyId, out var c2) ? BusinessNumber.Display(c2.BusinessNumber, true) : "",
            x.Stats.TotalCount,
            x.Stats.DelayedCount,
            x.Stats.UnpaidCount,
            x.Stats.AverageDelayDays)).ToList();

        // 비정상 패턴으로 표시된 거래가 많은 사람. 삭제한 것까지 센다 — 표시된 뒤 지워 버리는
        // 것도 패턴이다.
        var flagged = await db.Transactions.AsNoTracking()
            .Where(t => t.ReviewStatus == ReviewStatus.FLAGGED)
            .GroupBy(t => t.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(ct);
        var flaggedIds = flagged.Select(x => x.UserId).ToList();
        var users = await db.Users.AsNoTracking().Where(u => flaggedIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, ct);
        var txCounts = await db.Transactions.AsNoTracking()
            .Where(t => flaggedIds.Contains(t.UserId) && !t.IsDeleted)
            .GroupBy(t => t.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);
        var flaggedUsers = flagged.Select(x => new FlaggedUserDto(
            x.UserId,
            users.TryGetValue(x.UserId, out var u) ? u.DisplayName : null,
            users.TryGetValue(x.UserId, out var u2) ? u2.ExternalUserId : "",
            x.Count,
            txCounts.GetValueOrDefault(x.UserId))).ToList();

        return Results.Ok(new AdminStatisticsDto(monthly, topDelay, flaggedUsers));
    }

    private static async Task<IResult> AuditList(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options,
        string? targetType, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (Check.Clean(targetType) is { } type)
        {
            var upper = type.ToUpperInvariant();
            query = query.Where(a => a.TargetType == upper);
        }
        if (from is { } f)
        {
            var start = KstDate.StartUtc(f);
            query = query.Where(a => a.CreatedAt >= start);
        }
        if (to is { } until)
        {
            var end = KstDate.StartUtc(until.AddDays(1));
            query = query.Where(a => a.CreatedAt < end);
        }
        return Results.Ok(await query.ToEntriesAsync(db, options.Value.ListLimit, ct));
    }
}
