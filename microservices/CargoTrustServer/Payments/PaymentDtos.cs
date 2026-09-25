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

/// <summary>
/// BulkPaymentRequest — 여러 거래를 한 번에 처리한다(거래처 상세의 「내 미처리 거래」).
///
/// <para>
/// 금액을 받지 않는 것이 단건(<see cref="PaymentRequest"/>)과의 차이다. 한 번에
/// 처리하는 자리는 **한 회사가 여러 건을 묶어 입금한** 경우라, 건마다 남은 금액
/// 전액을 받은 것으로 적는다. 나눠 받은 건은 단건으로 적는다.
/// </para>
/// </summary>
public class BulkPaymentRequest
{
    public List<long>? TransactionIds { get; set; }
    public DateOnly? PaidDate { get; set; }
    /// <summary>null(받은 것을 적는다) · UNPAID · DISPUTE</summary>
    public string? Result { get; set; }
    public string? Memo { get; set; }
}

/// <summary>한 번에 처리하다 걸러진 한 건. 무엇이 안 됐는지 화면이 그대로 띄운다.</summary>
public record BulkPaymentFailureDto(long TransactionId, string Message);

/// <summary>
/// BulkPaymentResult — 된 것과 안 된 것을 함께 돌려준다.
///
/// <para>
/// 한 건이라도 걸리면 전부 물리는 대신, 되는 것을 처리하고 안 된 것은 이유와 함께
/// 돌려준다. 여기서 걸리는 것은 「이미 다 받은 거래」처럼 **다른 곳에서 먼저
/// 처리된 것**이 대부분이라, 통째로 물리면 사람이 고를 것을 하나씩 빼 가며
/// 다시 눌러야 한다.
/// </para>
/// </summary>
public record BulkPaymentResultDto(
    List<Transactions.MyTransactionDto> Updated,
    List<BulkPaymentFailureDto> Failed);
