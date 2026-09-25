using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Statistics;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Transactions;

/// <summary>
/// 미지급 거래 목록 — 서비스에 쌓인 <c>UNPAID</c> 거래를 거래처를 가리지 않고 모은다.
///
/// <para>
/// [미수금과 다르다]
/// </para>
///
/// <para>
/// 미수금(<see cref="Payments.PaymentEndpoints"/>)은 **내가** 못 받은 돈이다.
/// 여기는 **누가 적었든** 미지급으로 남은 거래다 — 거래처를 살피는 쪽이 보는
/// 자리라, 한 거래처에 여러 사람이 같은 일을 겪었는지가 이 목록의 전부다.
/// 그래서 상태 넷(예정·일부·미지급·분쟁)이 아니라 <c>UNPAID</c> 하나만 모은다.
/// 예정·일부는 아직 약속이 남은 것이고 분쟁은 사실 다툼이 끝나지 않은 것이라,
/// 「안 준 거래」라는 한 이름으로 묶으면 없는 말을 보태게 된다.
/// </para>
///
/// <para>
/// [등록자를 싣는다 — 가린 이름으로]
/// </para>
///
/// <para>
/// 누가 적었는지 없는 목록은 출처 없는 주장이라, 한 사람이 열 건을 올린 것과
/// 열 사람이 한 건씩 올린 것을 가릴 수 없다. 그렇다고 이름을 그대로 싣는 것은
/// 설계안 29 가 막는다 — 가운데를 가려 싣는다(<see cref="PersonName.Display"/>).
/// 관리자는 그대로 본다.
/// </para>
///
/// <para>
/// [통계와 같은 거래만 센다]
/// </para>
///
/// <para>
/// 삭제된 거래와 <c>HIDDEN</c>(통계 제외) 거래는 뺀다
/// (<see cref="CompanyStatsService.CountedTransactions"/>) — 거래처 상세의
/// 「미지급 N건」과 이 목록의 줄 수가 어긋나면 어느 쪽도 못 믿는다.
/// 숨긴 거래처의 거래도 관리자가 아니면 뺀다.
/// </para>
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
