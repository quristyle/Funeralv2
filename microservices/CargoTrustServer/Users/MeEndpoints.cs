using CargoTrustServer.Common;
using CargoTrustServer.Companies;
using CargoTrustServer.Data;
using CargoTrustServer.Payments;
using CargoTrustServer.Statistics;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Users;

/// <summary>Me</summary>
public record MeDto(
    long UserId,
    string ExternalUserId,
    string? DisplayName,
    UserType UserType,
    long? CompanyId,
    string? CompanyName,
    UserStatus Status,
    bool IsAdmin,
    int TransactionCount,
    DateTimeOffset CreatedAt);

/// <summary>Home</summary>
public record HomeDto(MeDto Me, ReceivableSummaryDto Receivable, List<CompanySummaryDto> RecentCompanies);

/// <summary>나 · 홈</summary>
public static class MeEndpoints
{
    public static void MapMeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", Me).WithTags("Me").WithSummary("나 — 처음 부르면 사용자 줄이 생긴다");
        api.MapGet("/home", Home).WithTags("Me").WithSummary("홈 — 나 · 미수금 요약 · 최근 본 거래처");
    }

    private static async Task<IResult> Me(CargoTrustDbContext db, CurrentUser me, CancellationToken ct) =>
        Results.Ok(await BuildMeAsync(db, me, ct));

    private static async Task<IResult> Home(
        CargoTrustDbContext db, CurrentUser me, CompanyStatsService stats, CancellationToken ct)
    {
        var meDto = await BuildMeAsync(db, me, ct);
        var receivables = await PaymentEndpoints.BuildAsync(db, me.UserId, ct);

        var viewed = db.CompanyViews.AsNoTracking().Where(v => v.UserId == me.UserId);
        if (!me.IsAdmin) viewed = viewed.Where(v => v.Company.Status != CompanyStatus.HIDDEN);
        var companies = await viewed
            .OrderByDescending(v => v.ViewedAt)
            .Take(10)
            .Select(v => v.Company)
            .ToListAsync(ct);

        var statMap = await stats.ForCompaniesAsync(companies.Select(c => c.CompanyId).ToList(), null, ct);
        return Results.Ok(new HomeDto(
            meDto,
            receivables.Summary,
            companies.Select(c => CompanyMap.Summary(c, me.IsAdmin, statMap[c.CompanyId])).ToList()));
    }

    private static async Task<MeDto> BuildMeAsync(CargoTrustDbContext db, CurrentUser me, CancellationToken ct)
    {
        var u = me.Entity;
        var companyName = u.CompanyId is { } cid
            ? await db.Companies.Where(c => c.CompanyId == cid).Select(c => c.CompanyName).FirstOrDefaultAsync(ct)
            : null;
        var count = await db.Transactions.CountAsync(t => t.UserId == u.UserId && !t.IsDeleted, ct);
        return new MeDto(u.UserId, u.ExternalUserId, u.DisplayName, u.UserType, u.CompanyId, companyName,
            u.Status, me.IsAdmin, count, u.CreatedAt);
    }
}
