using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Companies;
using CargoTrustServer.Data;
using CargoTrustServer.Payments;
using CargoTrustServer.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Admin;

/// <summary>관리자 — 거래 · 결제 기록</summary>
public static class AdminTransactionEndpoints
{
    public static void MapAdminTransactionEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/transactions", List).WithSummary("거래 목록 (삭제 포함, 최신순)");
        admin.MapPut("/transactions/{id:long}", Update).WithSummary("거래 검증 상태 · 결제 상태 · 메모");
        admin.MapGet("/payments", Payments).WithSummary("결제 기록");
    }

    private static async Task<IResult> List(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options,
        string? q, string? paymentStatus, string? reviewStatus, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (!Code.TryParse<PaymentStatus>(paymentStatus, out var ps))
            return ApiError.BadRequest($"paymentStatus 는 {Code.Allowed<PaymentStatus>()} 중 하나입니다.");
        if (!Code.TryParse<ReviewStatus>(reviewStatus, out var rs))
            return ApiError.BadRequest($"reviewStatus 는 {Code.Allowed<ReviewStatus>()} 중 하나입니다.");

        var query = db.Transactions.AsNoTracking().Include(t => t.Company).Include(t => t.User).AsQueryable();
        if (ps is { } p) query = query.Where(t => t.PaymentStatus == p);
        if (rs is { } r) query = query.Where(t => t.ReviewStatus == r);
        if (from is { } f) query = query.Where(t => t.TransportDate >= f);
        if (to is { } until) query = query.Where(t => t.TransportDate <= until);
        if (Check.Clean(q) is { } text)
        {
            var pattern = "%" + CompanyEndpoints.EscapeLike(text) + "%";
            var digits = BusinessNumber.AsSearchKey(text);
            query = query.Where(t =>
                EF.Functions.ILike(t.Company.CompanyName, pattern, "\\")
                || (digits != null && t.Company.BusinessNumber == digits)
                || (t.User.DisplayName != null && EF.Functions.ILike(t.User.DisplayName, pattern, "\\"))
                || EF.Functions.ILike(t.User.ExternalUserId, pattern, "\\")
                || (t.Origin != null && EF.Functions.ILike(t.Origin, pattern, "\\"))
                || (t.Destination != null && EF.Functions.ILike(t.Destination, pattern, "\\")));
        }

        var items = await query
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.TransactionId)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);
        return Results.Ok(await ToDtosAsync(db, items, ct));
    }

    private static async Task<IResult> Update(
        CargoTrustDbContext db, AuditService audit, long id, AdminTransactionUpdate req, CancellationToken ct)
    {
        if (!Code.TryParse<ReviewStatus>(req.ReviewStatus, out var rs))
            return ApiError.BadRequest($"reviewStatus 는 {Code.Allowed<ReviewStatus>()} 중 하나입니다.");
        if (!Code.TryParse<PaymentStatus>(req.PaymentStatus, out var ps))
            return ApiError.BadRequest($"paymentStatus 는 {Code.Allowed<PaymentStatus>()} 중 하나입니다.");

        var t = await db.Transactions.Include(x => x.Company).Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TransactionId == id, ct);
        if (t is null) return ApiError.NotFound("거래를 찾을 수 없습니다.");

        var before = audit.Snapshot(t);
        if (rs is { } r) t.ReviewStatus = r;
        if (ps is { } p) t.PaymentStatus = p;
        if (req.AdminMemo is not null) t.AdminMemo = Check.Clean(req.AdminMemo);
        t.UpdatedAt = DateTimeOffset.UtcNow;
        audit.AddChange("ADMIN_TRANSACTION_UPDATE", AuditTarget.Transaction, id, before, t);
        await db.SaveChangesAsync(ct);

        return Results.Ok((await ToDtosAsync(db, [t], ct))[0]);
    }

    private static async Task<IResult> Payments(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        // 기간은 **등록한 날**로 자른다. 미지급·분쟁 판정에는 받은 날(paid_date)이 없어서
        // 지급일로 자르면 그 기록들이 목록에서 사라진다.
        var query = db.Payments.AsNoTracking().AsQueryable();
        if (from is { } f)
        {
            var start = KstDate.StartUtc(f);
            query = query.Where(p => p.CreatedAt >= start);
        }
        if (to is { } until)
        {
            var end = KstDate.StartUtc(until.AddDays(1));
            query = query.Where(p => p.CreatedAt < end);
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.PaymentId)
            .Take(options.Value.ListLimit)
            .Join(db.Transactions, p => p.TransactionId, t => t.TransactionId,
                (p, t) => new { Payment = p, t.Amount, CompanyName = t.Company.CompanyName, UserName = t.User.DisplayName ?? t.User.ExternalUserId })
            .ToListAsync(ct);

        return Results.Ok(rows.Select(x =>
        {
            var dto = PaymentMap.Record<AdminPaymentDto>(x.Payment);
            dto.TransactionId = x.Payment.TransactionId;
            dto.CompanyName = x.CompanyName;
            dto.UserName = x.UserName;
            dto.Amount = x.Amount;
            return dto;
        }).OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.PaymentId).ToList());
    }

    /// <summary>거래 → AdminTransaction. Company · User 가 실려 있어야 한다.</summary>
    private static async Task<List<AdminTransactionDto>> ToDtosAsync(CargoTrustDbContext db, List<CargoTransaction> items, CancellationToken ct)
    {
        var ids = items.Select(t => t.TransactionId).ToList();
        var reviewed = await TransactionEndpoints.ReviewedIdsAsync(db, ids, ct);
        var reportCounts = await db.Reports.AsNoTracking()
            .Where(r => r.TargetType == ReportTarget.TRANSACTION && ids.Contains(r.TargetId))
            .GroupBy(r => r.TargetId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var disputeCounts = await db.Disputes.AsNoTracking()
            .Where(d => ids.Contains(d.TransactionId))
            .GroupBy(d => d.TransactionId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var today = KstDate.Today;
        return items.Select(t =>
        {
            var dto = TransactionMap.Mine<AdminTransactionDto>(t, reviewed.Contains(t.TransactionId), isAdmin: true, today);
            dto.UserId = t.UserId;
            dto.UserName = t.User.DisplayName;
            dto.ExternalUserId = t.User.ExternalUserId;
            dto.FlagReason = t.FlagReason;
            dto.AdminMemo = t.AdminMemo;
            dto.IsDeleted = t.IsDeleted;
            dto.ReportCount = reportCounts.GetValueOrDefault(t.TransactionId);
            dto.DisputeCount = disputeCounts.GetValueOrDefault(t.TransactionId);
            return dto;
        }).ToList();
    }
}
