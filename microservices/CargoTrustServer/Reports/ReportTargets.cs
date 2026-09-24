using System.Globalization;
using CargoTrustServer.Common;
using CargoTrustServer.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Reports;

/// <summary>
/// 신고 대상 — (target_type, target_id) 는 FK 가 없어서 있는지와 한 줄 요약을 여기서 찾는다.
/// </summary>
public static class ReportTargets
{
    /// <summary>대상이 있는가. 삭제된 거래는 없는 것으로 본다.</summary>
    public static Task<bool> ExistsAsync(CargoTrustDbContext db, ReportTarget type, long id, CancellationToken ct) => type switch
    {
        ReportTarget.COMPANY => db.Companies.AnyAsync(c => c.CompanyId == id && c.Status != CompanyStatus.HIDDEN, ct),
        ReportTarget.TRANSACTION => db.Transactions.AnyAsync(t => t.TransactionId == id && !t.IsDeleted, ct),
        ReportTarget.REVIEW => db.Reviews.AnyAsync(r => r.ReviewId == id, ct),
        _ => Task.FromResult(false),
    };

    /// <summary>
    /// 신고들의 대상 요약. 종류별로 한 번씩만 읽는다.
    /// 사업자번호는 보는 사람 기준으로 가린다.
    /// </summary>
    public static async Task<Dictionary<(ReportTarget, long), string>> SummarizeAsync(
        CargoTrustDbContext db, IEnumerable<Report> reports, bool isAdmin, CancellationToken ct)
    {
        var list = reports.ToList();
        var result = new Dictionary<(ReportTarget, long), string>();

        var companyIds = list.Where(r => r.TargetType == ReportTarget.COMPANY).Select(r => r.TargetId).Distinct().ToList();
        if (companyIds.Count > 0)
        {
            var companies = await db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.CompanyId)).ToListAsync(ct);
            foreach (var c in companies)
                result[(ReportTarget.COMPANY, c.CompanyId)] = $"{c.CompanyName} ({BusinessNumber.Display(c.BusinessNumber, isAdmin)})";
        }

        var txIds = list.Where(r => r.TargetType == ReportTarget.TRANSACTION).Select(r => r.TargetId).Distinct().ToList();
        if (txIds.Count > 0)
        {
            var txs = await db.Transactions.AsNoTracking().Include(t => t.Company)
                .Where(t => txIds.Contains(t.TransactionId)).ToListAsync(ct);
            foreach (var t in txs)
                result[(ReportTarget.TRANSACTION, t.TransactionId)] = TransactionLine(t);
        }

        var reviewIds = list.Where(r => r.TargetType == ReportTarget.REVIEW).Select(r => r.TargetId).Distinct().ToList();
        if (reviewIds.Count > 0)
        {
            var reviews = await db.Reviews.AsNoTracking().Include(r => r.Transaction).ThenInclude(t => t.Company)
                .Where(r => reviewIds.Contains(r.ReviewId)).ToListAsync(ct);
            foreach (var r in reviews)
            {
                var head = r.Content.Length > 30 ? r.Content[..30] + "…" : r.Content;
                result[(ReportTarget.REVIEW, r.ReviewId)] = $"{r.Transaction.Company.CompanyName} 후기 · {head}";
            }
        }

        return result;
    }

    public static string? Get(this Dictionary<(ReportTarget, long), string> map, Report r) =>
        map.TryGetValue((r.TargetType, r.TargetId), out var s) ? s : null;

    private static string TransactionLine(CargoTransaction t) =>
        $"{t.Company.CompanyName} · {t.TransportDate:yyyy-MM-dd} · {t.Amount.ToString("#,0", CultureInfo.InvariantCulture)}원";
}
