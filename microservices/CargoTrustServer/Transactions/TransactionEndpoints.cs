using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Disputes;
using CargoTrustServer.Payments;
using CargoTrustServer.Reviews;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Transactions;

/// <summary>내 거래 — 목록 · 상세 · 등록 · 수정 · 삭제, 그리고 운송사의 「우리 회사 거래」</summary>
public static class TransactionEndpoints
{
    public static void MapTransactionEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/transactions").WithTags("Transaction");

        group.MapGet("/", List).WithSummary("내 거래 목록");
        group.MapGet("/{id:long}", Detail).WithSummary("거래 상세 — 결제 이력 · 후기 · 이의제기");
        group.MapPost("/", Create).WithSummary("거래 등록");
        group.MapPut("/{id:long}", Update).WithSummary("거래 수정 (등록자만)");
        group.MapDelete("/{id:long}", Delete).WithSummary("거래 삭제 (등록자만, 논리 삭제)");

        api.MapGet("/company-transactions", CompanyTransactions)
            .WithTags("Transaction")
            .WithSummary("운송사 — 자기 회사에 관한 거래 (이의제기 고르기용)");
    }

    private static async Task<IResult> List(
        CargoTrustDbContext db, CurrentUser me, IOptions<CargoTrustOptions> options,
        string? status, DateOnly? from, DateOnly? to, long? companyId, CancellationToken ct)
    {
        if (!Code.TryParse<PaymentStatus>(status, out var paymentStatus))
            return ApiError.BadRequest($"status 는 {Code.Allowed<PaymentStatus>()} 중 하나입니다.");

        var query = db.Transactions.AsNoTracking().Include(t => t.Company)
            .Where(t => t.UserId == me.UserId && !t.IsDeleted);
        if (paymentStatus is { } s) query = query.Where(t => t.PaymentStatus == s);
        if (from is { } f) query = query.Where(t => t.TransportDate >= f);
        if (to is { } until) query = query.Where(t => t.TransportDate <= until);
        if (companyId is { } cid) query = query.Where(t => t.CompanyId == cid);

        var items = await query
            .OrderByDescending(t => t.TransportDate).ThenByDescending(t => t.TransactionId)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);

        var reviewed = await ReviewedIdsAsync(db, items.Select(t => t.TransactionId).ToList(), ct);
        var today = KstDate.Today;
        return Results.Ok(items.Select(t => TransactionMap.Mine(t, reviewed.Contains(t.TransactionId), me.IsAdmin, today)).ToList());
    }

    private static async Task<IResult> Detail(CargoTrustDbContext db, CurrentUser me, long id, CancellationToken ct)
    {
        var t = await db.Transactions.AsNoTracking().Include(x => x.Company).FirstOrDefaultAsync(x => x.TransactionId == id, ct);
        // 삭제된 거래는 등록자에게도 없는 것이다. 관리자만 본다(분쟁 확인용).
        if (t is null || (t.IsDeleted && !me.IsAdmin))
            return ApiError.NotFound("거래를 찾을 수 없습니다.");
        if (t.UserId != me.UserId && !me.IsAdmin)
            return ApiError.Forbidden("본인이 등록한 거래만 볼 수 있습니다.");

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.TransactionId == id)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.PaymentId)
            .ToListAsync(ct);
        var review = await db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.TransactionId == id, ct);
        var disputes = await db.Disputes.AsNoTracking()
            .Include(d => d.Transaction).Include(d => d.Company)
            .Where(d => d.TransactionId == id)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(new TransactionDetailDto(
            TransactionMap.Mine(t, review is not null, me.IsAdmin, KstDate.Today),
            payments.Select(PaymentMap.Record).ToList(),
            review is null ? null : ReviewMap.Review(review),
            disputes.Select(DisputeMap.Dispute).ToList()));
    }

    private static async Task<IResult> Create(
        CargoTrustDbContext db, CurrentUser me, AuditService audit,
        TransactionSaveRequest req, CancellationToken ct)
    {
        var (company, error) = await TransactionRules.ValidateAsync(db, req, ct);
        if (error is not null) return ApiError.BadRequest(error);

        if (await TransactionRules.IsDuplicateAsync(db, me.UserId, req, null, ct))
            return ApiError.Conflict(TransactionRules.DuplicateMessage);

        var flag = await TransactionRules.FlagReasonAsync(db, me.UserId, company!.CompanyId, req.TransportDate!.Value, countToday: true, ct);

        var now = DateTimeOffset.UtcNow;
        var t = new CargoTransaction
        {
            UserId = me.UserId,
            PaymentStatus = PaymentStatus.SCHEDULED,
            ReviewStatus = flag is null ? ReviewStatus.NORMAL : ReviewStatus.FLAGGED,
            FlagReason = flag,
            CreatedAt = now,
            UpdatedAt = now,
        };
        TransactionRules.Apply(t, req);
        db.Transactions.Add(t);
        await db.SaveChangesAsync(ct);

        audit.Add("TRANSACTION_CREATE", AuditTarget.Transaction, t.TransactionId, null, audit.Snapshot(t));
        await db.SaveChangesAsync(ct);

        t.Company = company;
        return Results.Ok(TransactionMap.Mine(t, false, me.IsAdmin, KstDate.Today));
    }

    private static async Task<IResult> Update(
        CargoTrustDbContext db, CurrentUser me, AuditService audit,
        long id, TransactionSaveRequest req, CancellationToken ct)
    {
        var t = await db.Transactions.FirstOrDefaultAsync(x => x.TransactionId == id && !x.IsDeleted, ct);
        if (t is null) return ApiError.NotFound("거래를 찾을 수 없습니다.");
        if (t.UserId != me.UserId) return ApiError.Forbidden("본인이 등록한 거래만 고칠 수 있습니다.");

        var (company, error) = await TransactionRules.ValidateAsync(db, req, ct);
        if (error is not null) return ApiError.BadRequest(error);

        if (await TransactionRules.IsDuplicateAsync(db, me.UserId, req, id, ct))
            return ApiError.Conflict(TransactionRules.DuplicateMessage);

        var before = audit.Snapshot(t);
        TransactionRules.Apply(t, req);
        // 운송료·예정일이 바뀌면 이미 받은 금액에 대한 판정도 달라진다.
        PaymentJudge.Recalculate(t);

        // 고치면서 운송일을 미래로 옮기면 새로 넣을 때와 같이 표시한다.
        // 관리자가 이미 내린 판정(VERIFIED · HIDDEN)은 사용자의 수정이 덮지 않는다.
        if (t.ReviewStatus == ReviewStatus.NORMAL
            && await TransactionRules.FlagReasonAsync(db, me.UserId, t.CompanyId, t.TransportDate, countToday: false, ct) is { } flag)
        {
            t.ReviewStatus = ReviewStatus.FLAGGED;
            t.FlagReason = flag;
        }
        t.UpdatedAt = DateTimeOffset.UtcNow;

        audit.AddChange("TRANSACTION_UPDATE", AuditTarget.Transaction, t.TransactionId, before, t);
        await db.SaveChangesAsync(ct);

        t.Company = company!;
        var hasReview = await db.Reviews.AnyAsync(r => r.TransactionId == id, ct);
        return Results.Ok(TransactionMap.Mine(t, hasReview, me.IsAdmin, KstDate.Today));
    }

    private static async Task<IResult> Delete(
        CargoTrustDbContext db, CurrentUser me, AuditService audit, long id, CancellationToken ct)
    {
        var t = await db.Transactions.FirstOrDefaultAsync(x => x.TransactionId == id && !x.IsDeleted, ct);
        if (t is null) return ApiError.NotFound("거래를 찾을 수 없습니다.");
        if (t.UserId != me.UserId) return ApiError.Forbidden("본인이 등록한 거래만 지울 수 있습니다.");

        // 지우지 않고 표시만 한다 — 분쟁에서 「있었던 거래」를 되짚을 수 있어야 한다.
        var before = audit.Snapshot(t);
        t.IsDeleted = true;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        audit.AddChange("TRANSACTION_DELETE", AuditTarget.Transaction, t.TransactionId, before, t);
        await db.SaveChangesAsync(ct);
        return ApiError.Empty();
    }

    private static async Task<IResult> CompanyTransactions(
        CargoTrustDbContext db, CurrentUser me, IOptions<CargoTrustOptions> options, CancellationToken ct)
    {
        if (me.UserType != UserType.CARRIER || me.Entity.CompanyId is not { } companyId)
            return ApiError.Forbidden("거래처에 연결된 운송사 사용자만 볼 수 있습니다.");

        // HIDDEN 거래도 싣는다. 통계에서 빠졌더라도 운송사가 이의를 걸 수는 있어야 한다.
        var items = await db.Transactions.AsNoTracking()
            .Where(t => t.CompanyId == companyId && !t.IsDeleted)
            .OrderByDescending(t => t.TransportDate).ThenByDescending(t => t.TransactionId)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);
        return Results.Ok(items.Select(TransactionMap.Public).ToList());
    }

    /// <summary>후기가 달린 거래 번호들.</summary>
    public static async Task<HashSet<long>> ReviewedIdsAsync(CargoTrustDbContext db, List<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var list = await db.Reviews.Where(r => ids.Contains(r.TransactionId)).Select(r => r.TransactionId).ToListAsync(ct);
        return list.ToHashSet();
    }
}
