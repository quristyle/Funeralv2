using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Transactions;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Payments;

/// <summary>결제 등록 · 미수금</summary>
public static class PaymentEndpoints
{
    /// <summary>미수금에 드는 상태 — 다 받은 것(PAID · DELAYED)을 뺀 나머지.</summary>
    private static readonly PaymentStatus[] ReceivableStatuses =
        [PaymentStatus.SCHEDULED, PaymentStatus.PARTIAL, PaymentStatus.UNPAID, PaymentStatus.DISPUTE];

    public static void MapPaymentEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/transactions/{id:long}/payment", RegisterPayment)
            .WithTags("Payment").WithSummary("결제 등록 — 받은 금액을 쌓고 상태를 판정한다");
        api.MapGet("/receivables", Receivables)
            .WithTags("Payment").WithSummary("내 미수금");
    }

    private static async Task<IResult> RegisterPayment(
        CargoTrustDbContext db, CurrentUser me, long id, PaymentRequest req, CancellationToken ct)
    {
        var t = await db.Transactions.Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.TransactionId == id && !x.IsDeleted, ct);
        if (t is null) return ApiError.NotFound("거래를 찾을 수 없습니다.");
        if (t.UserId != me.UserId) return ApiError.Forbidden("본인이 등록한 거래만 결제를 적을 수 있습니다.");

        var record = PaymentJudge.Apply(t, req, me.UserId, out var error);
        if (record is null) return ApiError.BadRequest(error!);

        db.Payments.Add(record);
        await db.SaveChangesAsync(ct);

        var hasReview = await db.Reviews.AnyAsync(r => r.TransactionId == id, ct);
        return Results.Ok(TransactionMap.Mine(t, hasReview, me.IsAdmin, KstDate.Today));
    }

    private static async Task<IResult> Receivables(CargoTrustDbContext db, CurrentUser me, CancellationToken ct) =>
        Results.Ok(await BuildAsync(db, me.UserId, ct));

    /// <summary>한 사람의 미수금. 홈 화면도 요약을 여기서 가져간다.</summary>
    public static async Task<ReceivablesDto> BuildAsync(CargoTrustDbContext db, long userId, CancellationToken ct)
    {
        var rows = await db.Transactions.AsNoTracking().Include(t => t.Company)
            .Where(t => t.UserId == userId && !t.IsDeleted && ReceivableStatuses.Contains(t.PaymentStatus))
            .OrderBy(t => t.ExpectedPaymentDate == null)
            .ThenBy(t => t.ExpectedPaymentDate)
            .ThenBy(t => t.TransportDate)
            .ToListAsync(ct);

        var today = KstDate.Today;
        var items = rows.Select(t => new ReceivableItemDto(
            t.TransactionId,
            t.CompanyId,
            t.Company.CompanyName,
            t.TransportDate,
            t.Amount,
            t.PaidAmount,
            TransactionMap.Outstanding(t),
            t.ExpectedPaymentDate,
            TransactionMap.OverdueDays(t, today),
            t.PaymentStatus,
            BucketOf(t, today))).ToList();

        decimal Sum(string bucket) => items.Where(i => i.Bucket == bucket).Sum(i => i.Outstanding);

        var summary = new ReceivableSummaryDto(
            items.Sum(i => i.Outstanding),
            Sum("SCHEDULED"),
            Sum("DELAYED"),
            Sum("UNPAID"),
            Sum("PARTIAL"),
            Sum("DISPUTE"),
            items.Count);
        return new ReceivablesDto(summary, items);
    }

    /// <summary>
    /// 미수금의 칸. 예정(SCHEDULED)은 예정일이 지났으면 DELAYED 칸으로 옮긴다 —
    /// 상태는 아직 「예정」이지만 받는 사람에게는 이미 늦은 돈이다.
    /// </summary>
    private static string BucketOf(CargoTransaction t, DateOnly today) => t.PaymentStatus switch
    {
        PaymentStatus.SCHEDULED when t.ExpectedPaymentDate is { } e && e < today => "DELAYED",
        PaymentStatus.SCHEDULED => "SCHEDULED",
        _ => t.PaymentStatus.ToString(),
    };
}
