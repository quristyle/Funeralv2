using CargoTrustServer.Common;
using CargoTrustServer.Data;

namespace CargoTrustServer.Disputes;

/// <summary>Dispute</summary>
public class DisputeDto
{
    public long DisputeId { get; set; }
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public DateOnly TransportDate { get; set; }
    public decimal Amount { get; set; }
    public DisputeReason Reason { get; set; }
    public string? Content { get; set; }
    public DisputeStatus Status { get; set; }
    public string? Resolution { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>DisputeRequest</summary>
public class DisputeRequest
{
    public long? TransactionId { get; set; }
    public string? Reason { get; set; }
    public string? Content { get; set; }
}

public static class DisputeMap
{
    /// <summary>처리 중(접수 · 검토중)인가 — 같은 거래에 둘째 이의제기를 막는 기준.</summary>
    public static bool IsOpen(DisputeStatus s) => s is DisputeStatus.RECEIVED or DisputeStatus.REVIEWING;

    /// <summary><paramref name="d"/> 는 Transaction · Company 가 실려 있어야 한다.</summary>
    public static T Dispute<T>(TransactionDispute d) where T : DisputeDto, new() => new()
    {
        DisputeId = d.DisputeId,
        TransactionId = d.TransactionId,
        CompanyId = d.CompanyId,
        CompanyName = d.Company.CompanyName,
        TransportDate = d.Transaction.TransportDate,
        Amount = d.Transaction.Amount,
        Reason = d.Reason,
        Content = d.Content,
        Status = d.Status,
        Resolution = d.Resolution,
        CreatedAt = d.CreatedAt,
        ResolvedAt = d.ResolvedAt,
    };

    public static DisputeDto Dispute(TransactionDispute d) => Dispute<DisputeDto>(d);
}
