using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Companies;
using CargoTrustServer.Disputes;
using CargoTrustServer.Payments;
using CargoTrustServer.Reports;
using CargoTrustServer.Reviews;
using CargoTrustServer.Statistics;
using CargoTrustServer.Transactions;

namespace CargoTrustServer.Admin;

// 관리자 응답에서는 사업자번호를 가리지 않는다.
// 사용자 DTO 를 물려받아 칸을 더한다 — 같은 거래가 두 화면에서 다른 이름으로 나가지 않게 한다.

public record StatusCountDto(PaymentStatus Status, int Count);

/// <summary>AdminDashboard</summary>
public record AdminDashboardDto(
    int CompanyCount,
    int UserCount,
    int TransactionCount,
    int TransactionsLast30,
    int FlaggedCount,
    int OpenReports,
    int OpenDisputes,
    decimal TotalAmount,
    decimal OutstandingAmount,
    List<StatusCountDto> StatusCounts,
    List<AuditEntryDto> RecentAudit);

/// <summary>AdminCompany = CompanyInfo + 관리 칸</summary>
public class AdminCompanyDto : CompanyInfoDto
{
    public string? AdminMemo { get; set; }
    public int TransactionCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>AdminCompanySave = CompanyCreateRequest + status · adminMemo</summary>
public class AdminCompanySave : CompanyCreateRequest
{
    public string? Status { get; set; }
    public string? AdminMemo { get; set; }
}

/// <summary>AdminTransaction = MyTransaction + 관리 칸</summary>
public class AdminTransactionDto : MyTransactionDto
{
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string? FlagReason { get; set; }
    public string? AdminMemo { get; set; }
    public bool IsDeleted { get; set; }
    public int ReportCount { get; set; }
    public int DisputeCount { get; set; }
}

/// <summary>AdminTransactionUpdate — null 인 칸은 바꾸지 않는다.</summary>
public class AdminTransactionUpdate
{
    public string? ReviewStatus { get; set; }
    public string? PaymentStatus { get; set; }
    public string? AdminMemo { get; set; }
}

/// <summary>AdminPayment = PaymentRecord + 거래 칸</summary>
public class AdminPaymentDto : PaymentRecordDto
{
    public long TransactionId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>AdminReview = Review + 거래 칸</summary>
public class AdminReviewDto : ReviewDto
{
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public DateOnly TransportDate { get; set; }
    public int ReportCount { get; set; }
}

/// <summary>후기 공개 여부 — <c>{status}</c></summary>
public class ReviewStatusRequest
{
    public string? Status { get; set; }
}

/// <summary>AdminReport = Report + 신고자</summary>
public class AdminReportDto : ReportDto
{
    public string? ReporterName { get; set; }
    public string ReporterExternalId { get; set; } = string.Empty;
}

/// <summary>AdminDispute = Dispute + 신청자 · 거래 등록자</summary>
public class AdminDisputeDto : DisputeDto
{
    public string? RequesterName { get; set; }
    public string? TransactionOwnerName { get; set; }
}

/// <summary>AdminResolve — 신고 · 이의제기 처리</summary>
public class AdminResolve
{
    public string? Status { get; set; }
    public string? Resolution { get; set; }
    public bool HideTarget { get; set; }
}

/// <summary>AdminUser</summary>
public record AdminUserDto(
    long UserId,
    string ExternalUserId,
    string? DisplayName,
    UserType UserType,
    long? CompanyId,
    string? CompanyName,
    UserStatus Status,
    string? AdminMemo,
    int TransactionCount,
    int ReportCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt);

/// <summary>
/// AdminUserUpdate. userType · status 는 null 이면 그대로 둔다.
/// companyId · adminMemo 는 **보낸 값으로 바꾼다**(null = 비운다) — 운송사의 회사 연결을 끊는 길이 있어야 한다.
/// </summary>
public class AdminUserUpdate
{
    public string? UserType { get; set; }
    public long? CompanyId { get; set; }
    public string? Status { get; set; }
    public string? AdminMemo { get; set; }
}

/// <summary>AdminStatistics</summary>
public record AdminStatisticsDto(
    List<MonthlyStatDto> Monthly,
    List<DelayCompanyDto> TopDelayCompanies,
    List<FlaggedUserDto> FlaggedUsers);

public record DelayCompanyDto(
    long CompanyId,
    string CompanyName,
    string BusinessNumber,
    int TotalCount,
    int DelayedCount,
    int UnpaidCount,
    decimal? AverageDelayDays);

public record FlaggedUserDto(
    long UserId,
    string? DisplayName,
    string ExternalUserId,
    int FlaggedCount,
    int TransactionCount);
