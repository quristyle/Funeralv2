using CargoTrustServer.Common;
using CargoTrustServer.Data;

namespace CargoTrustServer.Reports;

/// <summary>Report</summary>
public class ReportDto
{
    public long ReportId { get; set; }
    public ReportTarget TargetType { get; set; }
    public long TargetId { get; set; }
    /// <summary>대상을 한 줄로 — 목록에서 무엇을 신고했는지 알아보게 한다.</summary>
    public string? TargetSummary { get; set; }
    public ReportReason Reason { get; set; }
    public string? Content { get; set; }
    public ReportStatus Status { get; set; }
    public string? Resolution { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>ReportRequest</summary>
public class ReportRequest
{
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public string? Reason { get; set; }
    public string? Content { get; set; }
}

public static class ReportMap
{
    /// <summary>처리 안 된(접수 · 검토중) 신고인가 — 같은 대상의 중복 신고를 막는 기준.</summary>
    public static bool IsOpen(ReportStatus s) => s is ReportStatus.RECEIVED or ReportStatus.REVIEWING;

    public static T Report<T>(Report r, string? targetSummary) where T : ReportDto, new() => new()
    {
        ReportId = r.ReportId,
        TargetType = r.TargetType,
        TargetId = r.TargetId,
        TargetSummary = targetSummary,
        Reason = r.Reason,
        Content = r.Content,
        Status = r.Status,
        Resolution = r.Resolution,
        CreatedAt = r.CreatedAt,
        ResolvedAt = r.ResolvedAt,
    };
}
