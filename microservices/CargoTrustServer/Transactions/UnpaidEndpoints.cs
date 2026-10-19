using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Statistics;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Transactions;

/// <summary>
/// 미지급 거래 목록. 미수금은 내가 못 받은 돈이고 여기는 누가 적었든 미지급으로 남은 거래다. UNPAID 하나만 모으고, 삭제·숨김(통계 제외) 거래는 뺀다 — 거래처 상세의 「미지급 N건」과 어긋나지 않게.
/// </summary>
public static class UnpaidEndpoints
{
    public static void MapUnpaidEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/unpaid-transactions", List)
            .WithTags("Transaction")
            .WithSummary("미지급 거래 목록 — 등록자(가린 이름) 포함");
    }

    private static async Task<IResult> List(
        CargoTrustDbContext db, CurrentUser me, CompanyStatsService stats, IOptions<CargoTrustOptions> options,
        long? companyId, string? q, DateOnly? from, DateOnly? to, bool? mine, CancellationToken ct)
    {
        var query = stats.CountedTransactions().AsNoTracking()
            .Include(t => t.Company).Include(t => t.User)
            .Where(t => t.PaymentStatus == PaymentStatus.UNPAID);

        if (!me.IsAdmin) query = query.Where(t => t.Company.Status != CompanyStatus.HIDDEN);
        if (companyId is { } cid) query = query.Where(t => t.CompanyId == cid);
        if (from is { } f) query = query.Where(t => t.TransportDate >= f);
        if (to is { } until) query = query.Where(t => t.TransportDate <= until);
        if (mine is true) query = query.Where(t => t.UserId == me.UserId);

        if (Check.Clean(q) is { } text)
        {
            // 거래처 검색과 같은 규칙이다 — 이름은 부분 일치, 번호는 10자리 전체로만.
            // 앞자리 몇 개로 찾게 두면 가린 뒤 5자리를 되짚을 수 있다(설계안 29).
            var bizKey = BusinessNumber.AsSearchKey(text);
            var pattern = "%" + Companies.CompanyEndpoints.EscapeLike(text) + "%";
            query = query.Where(t => EF.Functions.ILike(t.Company.CompanyName, pattern, "\\")
                                     || (bizKey != null && t.Company.BusinessNumber == bizKey));
        }

        // 오래 밀린 것이 먼저다. 예정일이 없는 거래는 「얼마나 밀렸는지」를 셀 수 없어 뒤로 보낸다.
        var rows = await query
            .OrderBy(t => t.ExpectedPaymentDate == null)
            .ThenBy(t => t.ExpectedPaymentDate)
            .ThenByDescending(t => t.TransportDate)
            .ThenByDescending(t => t.TransactionId)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);

        var today = KstDate.Today;
        var items = rows.Select(t => new UnpaidTransactionDto(
            t.TransactionId,
            t.CompanyId,
            t.Company.CompanyName,
            BusinessNumber.Display(t.Company.BusinessNumber, me.IsAdmin),
            t.TransportDate,
            TransactionMap.FirstWord(t.Origin),
            TransactionMap.FirstWord(t.Destination),
            t.TransportType,
            t.Amount,
            t.PaidAmount,
            TransactionMap.Outstanding(t),
            t.ExpectedPaymentDate,
            TransactionMap.OverdueDays(t, today),
            PersonName.Display(t.User?.DisplayName, me.IsAdmin),
            t.UserId == me.UserId,
            t.CreatedAt)).ToList();

        var summary = new UnpaidSummaryDto(
            items.Count,
            items.Sum(i => i.Outstanding),
            items.Select(i => i.CompanyId).Distinct().Count(),
            rows.Select(t => t.UserId).Distinct().Count(),
            items.Count(i => i.Mine));

        return Results.Ok(new UnpaidListDto(summary, items));
    }
}
