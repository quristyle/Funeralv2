using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Disputes;
using CargoTrustServer.Payments;
using CargoTrustServer.Reviews;
using CargoTrustServer.Statistics;

namespace CargoTrustServer.Transactions;

/// <summary>
/// PublicTransaction — 등록자는 **가린 이름**으로 싣는다(<see cref="PersonName.Display"/>).
/// 출발·도착은 첫 낱말만 간다.
/// </summary>
public record PublicTransactionDto(
    long TransactionId,
    DateOnly TransportDate,
    string? OriginRegion,
    string? DestinationRegion,
    string? TransportType,
    decimal Amount,
    DateOnly? ExpectedPaymentDate,
    DateOnly? ActualPaymentDate,
    PaymentStatus PaymentStatus,
    int? DelayDays,
    string RegisteredBy);

/// <summary>MyTransaction — 등록자 본인(과 관리자)이 보는 거래.</summary>
public class MyTransactionDto
{
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string BusinessNumber { get; set; } = string.Empty;
    public DateOnly TransportDate { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TransportType { get; set; }
    public string? DispatchChannel { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Outstanding { get; set; }
    public DateOnly? ExpectedPaymentDate { get; set; }
    public DateOnly? ActualPaymentDate { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public int? DelayDays { get; set; }
    public int? OverdueDays { get; set; }
    public string? Memo { get; set; }
    public ReviewStatus ReviewStatus { get; set; }
    public bool HasReview { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// UnpaidTransaction — 미지급 거래 목록의 한 줄(<see cref="UnpaidEndpoints"/>).
///
/// <para>
/// <see cref="MyTransactionDto"/> 와 비슷해 보이지만 **남의 거래가 섞인 목록**이다.
/// 그래서 메모·검증 상태처럼 등록자만 볼 것은 없고, 사업자번호와 등록자 이름은
/// 가린 꼴로 온다(설계안 29). 출발·도착도 첫 낱말만이다.
/// </para>
/// </summary>
public record UnpaidTransactionDto(
    long TransactionId,
    long CompanyId,
    string CompanyName,
    string BusinessNumber,
    DateOnly TransportDate,
    string? OriginRegion,
    string? DestinationRegion,
    string? TransportType,
    decimal Amount,
    decimal PaidAmount,
    decimal Outstanding,
    DateOnly? ExpectedPaymentDate,
    int? OverdueDays,
    string RegisteredBy,
    bool Mine,
    DateTimeOffset CreatedAt);

/// <summary>UnpaidSummary — 목록에 실제로 실린 줄만 센 값이다(상한에 잘리면 그만큼만).</summary>
public record UnpaidSummaryDto(
    int Count,
    decimal Amount,
    int CompanyCount,
    int RegistrantCount,
    int MineCount);

/// <summary>UnpaidList</summary>
public record UnpaidListDto(UnpaidSummaryDto Summary, List<UnpaidTransactionDto> Items);

/// <summary>TransactionSaveRequest</summary>
public class TransactionSaveRequest
{
    public long? CompanyId { get; set; }
    public DateOnly? TransportDate { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TransportType { get; set; }
    public string? DispatchChannel { get; set; }
    public decimal? Amount { get; set; }
    public DateOnly? ExpectedPaymentDate { get; set; }
    public string? Memo { get; set; }
}

/// <summary>TransactionDetail</summary>
public record TransactionDetailDto(
    MyTransactionDto Transaction,
    List<PaymentRecordDto> Payments,
    ReviewDto? Review,
    List<DisputeDto> Disputes);

public static class TransactionMap
{
    /// <summary>
    /// <paramref name="t"/> 는 <see cref="CargoTransaction.User"/> 가 실려 있어야 한다
    /// (<c>Include(t =&gt; t.User)</c>). 안 실으면 등록자가 「이름 없음」으로 나간다.
    /// </summary>
    public static PublicTransactionDto Public(CargoTransaction t, bool isAdmin) => new(
        t.TransactionId,
        t.TransportDate,
        FirstWord(t.Origin),
        FirstWord(t.Destination),
        t.TransportType,
        t.Amount,
        t.ExpectedPaymentDate,
        t.ActualPaymentDate,
        t.PaymentStatus,
        CompanyStatsService.DelayDays(t.ExpectedPaymentDate, t.ActualPaymentDate),
        PersonName.Display(t.User?.DisplayName, isAdmin));

    /// <summary><paramref name="t"/> 는 Company 가 실려 있어야 한다.</summary>
    public static T Mine<T>(CargoTransaction t, bool hasReview, bool isAdmin, DateOnly today) where T : MyTransactionDto, new() => new()
    {
        TransactionId = t.TransactionId,
        CompanyId = t.CompanyId,
        CompanyName = t.Company.CompanyName,
        BusinessNumber = Common.BusinessNumber.Display(t.Company.BusinessNumber, isAdmin),
        TransportDate = t.TransportDate,
        Origin = t.Origin,
        Destination = t.Destination,
        TransportType = t.TransportType,
        DispatchChannel = t.DispatchChannel,
        Amount = t.Amount,
        PaidAmount = t.PaidAmount,
        Outstanding = Outstanding(t),
        ExpectedPaymentDate = t.ExpectedPaymentDate,
        ActualPaymentDate = t.ActualPaymentDate,
        PaymentStatus = t.PaymentStatus,
        DelayDays = CompanyStatsService.DelayDays(t.ExpectedPaymentDate, t.ActualPaymentDate),
        OverdueDays = OverdueDays(t, today),
        Memo = t.Memo,
        ReviewStatus = t.ReviewStatus,
        HasReview = hasReview,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };

    public static MyTransactionDto Mine(CargoTransaction t, bool hasReview, bool isAdmin, DateOnly today) =>
        Mine<MyTransactionDto>(t, hasReview, isAdmin, today);

    /// <summary>다 받은 것(PAID · DELAYED)인가.</summary>
    public static bool IsSettledInFull(PaymentStatus s) => s is PaymentStatus.PAID or PaymentStatus.DELAYED;

    /// <summary>못 받은 금액 = 운송료 − 받은 금액. 다 받은 거래는 0.</summary>
    public static decimal Outstanding(CargoTransaction t) =>
        CompanyStatsService.Outstanding(t.PaymentStatus, t.Amount, t.PaidAmount);

    /// <summary>아직 다 안 받았고 예정일이 지났으면 오늘 − 예정일, 아니면 null.</summary>
    public static int? OverdueDays(CargoTransaction t, DateOnly today) =>
        !IsSettledInFull(t.PaymentStatus) && t.ExpectedPaymentDate is { } e && e < today
            ? today.DayNumber - e.DayNumber
            : null;

    /// <summary>「경기 평택시 …」→「경기」. 공개 화면에 상세 주소를 싣지 않는다.</summary>
    public static string? FirstWord(string? text) => Companies.CompanyInput.FirstWord(text);
}
