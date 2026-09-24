using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Disputes;
using CargoTrustServer.Payments;
using CargoTrustServer.Reviews;
using CargoTrustServer.Statistics;

namespace CargoTrustServer.Transactions;

/// <summary>PublicTransaction — 누가 등록했는지는 싣지 않는다.</summary>
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
    int? DelayDays);

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
    public static PublicTransactionDto Public(CargoTransaction t) => new(
        t.TransactionId,
        t.TransportDate,
        FirstWord(t.Origin),
        FirstWord(t.Destination),
        t.TransportType,
        t.Amount,
        t.ExpectedPaymentDate,
        t.ActualPaymentDate,
        t.PaymentStatus,
        CompanyStatsService.DelayDays(t.ExpectedPaymentDate, t.ActualPaymentDate));

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
