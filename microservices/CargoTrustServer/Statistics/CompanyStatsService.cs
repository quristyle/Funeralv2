using CargoTrustServer.Common;
using CargoTrustServer.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Statistics;

/// <summary>
/// CompanyStats — 계약 「통계 계산」 표 그대로.
///
/// 판정어(「악성」「위험」)는 싣지 않는다(설계안 12). 숫자와 기간·규모만 준다.
/// </summary>
public record CompanyStatsDto
{
    public int TotalCount { get; init; }
    public int NormalCount { get; init; }
    public int DelayedCount { get; init; }
    public int PartialCount { get; init; }
    public int UnpaidCount { get; init; }
    public int DisputeCount { get; init; }
    public int ScheduledCount { get; init; }
    public int SettledCount { get; init; }
    public decimal? NormalRate { get; init; }
    public decimal? AveragePromisedDays { get; init; }
    public decimal? AverageActualDays { get; init; }
    public decimal? AverageDelayDays { get; init; }
    public int? MaxDelayDays { get; init; }
    public int RecentUnpaidCount { get; init; }
    public DateOnly? LastTransactionDate { get; init; }
    public Confidence Confidence { get; init; }
    public string ConfidenceLabel { get; init; } = string.Empty;
    public int? PeriodDays { get; init; }
    public string PeriodLabel { get; init; } = string.Empty;
}

/// <summary>관리자 통계의 월별 한 칸.</summary>
public record MonthlyStatDto(
    string Month,
    int Count,
    decimal Amount,
    int NormalCount,
    int DelayedCount,
    int PartialCount,
    int UnpaidCount,
    int DisputeCount);

/// <summary>통계에 드는 거래 한 줄 — 필요한 열만 읽는다.</summary>
public record StatRow(
    long CompanyId,
    DateOnly TransportDate,
    DateOnly? ExpectedPaymentDate,
    DateOnly? ActualPaymentDate,
    PaymentStatus PaymentStatus,
    decimal Amount,
    decimal PaidAmount);

/// <summary>
/// 거래처 통계를 계산하는 **유일한 곳**이다.
///
/// 사용자 화면(검색 · 상세 · 홈)과 관리자 화면(대시보드 · 통계)이 모두 여기를 부른다.
/// 두 곳이 식을 따로 가지면 같은 회사의 정상률이 화면마다 달라지고, 그 순간 숫자를 믿을
/// 이유가 사라진다.
///
/// 원본 거래에서 매번 계산한다(집계 테이블 없음). 삭제된 거래와 HIDDEN 거래는 뺀다.
/// 기간은 운송일(transport_date) 기준이다.
/// </summary>
public class CompanyStatsService(CargoTrustDbContext db)
{
    /// <summary>상세 화면이 늘 싣는 다섯 기간. null = 전체.</summary>
    public static readonly int?[] StandardPeriods = [30, 90, 180, 365, null];

    /// <summary>「최근 미지급」을 세는 창 — 기간과 무관하다.</summary>
    public const int RecentUnpaidDays = 90;

    /// <summary>통계에 드는 거래 — 삭제 안 된 것, HIDDEN 이 아닌 것.</summary>
    public IQueryable<CargoTransaction> CountedTransactions() =>
        db.Transactions.Where(t => !t.IsDeleted && t.ReviewStatus != ReviewStatus.HIDDEN);

    /// <summary>회사들의 통계 줄을 한 번에 읽는다.</summary>
    public async Task<List<StatRow>> LoadRowsAsync(IReadOnlyCollection<long> companyIds, CancellationToken ct)
    {
        if (companyIds.Count == 0) return [];
        return await ToRows(CountedTransactions().Where(t => companyIds.Contains(t.CompanyId))).ToListAsync(ct);
    }

    /// <summary>전 거래(관리자 통계용)의 통계 줄.</summary>
    public Task<List<StatRow>> LoadAllRowsAsync(DateOnly? from, CancellationToken ct)
    {
        var q = CountedTransactions();
        if (from is { } f) q = q.Where(t => t.TransportDate >= f);
        return ToRows(q).ToListAsync(ct);
    }

    private static IQueryable<StatRow> ToRows(IQueryable<CargoTransaction> q) =>
        q.Select(t => new StatRow(t.CompanyId, t.TransportDate, t.ExpectedPaymentDate, t.ActualPaymentDate,
            t.PaymentStatus, t.Amount, t.PaidAmount));

    /// <summary>회사별 한 기간 통계. 거래가 없는 회사도 빈 통계로 채워 돌려준다.</summary>
    public async Task<Dictionary<long, CompanyStatsDto>> ForCompaniesAsync(
        IReadOnlyCollection<long> companyIds, int? periodDays, CancellationToken ct)
    {
        var rows = await LoadRowsAsync(companyIds, ct);
        var today = KstDate.Today;
        var byCompany = rows.ToLookup(r => r.CompanyId);
        return companyIds.Distinct().ToDictionary(id => id, id => Compute(byCompany[id], periodDays, today));
    }

    /// <summary>
    /// 통계 한 벌. <paramref name="rows"/> 는 한 회사(또는 묶음)의 통계 줄 전부 — 기간은 여기서 자른다.
    /// </summary>
    public static CompanyStatsDto Compute(IEnumerable<StatRow> rows, int? periodDays, DateOnly today)
    {
        var all = rows as IReadOnlyCollection<StatRow> ?? rows.ToList();
        var from = periodDays is { } days ? today.AddDays(-days) : (DateOnly?)null;
        var inPeriod = from is { } f ? all.Where(r => r.TransportDate >= f).ToList() : all.ToList();

        int CountOf(PaymentStatus s) => inPeriod.Count(r => r.PaymentStatus == s);

        var total = inPeriod.Count;
        var normal = CountOf(PaymentStatus.PAID);
        var scheduled = CountOf(PaymentStatus.SCHEDULED);
        var settled = total - scheduled;

        var promised = inPeriod
            .Where(r => r.ExpectedPaymentDate.HasValue)
            .Select(r => r.ExpectedPaymentDate!.Value.DayNumber - r.TransportDate.DayNumber)
            .ToList();
        var actual = inPeriod
            .Where(r => r.ActualPaymentDate.HasValue)
            .Select(r => r.ActualPaymentDate!.Value.DayNumber - r.TransportDate.DayNumber)
            .ToList();
        var delays = inPeriod
            .Where(r => r.PaymentStatus == PaymentStatus.DELAYED)
            .Select(r => DelayDays(r.ExpectedPaymentDate, r.ActualPaymentDate))
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        var recentFrom = today.AddDays(-RecentUnpaidDays);
        var (confidence, confidenceLabel) = ConfidenceOf(total);

        return new CompanyStatsDto
        {
            TotalCount = total,
            NormalCount = normal,
            DelayedCount = CountOf(PaymentStatus.DELAYED),
            PartialCount = CountOf(PaymentStatus.PARTIAL),
            UnpaidCount = CountOf(PaymentStatus.UNPAID),
            DisputeCount = CountOf(PaymentStatus.DISPUTE),
            ScheduledCount = scheduled,
            SettledCount = settled,
            NormalRate = settled == 0 ? null : Math.Round(normal * 100m / settled, 2, MidpointRounding.AwayFromZero),
            AveragePromisedDays = Average(promised),
            AverageActualDays = Average(actual),
            AverageDelayDays = Average(delays),
            MaxDelayDays = delays.Count == 0 ? null : delays.Max(),
            RecentUnpaidCount = all.Count(r => r.PaymentStatus == PaymentStatus.UNPAID && r.TransportDate >= recentFrom),
            LastTransactionDate = inPeriod.Count == 0 ? null : inPeriod.Max(r => r.TransportDate),
            Confidence = confidence,
            ConfidenceLabel = confidenceLabel,
            PeriodDays = periodDays,
            PeriodLabel = periodDays is { } p ? $"최근 {p}일" : "전체",
        };
    }

    /// <summary>지연일 — max(0, 실제 지급일 − 예정일). 둘 중 하나가 없으면 null.</summary>
    public static int? DelayDays(DateOnly? expected, DateOnly? actual) =>
        expected is { } e && actual is { } a ? Math.Max(0, a.DayNumber - e.DayNumber) : null;

    /// <summary>
    /// 데이터 규모(설계안 27). 거래 2건의 100% 와 2,000건의 85% 를 같게 보이지 않게 한다.
    /// </summary>
    public static (Confidence, string) ConfidenceOf(int count) => count switch
    {
        0 => (Confidence.NONE, "거래 경험 없음"),
        < 5 => (Confidence.LOW, "거래 경험 1~4건"),
        < 20 => (Confidence.MEDIUM, "거래 경험 5~19건"),
        _ => (Confidence.HIGH, "거래 경험 20건 이상"),
    };

    /// <summary>계약의 period 값(30·90·180·365·all)을 읽는다. 비었으면 90.</summary>
    public static bool TryParsePeriod(string? raw, out int? periodDays)
    {
        periodDays = 90;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (raw.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            periodDays = null;
            return true;
        }
        if (int.TryParse(raw, out var d) && StandardPeriods.Contains(d))
        {
            periodDays = d;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 월별 추이(관리자 통계). 운송일의 달로 묶고, 거래가 없는 달도 0 으로 채운다 —
    /// 빈 달을 빼면 그래프가 그 달을 건너뛰어 추이가 거짓말을 한다.
    /// </summary>
    public static List<MonthlyStatDto> Monthly(IEnumerable<StatRow> rows, int months, DateOnly today)
    {
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var byMonth = rows.Where(r => r.TransportDate >= first)
            .ToLookup(r => new DateOnly(r.TransportDate.Year, r.TransportDate.Month, 1));

        return Enumerable.Range(0, months).Select(i =>
        {
            var month = first.AddMonths(i);
            var g = byMonth[month].ToList();
            int CountOf(PaymentStatus s) => g.Count(r => r.PaymentStatus == s);
            return new MonthlyStatDto(
                $"{month:yyyy-MM}",
                g.Count,
                g.Sum(r => r.Amount),
                CountOf(PaymentStatus.PAID),
                CountOf(PaymentStatus.DELAYED),
                CountOf(PaymentStatus.PARTIAL),
                CountOf(PaymentStatus.UNPAID),
                CountOf(PaymentStatus.DISPUTE));
        }).ToList();
    }

    /// <summary>
    /// 지연·미지급이 많은 거래처(관리자 통계). 회사별 전체 기간 통계를 <see cref="Compute"/> 로 낸 뒤
    /// 지연+미지급 건수, 평균 지연일 순으로 줄 세운다. 지연·미지급이 없는 회사는 싣지 않는다.
    /// </summary>
    public static List<(long CompanyId, CompanyStatsDto Stats)> TopDelay(IEnumerable<StatRow> rows, int take, DateOnly today) =>
        rows.GroupBy(r => r.CompanyId)
            .Select(g => (CompanyId: g.Key, Stats: Compute(g.ToList(), null, today)))
            .Where(x => x.Stats.DelayedCount + x.Stats.UnpaidCount > 0)
            .OrderByDescending(x => x.Stats.DelayedCount + x.Stats.UnpaidCount)
            .ThenByDescending(x => x.Stats.AverageDelayDays ?? 0)
            .ThenBy(x => x.CompanyId)
            .Take(take)
            .ToList();

    /// <summary>못 받은 금액 — 다 받은 것(PAID · DELAYED)은 0. MyTransaction.outstanding 과 같은 식.</summary>
    /// 미수금 · 대시보드 · 거래 화면이 모두 이 식을 쓴다.
    public static decimal Outstanding(PaymentStatus status, decimal amount, decimal paidAmount) =>
        status is PaymentStatus.PAID or PaymentStatus.DELAYED ? 0m : Math.Max(0m, amount - paidAmount);

    public static decimal Outstanding(StatRow r) => Outstanding(r.PaymentStatus, r.Amount, r.PaidAmount);

    // 일수 평균은 소수 첫째 자리까지 — 「평균 32.4일」 이상의 정밀도는 읽는 사람에게 뜻이 없다.
    private static decimal? Average(List<int> values) =>
        values.Count == 0 ? null : Math.Round((decimal)values.Sum() / values.Count, 1, MidpointRounding.AwayFromZero);
}
