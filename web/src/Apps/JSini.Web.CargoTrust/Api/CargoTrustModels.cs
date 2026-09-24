namespace JSini.Web.CargoTrust.Api;

// ────────────────────────────────────────────────────────────────
// CargoTrustServer 응답·요청 DTO 모음 — 차주(사용자) 쪽.
//
// 정본은 docs/cargotrust/05-api-design.md 「자료 모양」이다. 필드 이름을 그
// 문서 그대로 옮겼다(camelCase 는 GatewayClient 가 대소문자를 가리지 않고 받는다).
// 한쪽만 고치지 않는다 — 그 문서가 백엔드와 두 MFE 의 약속이다.
//
// [관리자 모듈과 공유하지 않는다]
//
// 관리자 화면(JSini.Web.CargoTrust.Admin)도 비슷한 모양을 쓰지만 복제한다.
// 두 모듈이 쓰면 복제가 규칙이고(web/CLAUDE.md), 여기서는 그보다 강한 이유가
// 있다 — 관리자 DTO 에는 등록자 이름·관리 메모·가리지 않은 사업자번호가 실린다.
// 한 벌로 합쳐 두면 차주 화면이 그 칸을 그릴 수 있게 되고, 가려야 할 값이
// 한 줄 실수로 드러난다(설계안 29).
//
// [날짜는 DateOnly 다]
//
// 서버가 "yyyy-MM-dd" 로 주고 **그 꼴로만 받는다**(DateOnly). DateTime 으로 두면
// 보낼 때 "2026-09-24T00:00:00" 이 나가 서버가 400 을 낸다. 편집기
// (DxDateEdit)가 DateTime? 을 쓰는 자리는 폼 모델이 따로 옮긴다
// (TransactionForm · PaymentForm).
// ────────────────────────────────────────────────────────────────

/// <summary>거래처 기본정보. 사업자번호는 관리자가 아니면 뒤 5자리가 가려져 온다.</summary>
public sealed class CompanyInfo
{
    public long CompanyId { get; set; }
    public string BusinessNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CeoName { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? Phone { get; set; }
    public string? BusinessType { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// 거래처 통계 한 벌. 원본 거래에서 매번 계산한 값이다(설계안 26).
///
/// <para>
/// <see cref="PeriodLabel"/> 과 <see cref="ConfidenceLabel"/> 을 서버가 함께
/// 주는 이유가 있다 — 화면이 숫자 옆에 **기간과 기준을 반드시 함께 적어야**
/// 하는데(설계안 26 마지막 줄), 그 글자를 화면마다 지으면 한 화면은
/// 「최근 90일」, 다른 화면은 「90일」이 되어 같은 숫자가 다른 뜻으로 읽힌다.
/// </para>
/// </summary>
public sealed class CompanyStats
{
    public int TotalCount { get; set; }
    public int NormalCount { get; set; }
    public int DelayedCount { get; set; }
    public int PartialCount { get; set; }
    public int UnpaidCount { get; set; }
    public int DisputeCount { get; set; }
    public int ScheduledCount { get; set; }

    /// <summary>예정을 뺀 건수 — 정상률의 분모.</summary>
    public int SettledCount { get; set; }

    /// <summary>정상률(%). 정산된 거래가 없으면 <c>null</c> — 0% 와 다르다.</summary>
    public decimal? NormalRate { get; set; }

    public double? AveragePromisedDays { get; set; }
    public double? AverageActualDays { get; set; }
    public double? AverageDelayDays { get; set; }
    public int? MaxDelayDays { get; set; }

    /// <summary>최근 90일 미지급 건수. <b>고른 기간과 무관하다</b> — 화면이 그렇게 적는다.</summary>
    public int RecentUnpaidCount { get; set; }

    public DateOnly? LastTransactionDate { get; set; }

    /// <summary><c>NONE</c> · <c>LOW</c> · <c>MEDIUM</c> · <c>HIGH</c>.</summary>
    public string Confidence { get; set; } = "NONE";

    public string? ConfidenceLabel { get; set; }

    /// <summary>30 · 90 · 180 · 365 · 전체면 <c>null</c>.</summary>
    public int? PeriodDays { get; set; }

    public string? PeriodLabel { get; set; }
}

/// <summary>검색 결과 한 칸. 통계는 전체 기간이다.</summary>
public sealed class CompanySummary
{
    public long CompanyId { get; set; }
    public string BusinessNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CeoName { get; set; }
    public string? Region { get; set; }
    public string? BusinessType { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public CompanyStats Stats { get; set; } = new();
}

/// <summary>거래처 상세. <see cref="Periods"/> 는 30·90·180·365·전체 다섯 개로 고정이다.</summary>
public sealed class CompanyDetail
{
    public CompanyInfo Company { get; set; } = new();
    public CompanyStats Stats { get; set; } = new();
    public List<CompanyStats> Periods { get; set; } = [];
    public List<PublicTransaction> RecentTransactions { get; set; } = [];
    public List<PublicTransaction> RecentUnpaid { get; set; } = [];
    public int MyTransactionCount { get; set; }
}

/// <summary>
/// 공개 거래 한 줄. <b>누가 등록했는지는 싣지 않는다</b>(설계안 29) —
/// 출발·도착도 첫 낱말(「경기」)만 온다.
/// </summary>
public sealed class PublicTransaction
{
    public long TransactionId { get; set; }
    public DateOnly? TransportDate { get; set; }
    public string? OriginRegion { get; set; }
    public string? DestinationRegion { get; set; }
    public string? TransportType { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? ExpectedPaymentDate { get; set; }
    public DateOnly? ActualPaymentDate { get; set; }
    public string PaymentStatus { get; set; } = "SCHEDULED";
    public int? DelayDays { get; set; }

    /// <summary>표에 「경기 → 부산」 한 칸으로 싣는다.</summary>
    public string Route => CargoCodes.RouteText(OriginRegion, DestinationRegion);
}

/// <summary>거래처 상세에 뜨는 후기. 거래 요약이 함께 온다 — 후기만 떠 있으면 감정인지 사실인지 가를 수 없다.</summary>
public sealed class PublicReview
{
    public long ReviewId { get; set; }
    public DateOnly? TransportDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = "SCHEDULED";
    public string? Content { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>내가 등록한 거래.</summary>
public sealed class MyTransaction
{
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? BusinessNumber { get; set; }
    public DateOnly? TransportDate { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TransportType { get; set; }
    public string? DispatchChannel { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }

    /// <summary>운송료 − 받은 금액. 정상·지연 지급이면 서버가 0 으로 준다.</summary>
    public decimal Outstanding { get; set; }

    public DateOnly? ExpectedPaymentDate { get; set; }
    public DateOnly? ActualPaymentDate { get; set; }
    public string PaymentStatus { get; set; } = "SCHEDULED";
    public int? DelayDays { get; set; }

    /// <summary>아직 못 받았고 예정일이 지났으면 오늘까지 지난 날수. 아니면 <c>null</c>.</summary>
    public int? OverdueDays { get; set; }

    public string? Memo { get; set; }
    public string ReviewStatus { get; set; } = "NORMAL";
    public bool HasReview { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string Route => CargoCodes.RouteText(Origin, Destination);

    /// <summary>결제 결과를 더 받을 수 있는가. 정상·지연 지급은 이미 다 받은 것이다.</summary>
    public bool CanPay => CargoCodes.IsOpen(PaymentStatus);
}

/// <summary>거래 등록·수정 요청.</summary>
public sealed class TransactionSaveRequest
{
    public long CompanyId { get; set; }
    public DateOnly TransportDate { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TransportType { get; set; }
    public string? DispatchChannel { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? ExpectedPaymentDate { get; set; }
    public string? Memo { get; set; }
}

/// <summary>
/// 결제 결과 등록 요청. <see cref="Result"/> 가 <c>null</c> 이면 「받았다」이고
/// 그때는 받은 날과 금액이 있어야 한다 — 상태(정상·지연·일부)는 서버가 정한다.
/// </summary>
public sealed class PaymentRequest
{
    public DateOnly? PaidDate { get; set; }
    public decimal PaidAmount { get; set; }

    /// <summary><c>null</c> · <c>UNPAID</c> · <c>DISPUTE</c>.</summary>
    public string? Result { get; set; }

    public string? Memo { get; set; }
}

/// <summary>결제 기록 한 줄. 받을 때마다 한 줄씩 쌓인다.</summary>
public sealed class PaymentRecord
{
    public long PaymentId { get; set; }
    public DateOnly? PaidDate { get; set; }
    public decimal PaidAmount { get; set; }
    public string? ResultStatus { get; set; }
    public int? DelayDays { get; set; }
    public string? Memo { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>내 거래 상세.</summary>
public sealed class TransactionDetail
{
    public MyTransaction Transaction { get; set; } = new();
    public List<PaymentRecord> Payments { get; set; } = [];
    public ReviewInfo? Review { get; set; }
    public List<DisputeInfo> Disputes { get; set; } = [];
}

/// <summary>
/// 내가 쓴 후기(거래당 하나). 계약의 이름은 <c>Review</c> 인데 그대로 두면
/// 후기 팝업 부품과 이름이 부딪혀 <c>Info</c> 를 붙였다.
/// </summary>
public sealed class ReviewInfo
{
    public long ReviewId { get; set; }
    public long TransactionId { get; set; }
    public string? Content { get; set; }
    public string Status { get; set; } = "VISIBLE";
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>미수금 합계(원).</summary>
public sealed class ReceivableSummary
{
    public decimal Total { get; set; }
    public decimal Scheduled { get; set; }
    public decimal Delayed { get; set; }
    public decimal Unpaid { get; set; }
    public decimal Partial { get; set; }
    public decimal Dispute { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// 미수금 한 건. <see cref="Bucket"/> 이 상태와 다른 것은 <c>SCHEDULED</c> 하나다 —
/// 예정일이 지난 예정 거래를 서버가 <c>DELAYED</c>(지급 지연) 칸으로 옮겨 준다.
/// </summary>
public sealed class ReceivableItem
{
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public DateOnly? TransportDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Outstanding { get; set; }
    public DateOnly? ExpectedPaymentDate { get; set; }
    public int? OverdueDays { get; set; }
    public string PaymentStatus { get; set; } = "SCHEDULED";
    public string Bucket { get; set; } = "SCHEDULED";
}

/// <summary>미수금 화면 한 벌.</summary>
public sealed class ReceivablesInfo
{
    public ReceivableSummary Summary { get; set; } = new();
    public List<ReceivableItem> Items { get; set; } = [];
}

/// <summary>나(CargoTrust 사용자). 서버가 처음 부를 때 줄을 만든다.</summary>
public sealed class MeInfo
{
    public long UserId { get; set; }
    public string? ExternalUserId { get; set; }
    public string? DisplayName { get; set; }

    /// <summary><c>DRIVER</c> · <c>CARRIER</c> · <c>ADMIN</c>.</summary>
    public string UserType { get; set; } = "DRIVER";

    public long? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public bool IsAdmin { get; set; }
    public int TransactionCount { get; set; }
    public DateTime? CreatedAt { get; set; }

    /// <summary>이의제기를 낼 수 있는가 — 회사에 연결된 운송사 계정만이다.</summary>
    public bool CanDispute => UserType == "CARRIER" && CompanyId is not null;

    public bool IsBlocked => Status == "BLOCKED";
}

/// <summary>홈 한 벌.</summary>
public sealed class HomeInfo
{
    public MeInfo Me { get; set; } = new();
    public ReceivableSummary Receivable { get; set; } = new();
    public List<CompanySummary> RecentCompanies { get; set; } = [];
}

/// <summary>이의제기. 계약의 이름은 <c>Dispute</c> 인데 화면(DisputePage)과 떼어 두려고 <c>Info</c> 를 붙였다.</summary>
public sealed class DisputeInfo
{
    public long DisputeId { get; set; }
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DateOnly? TransportDate { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "OTHER";
    public string? Content { get; set; }
    public string Status { get; set; } = "RECEIVED";
    public string? Resolution { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class DisputeRequest
{
    public long TransactionId { get; set; }
    public string Reason { get; set; } = "OTHER";
    public string? Content { get; set; }
}

/// <summary>신고. 계약의 이름은 <c>Report</c> 다.</summary>
public sealed class ReportInfo
{
    public long ReportId { get; set; }

    /// <summary><c>COMPANY</c> · <c>TRANSACTION</c> · <c>REVIEW</c>.</summary>
    public string TargetType { get; set; } = "COMPANY";

    public long TargetId { get; set; }
    public string? TargetSummary { get; set; }
    public string Reason { get; set; } = "OTHER";
    public string? Content { get; set; }
    public string Status { get; set; } = "RECEIVED";
    public string? Resolution { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class ReportRequest
{
    public string TargetType { get; set; } = "COMPANY";
    public long TargetId { get; set; }
    public string Reason { get; set; } = "OTHER";
    public string? Content { get; set; }
}

/// <summary>거래처 등록 요청. 사업자번호는 숫자만 보내도 되고 하이픈이 있어도 서버가 걷어 낸다.</summary>
public sealed class CompanyCreateRequest
{
    public string BusinessNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CeoName { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? Phone { get; set; }
    public string? BusinessType { get; set; }
}

/// <summary>후기 저장 요청. 거래당 하나라 있으면 고친다.</summary>
public sealed class ReviewSaveRequest
{
    public string? Content { get; set; }
}
