using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Transactions;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Payments;

/// <summary>결제 등록 · 미수금</summary>
public static class PaymentEndpoints
{
    /// <summary>
    /// 미수금에 드는 상태 — 다 받은 것(PAID · DELAYED)을 뺀 나머지.
    /// 내 거래 목록의 <c>open=true</c> 도 이 목록을 그대로 쓴다(<see cref="Transactions.TransactionEndpoints"/>) —
    /// 두 곳에 적으면 미수금 화면과 「미처리」 목록에 다른 거래가 뜬다.
    /// </summary>
    public static readonly PaymentStatus[] ReceivableStatuses =
        [PaymentStatus.SCHEDULED, PaymentStatus.PARTIAL, PaymentStatus.UNPAID, PaymentStatus.DISPUTE];

    /// <summary>한 번에 처리할 수 있는 거래 수. 실수로 전체를 고른 채 누르는 일을 막는 선이다.</summary>
    private const int BulkLimit = 100;

    public static void MapPaymentEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/transactions/{id:long}/payment", RegisterPayment)
            .WithTags("Payment").WithSummary("결제 등록 — 받은 금액을 쌓고 상태를 판정한다");
        // `{id:long}` 은 숫자만 받으므로 아래 경로와 부딪히지 않는다.
        api.MapPost("/transactions/payments", RegisterPayments)
            .WithTags("Payment").WithSummary("결제 한 번에 등록 — 고른 거래마다 남은 금액 전액");
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

    /// <summary>
    /// 고른 거래를 한 번에 처리한다. 한 회사가 여러 건을 묶어 입금했을 때 건마다
    /// 팝업을 열지 않게 하는 길이다(거래처 상세의 「내 미처리 거래」).
    ///
    /// <para>
    /// 판정은 단건과 **똑같은 <see cref="PaymentJudge"/>** 를 거친다. 여기서
    /// 따로 계산하면 같은 사실이 어느 화면에서 적었는지에 따라 다른 상태로 쌓인다.
    /// 건마다 넣는 금액은 남은 금액 전액이다 — 나눠 받은 건은 단건으로 적는다.
    /// </para>
    /// </summary>
    private static async Task<IResult> RegisterPayments(
        CargoTrustDbContext db, CurrentUser me, BulkPaymentRequest req, CancellationToken ct)
    {
        // 같은 거래를 두 번 고른 채로 오면 두 번 쌓인다 — 보내는 쪽을 믿지 않는다.
        var ids = (req.TransactionIds ?? []).Distinct().ToList();
        if (ids.Count == 0) return ApiError.BadRequest("처리할 거래를 고르십시오.");
        if (ids.Count > BulkLimit) return ApiError.BadRequest($"한 번에 {BulkLimit}건까지 처리합니다.");
        if (Check.MaxLength(req.Memo, 10_000, "메모") is { } memoError) return ApiError.BadRequest(memoError);

        if (!Code.TryParse<PaymentStatus>(req.Result, out var result)
            || result is not (null or PaymentStatus.UNPAID or PaymentStatus.DISPUTE))
        {
            return ApiError.BadRequest("result 는 비우거나 UNPAID · DISPUTE 중 하나입니다.");
        }

        var rows = await db.Transactions.Include(x => x.Company)
            .Where(x => ids.Contains(x.TransactionId) && !x.IsDeleted && x.UserId == me.UserId)
            .ToListAsync(ct);
        var byId = rows.ToDictionary(x => x.TransactionId);

        var updated = new List<CargoTransaction>();
        var failed = new List<BulkPaymentFailureDto>();

        // 고른 차례 그대로 돈다 — 화면이 「무엇이 안 됐는지」를 고른 순서로 읽는다.
        foreach (var id in ids)
        {
            // 남의 거래·지운 거래·없는 번호를 가르지 않는다. 그 셋을 구별해 말하면
            // 번호를 바꿔 가며 남의 거래가 있는지 떠볼 수 있다.
            if (!byId.TryGetValue(id, out var t))
            {
                failed.Add(new BulkPaymentFailureDto(id, "거래를 찾을 수 없습니다."));
                continue;
            }

            var one = new PaymentRequest
            {
                PaidDate = req.PaidDate,
                PaidAmount = TransactionMap.Outstanding(t),
                Result = req.Result,
                Memo = req.Memo,
            };

            // 「받았다」인데 남은 금액이 0 이면 단건과 같은 말(0보다 커야 한다)이
            // 나오지만, 여기서는 왜 걸렸는지가 그 말로 읽히지 않는다.
            if (result is null && one.PaidAmount <= 0)
            {
                failed.Add(new BulkPaymentFailureDto(id, "이미 다 받은 거래입니다."));
                continue;
            }

            var record = PaymentJudge.Apply(t, one, me.UserId, out var error);
            if (record is null)
            {
                failed.Add(new BulkPaymentFailureDto(id, error!));
                continue;
            }

            db.Payments.Add(record);
            updated.Add(t);
        }

        // 한 번에 저장한다 — 중간에 끊겨 절반만 쌓이면 어디까지 적혔는지 알 길이 없다.
        if (updated.Count > 0) await db.SaveChangesAsync(ct);

        var updatedIds = updated.Select(t => t.TransactionId).ToList();
        var reviewed = await db.Reviews.AsNoTracking()
            .Where(r => updatedIds.Contains(r.TransactionId))
            .Select(r => r.TransactionId)
            .ToListAsync(ct);

        var today = KstDate.Today;
        return Results.Ok(new BulkPaymentResultDto(
            updated.Select(t => TransactionMap.Mine(t, reviewed.Contains(t.TransactionId), me.IsAdmin, today)).ToList(),
            failed));
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
