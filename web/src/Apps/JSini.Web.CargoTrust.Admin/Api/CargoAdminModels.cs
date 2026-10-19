using System.Text.Json;

namespace JSini.Web.CargoTrust.Admin.Api;

// 운송관리 관리자 화면의 자료. 정본은 docs/cargotrust/05-api-design.md 의 「관리자 엔드포인트」절이다.
// 전부 class 에 set 을 열어 둔다(편집 창이 기본 생성자로 베낀다). 코드값은 문자열, 날짜는 DateTime 으로 받는다.

/// <summary>대시보드 — <c>GET admin/dashboard</c>.</summary>
public sealed class AdminDashboard
{
    public int CompanyCount { get; set; }
    public int UserCount { get; set; }
    public int TransactionCount { get; set; }
    public int TransactionsLast30 { get; set; }
    public int FlaggedCount { get; set; }
    public int OpenReports { get; set; }
    public int OpenDisputes { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public List<AdminStatusCount> StatusCounts { get; set; } = [];
    public List<AdminAuditEntry> RecentAudit { get; set; } = [];
}

/// <summary>결제 상태별 건수 한 줄.</summary>
public sealed class AdminStatusCount
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// 거래처 — <c>CompanyInfo</c> 에 관리자 몫이 더해진 것. 사업자번호를 가리지 않는다.
/// </summary>
public sealed class AdminCompany
{
    public long CompanyId { get; set; }
    public string? BusinessNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? CeoName { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? Phone { get; set; }
    public string? BusinessType { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? AdminMemo { get; set; }
    public int TransactionCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 고르개에 적는 이름(「○○물류 · 123-45-67890」). 이름만 적으면 동명 업체를
    /// 가를 수 없다 — 설계안 13.1 이 꼽은 문제 그대로다.
    /// </summary>
    public string PickText => string.IsNullOrWhiteSpace(BusinessNumber)
        ? CompanyName ?? string.Empty
        : $"{CompanyName} · {BusinessNumber}";
}

/// <summary>거래처 등록·수정 — <c>AdminCompanySave</c>.</summary>
public sealed class AdminCompanySave
{
    public string? BusinessNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? CeoName { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? Phone { get; set; }
    public string? BusinessType { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? AdminMemo { get; set; }
}

/// <summary>
/// 거래 — 사용자 쪽 <c>MyTransaction</c> 에 등록자 · 검증 · 관리자 몫이 더해진 것.
/// </summary>
public sealed class AdminTransaction
{
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? BusinessNumber { get; set; }
    public DateTime? TransportDate { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TransportType { get; set; }
    public string? DispatchChannel { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Outstanding { get; set; }
    public DateTime? ExpectedPaymentDate { get; set; }
    public DateTime? ActualPaymentDate { get; set; }
    public string? PaymentStatus { get; set; }
    public int? DelayDays { get; set; }
    public int? OverdueDays { get; set; }
    public string? Memo { get; set; }
    public string? ReviewStatus { get; set; }
    public bool HasReview { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public long UserId { get; set; }
    public string? UserName { get; set; }
    public string? ExternalUserId { get; set; }
    public string? FlagReason { get; set; }
    public string? AdminMemo { get; set; }
    public bool IsDeleted { get; set; }
    public int ReportCount { get; set; }
    public int DisputeCount { get; set; }

    /// <summary>구간을 한 칸에 — 표가 넓어 출발 · 도착을 따로 두면 가로로 굴러야 한다.</summary>
    public string Route => $"{Origin} → {Destination}";
}

/// <summary>
/// 거래 검증 결과를 고친다 — <c>AdminTransactionUpdate</c>.
/// <see cref="PaymentStatus"/> 가 <c>null</c> 이면 서버가 결제 상태를 건드리지 않는다.
/// </summary>
public sealed class AdminTransactionUpdate
{
    public string? ReviewStatus { get; set; }
    public string? PaymentStatus { get; set; }
    public string? AdminMemo { get; set; }
}

/// <summary>결제 기록 — <c>PaymentRecord</c> + 어느 거래의 것인지.</summary>
public sealed class AdminPayment
{
    public long PaymentId { get; set; }
    public DateTime? PaidDate { get; set; }
    public decimal PaidAmount { get; set; }
    public string? ResultStatus { get; set; }
    public int? DelayDays { get; set; }
    public string? Memo { get; set; }
    public DateTime? CreatedAt { get; set; }

    public long TransactionId { get; set; }
    public string? CompanyName { get; set; }
    public string? UserName { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>후기 — <c>Review</c> + 어느 회사 · 누가.</summary>
public sealed class AdminReview
{
    public long ReviewId { get; set; }
    public long TransactionId { get; set; }
    public string? Content { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public long CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? UserName { get; set; }
    public DateTime? TransportDate { get; set; }
    public int ReportCount { get; set; }
}

/// <summary>신고 — <c>Report</c> + 신고자.</summary>
public sealed class AdminReport
{
    public long ReportId { get; set; }
    public string? TargetType { get; set; }
    public long TargetId { get; set; }
    public string? TargetSummary { get; set; }
    public string? Reason { get; set; }
    public string? Content { get; set; }
    public string? Status { get; set; }
    public string? Resolution { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public string? ReporterName { get; set; }
    public string? ReporterExternalId { get; set; }
}

/// <summary>이의제기 — <c>Dispute</c> + 신청자 · 그 거래를 등록한 사람.</summary>
public sealed class AdminDispute
{
    public long DisputeId { get; set; }
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DateTime? TransportDate { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string? Content { get; set; }
    public string? Status { get; set; }
    public string? Resolution { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public string? RequesterName { get; set; }
    public string? TransactionOwnerName { get; set; }
}

/// <summary>
/// 신고 · 이의제기 처리 — <c>AdminResolve</c>.
/// <see cref="HideTarget"/> 가 참이면 서버가 대상을 숨긴다(이의제기는 인용일 때만).
/// </summary>
public sealed class AdminResolve
{
    public string? Status { get; set; }
    public string? Resolution { get; set; }
    public bool HideTarget { get; set; }
}

/// <summary>운송관리 사용자.</summary>
public sealed class AdminUser
{
    public long UserId { get; set; }
    public string? ExternalUserId { get; set; }
    public string? DisplayName { get; set; }
    public string? UserType { get; set; }
    public long? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? Status { get; set; }
    public string? AdminMemo { get; set; }
    public int TransactionCount { get; set; }
    public int ReportCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

/// <summary>사용자 유형 · 연결 회사 · 차단 · 메모를 고친다 — <c>AdminUserUpdate</c>.</summary>
public sealed class AdminUserUpdate
{
    public string? UserType { get; set; }
    public long? CompanyId { get; set; }
    public string? Status { get; set; }
    public string? AdminMemo { get; set; }
}

/// <summary>통계 — <c>GET admin/statistics</c>.</summary>
public sealed class AdminStatistics
{
    public List<AdminMonthly> Monthly { get; set; } = [];
    public List<AdminDelayCompany> TopDelayCompanies { get; set; } = [];
    public List<AdminFlaggedUser> FlaggedUsers { get; set; } = [];
}

/// <summary>한 달 치 거래.</summary>
public sealed class AdminMonthly
{
    /// <summary>「2026-09」.</summary>
    public string Month { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
    public int NormalCount { get; set; }
    public int DelayedCount { get; set; }
    public int PartialCount { get; set; }
    public int UnpaidCount { get; set; }
    public int DisputeCount { get; set; }
}

/// <summary>지연 · 미지급 건수가 많은 거래처 한 줄.</summary>
public sealed class AdminDelayCompany
{
    public long CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? BusinessNumber { get; set; }
    public int TotalCount { get; set; }
    public int DelayedCount { get; set; }
    public int UnpaidCount { get; set; }
    public double? AverageDelayDays { get; set; }
}

/// <summary>의심(FLAGGED)으로 걸린 거래를 등록한 사용자 한 줄.</summary>
public sealed class AdminFlaggedUser
{
    public long UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? ExternalUserId { get; set; }
    public int FlaggedCount { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>감사 기록 한 줄.</summary>
public sealed class AdminAuditEntry
{
    public long AuditId { get; set; }
    public long? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? Action { get; set; }
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    /// <summary>
    /// 바뀌기 전 값. 계약서는 JSON 문자열이라 했지만 JsonElement 로 받는다 — 객체로 와도 목록이 죽지 않게.
    /// 펴는 것은 CargoAdminJson 이 한다.
    /// </summary>
    public JsonElement? BeforeData { get; set; }

    /// <summary>바뀐 뒤. <see cref="BeforeData"/> 와 같은 이유로 JsonElement 다.</summary>
    public JsonElement? AfterData { get; set; }

    public DateTime? CreatedAt { get; set; }
}
