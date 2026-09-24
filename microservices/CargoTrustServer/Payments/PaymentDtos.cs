using CargoTrustServer.Common;
using CargoTrustServer.Data;

namespace CargoTrustServer.Payments;

/// <summary>PaymentRequest</summary>
public class PaymentRequest
{
    public DateOnly? PaidDate { get; set; }
    public decimal? PaidAmount { get; set; }
    /// <summary>null(받은 것을 적는다) · UNPAID · DISPUTE</summary>
    public string? Result { get; set; }
    public string? Memo { get; set; }
}

/// <summary>PaymentRecord</summary>
public class PaymentRecordDto
{
    public long PaymentId { get; set; }
    public DateOnly? PaidDate { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentStatus ResultStatus { get; set; }
    public int? DelayDays { get; set; }
    public string? Memo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>ReceivableSummary — 금액(원)</summary>
public record ReceivableSummaryDto(
    decimal Total,
    decimal Scheduled,
    decimal Delayed,
    decimal Unpaid,
    decimal Partial,
    decimal Dispute,
    int Count);

/// <summary>ReceivableItem</summary>
public record ReceivableItemDto(
    long TransactionId,
    long CompanyId,
    string CompanyName,
    DateOnly TransportDate,
    decimal Amount,
    decimal PaidAmount,
    decimal Outstanding,
    DateOnly? ExpectedPaymentDate,
    int? OverdueDays,
    PaymentStatus PaymentStatus,
    string Bucket);

/// <summary>Receivables</summary>
public record ReceivablesDto(ReceivableSummaryDto Summary, List<ReceivableItemDto> Items);

public static class PaymentMap
{
    public static T Record<T>(PaymentRecord p) where T : PaymentRecordDto, new() => new()
    {
        PaymentId = p.PaymentId,
        PaidDate = p.PaidDate,
        PaidAmount = p.PaidAmount,
        ResultStatus = p.ResultStatus,
        DelayDays = p.DelayDays,
        Memo = p.Memo,
        CreatedAt = p.CreatedAt,
    };

    public static PaymentRecordDto Record(PaymentRecord p) => Record<PaymentRecordDto>(p);
}
