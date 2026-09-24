using CargoTrustServer.Common;

namespace CargoTrustServer.Data;

// 테이블 정본은 deploy/sql/cargotrust-schema-2026-09-24.sql 이다.
// 속성 이름은 열 이름의 PascalCase — DbContext 가 snake_case 로 되돌려 붙인다.
// 열을 여기서 먼저 늘리지 않는다. SQL 파일 아래에 ALTER 를 덧붙인 뒤에 따라 늘린다.

/// <summary>거래처. 사업자등록번호(숫자 10자리)가 식별자다.</summary>
public class Company
{
    public long CompanyId { get; set; }
    public string BusinessNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CeoName { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? Phone { get; set; }
    public string? BusinessType { get; set; }
    public CompanyStatus Status { get; set; } = CompanyStatus.ACTIVE;
    public string? AdminMemo { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>사용자. 포털 계정(X-User-Id)에 딸린 줄이고, 처음 부를 때 서버가 만든다.</summary>
public class AppUser
{
    public long UserId { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public UserType UserType { get; set; } = UserType.DRIVER;
    /// <summary>운송사 사용자가 대표하는 거래처. 이의제기는 이 회사의 거래에만 건다.</summary>
    public long? CompanyId { get; set; }
    public Company? Company { get; set; }
    public UserStatus Status { get; set; } = UserStatus.ACTIVE;
    public string? AdminMemo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }
}

/// <summary>거래. 지금의 결제 상태를 들고 있고, 결제 이력은 <see cref="PaymentRecord"/> 에 쌓인다.</summary>
public class CargoTransaction
{
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateOnly TransportDate { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TransportType { get; set; }
    public string? DispatchChannel { get; set; }

    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }

    public DateOnly? ExpectedPaymentDate { get; set; }
    public DateOnly? ActualPaymentDate { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.SCHEDULED;
    public string? Memo { get; set; }

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.NORMAL;
    public string? FlagReason { get; set; }
    public string? AdminMemo { get; set; }
    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>결제 등록 한 번이 한 줄.</summary>
public class PaymentRecord
{
    public long PaymentId { get; set; }
    public long TransactionId { get; set; }
    public long UserId { get; set; }
    public DateOnly? PaidDate { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentStatus ResultStatus { get; set; }
    public int? DelayDays { get; set; }
    public string? Memo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>거래 후기 — 거래 하나에 하나.</summary>
public class TransactionReview
{
    public long ReviewId { get; set; }
    public long TransactionId { get; set; }
    public CargoTransaction Transaction { get; set; } = null!;
    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public ReviewVisibility Status { get; set; } = ReviewVisibility.VISIBLE;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>업체 이의제기.</summary>
public class TransactionDispute
{
    public long DisputeId { get; set; }
    public long TransactionId { get; set; }
    public CargoTransaction Transaction { get; set; } = null!;
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public long RequesterUserId { get; set; }
    public AppUser Requester { get; set; } = null!;
    public DisputeReason Reason { get; set; }
    public string? Content { get; set; }
    public DisputeStatus Status { get; set; } = DisputeStatus.RECEIVED;
    public string? Resolution { get; set; }
    public long? ResolvedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>신고. 대상은 (target_type, target_id) 로 가리킨다 — FK 가 없다.</summary>
public class Report
{
    public long ReportId { get; set; }
    public ReportTarget TargetType { get; set; }
    public long TargetId { get; set; }
    public long ReporterUserId { get; set; }
    public AppUser Reporter { get; set; } = null!;
    public ReportReason Reason { get; set; }
    public string? Content { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.RECEIVED;
    public string? Resolution { get; set; }
    public long? ResolvedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>최근 본 거래처 — 한 사람이 한 회사를 여러 번 봐도 한 줄.</summary>
public class CompanyView
{
    public long UserId { get; set; }
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public DateTimeOffset ViewedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>감사 기록. 전·후는 JSONB 로 둔다.</summary>
public class AuditLog
{
    public long AuditId { get; set; }
    public long? ActorUserId { get; set; }
    public string? ActorExternal { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
