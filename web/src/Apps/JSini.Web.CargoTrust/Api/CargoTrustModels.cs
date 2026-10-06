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
/// 공개 거래 한 줄. 출발·도착은 첫 낱말(「경기」)만 오고, 등록자는
/// <b>가린 이름</b>으로 온다(「이*열」 — 설계안 29). 관리자에게만 그대로 온다.
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

    /// <summary>이 거래를 등록한 사람(가린 이름). 서버가 가려서 준다 — 화면이 더 가리지 않는다.</summary>
    public string? RegisteredBy { get; set; }

    /// <summary>표에 「경기 → 부산」 한 칸으로 싣는다.</summary>
    public string Route => CargoCodes.RouteText(OriginRegion, DestinationRegion);
}

/// <summary>
/// 미지급 거래 목록의 한 줄(<c>/unpaid-transactions</c>).
///
/// <para>
/// <see cref="MyTransaction"/> 과 달리 <b>남의 거래가 섞여 있다.</b> 그래서 메모·검증
/// 상태처럼 등록자만 볼 칸이 없고, 사업자번호와 등록자 이름은 가린 꼴로 온다.
/// </para>
/// </summary>
public sealed class UnpaidTransaction
{
    public long TransactionId { get; set; }
    public long CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? BusinessNumber { get; set; }
    public DateOnly? TransportDate { get; set; }
    public string? OriginRegion { get; set; }
    public string? DestinationRegion { get; set; }
    public string? TransportType { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }

    /// <summary>아직 못 받은 금액. 일부를 받아 둔 미지급이면 운송료보다 작다.</summary>
    public decimal Outstanding { get; set; }

    public DateOnly? ExpectedPaymentDate { get; set; }

    /// <summary>예정일이 지났으면 오늘까지 지난 날수. 예정일이 없으면 <c>null</c>.</summary>
    public int? OverdueDays { get; set; }

    /// <summary>등록한 사람(가린 이름). 관리자에게만 그대로 온다.</summary>
    public string? RegisteredBy { get; set; }

    /// <summary>내가 등록한 거래인가. 결제 등록은 이것이 참인 줄에만 뜬다.</summary>
    public bool Mine { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string Route => CargoCodes.RouteText(OriginRegion, DestinationRegion);
}

/// <summary>미지급 거래 목록의 머리 숫자. <b>실제로 실린 줄만</b> 센 값이다(상한 500).</summary>
public sealed class UnpaidSummary
{
    public int Count { get; set; }

    /// <summary>못 받은 금액의 합.</summary>
    public decimal Amount { get; set; }

    public int CompanyCount { get; set; }

    /// <summary>등록한 사람 수. 한 사람이 몰아 올린 목록인지가 이 숫자로 읽힌다.</summary>
    public int RegistrantCount { get; set; }

    public int MineCount { get; set; }
}

/// <summary>미지급 거래 목록 한 벌.</summary>
public sealed class UnpaidList
{
    public UnpaidSummary Summary { get; set; } = new();
    public List<UnpaidTransaction> Items { get; set; } = [];
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

/// <summary>
/// 여러 거래를 한 번에 처리하는 요청. 금액이 없는 것이 단건(<see cref="PaymentRequest"/>)과의
/// 차이다 — 건마다 <b>남은 금액 전액</b>을 넣는다고 서버가 읽는다.
/// </summary>
public sealed class BulkPaymentRequest
{
    public List<long> TransactionIds { get; set; } = [];
    public DateOnly? PaidDate { get; set; }

    /// <summary><c>null</c> · <c>UNPAID</c> · <c>DISPUTE</c>.</summary>
    public string? Result { get; set; }

    public string? Memo { get; set; }
}

/// <summary>한 번에 처리한 결과. 된 것과 안 된 것이 함께 온다.</summary>
public sealed class BulkPaymentResult
{
    public List<MyTransaction> Updated { get; set; } = [];
    public List<BulkPaymentFailure> Failed { get; set; } = [];
}

/// <summary>한 번에 처리하다 걸러진 한 건.</summary>
public sealed class BulkPaymentFailure
{
    public long TransactionId { get; set; }
    public string? Message { get; set; }
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

// ────────────────────────────────────────────────────────────────
// 톨게이트 심야할인 (2026-10-06)
//
// [시각 칸은 KST 벽시계다]
//
// 이 묶음의 DateTime 에는 오프셋이 없고 「한국 시각 몇 시」라는 뜻이다.
// 서버 계약(docs/cargotrust/06-toll-night-discount.md)이 그렇게 정해져 있다 —
// 포털 프론트는 컨테이너 시계가 UTC 인 서버 안에서 돌아서, 오프셋을 붙이는
// 순간 아홉 시간이 어긋나고 그 어긋남은 **할인율이 한 띠 밀리는** 모습으로만
// 드러난다. 보낼 때 Kind 를 Unspecified 로 못 박는다(TollCalcDraft).
// ────────────────────────────────────────────────────────────────

/// <summary>할인 띠 하나.</summary>
public sealed class TollBandInfo
{
    public decimal MinRatio { get; set; }
    public decimal? MaxRatioExclusive { get; set; }
    public decimal DiscountPercent { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>지금 쓰는 규칙. 결과 옆에 「무엇을 기준으로 셌는지」를 적으려고 받는다.</summary>
public sealed class TollRulesInfo
{
    public string Code { get; set; } = string.Empty;
    public string ClosedWindowLabel { get; set; } = string.Empty;
    public string OpenWindowLabel { get; set; } = string.Empty;

    /// <summary>야간창. <c>"21:00"</c> 꼴이고 화면이 셈에 쓴다(글자에서 뽑아 쓰지 않는다).</summary>
    public string ClosedNightStart { get; set; } = "21:00";

    public string ClosedNightEnd { get; set; } = "06:00";
    public string OpenNightStart { get; set; } = "23:00";
    public string OpenNightEnd { get; set; } = "05:00";

    public List<TollBandInfo> ClosedBands { get; set; } = [];
    public List<TollBandInfo> OpenBands { get; set; } = [];

    /// <summary>고를 수 있는 목표 할인율 — 0% 는 고를 이유가 없어 뺀다.</summary>
    public IReadOnlyList<decimal> TargetChoices(string sectionType) =>
        [.. (sectionType == "OPEN" ? OpenBands : ClosedBands)
            .Select(b => b.DiscountPercent).Where(d => d > 0).Distinct().OrderBy(d => d)];
}

/// <summary>영업소.</summary>
public sealed class TollPlazaInfo
{
    public long PlazaId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string? RouteNo { get; set; }
    public string? RouteName { get; set; }
    public string SectionType { get; set; } = "CLOSED";
    public bool IsPrivate { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lon { get; set; }
}

/// <summary>대상 조건 한 줄. <c>Ok</c> 가 null 이면 「알 수 없음」이다.</summary>
public sealed class EligibilityCheckInfo
{
    public string Label { get; set; } = string.Empty;
    public bool? Ok { get; set; }
    public string Note { get; set; } = string.Empty;
}

/// <summary>야간창에 겹친 토막 — 타임라인 막대의 음영.</summary>
public sealed class TollNightSegmentInfo
{
    public DateTime FromKst { get; set; }
    public DateTime ToKst { get; set; }
}

/// <summary>정방향 결과.</summary>
public sealed class TollCalcResultInfo
{
    public string SectionType { get; set; } = "CLOSED";
    public string RuleSetCode { get; set; } = string.Empty;
    public DateTime EntryAtKst { get; set; }
    public DateTime? ExitAtKst { get; set; }
    public int TotalMinutes { get; set; }
    public int NightMinutes { get; set; }
    public decimal NightRatio { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal BandMinRatio { get; set; }
    public decimal? NextBandMinRatio { get; set; }
    public decimal? NextDiscountPercent { get; set; }
    public int? DelayExitMinutes { get; set; }
    public int? DelayEntryMinutes { get; set; }
    public string NightWindowLabel { get; set; } = string.Empty;
    public List<TollNightSegmentInfo> NightSegments { get; set; } = [];
    public List<EligibilityCheckInfo> Checks { get; set; } = [];
    /// <summary>
    /// 이 차량이 그 할인을 <b>받을 수 있나</b> — <c>OK</c> · <c>CHECK</c> · <c>NO</c> · <c>NONE</c>.
    ///
    /// <para>
    /// 할인율과 다른 물음이다. 할인율은 야간 이용비율로만 정해져 차를 바꿔도 안 바뀌는데,
    /// 「그래서 이 차가 받나」는 차마다 다르다.
    /// </para>
    /// </summary>
    public string EligibilityVerdict { get; set; } = "NONE";

    public string EligibilityNote { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}

/// <summary>추천 하나.</summary>
public sealed class TollSuggestOptionInfo
{
    public DateTime FromKst { get; set; }
    public DateTime ToKst { get; set; }
    public DateTime BestKst { get; set; }
    public int MinDurationMinutes { get; set; }
    public int MaxDurationMinutes { get; set; }
    public DateTime PairedEntryKst { get; set; }
    public DateTime PairedExitKst { get; set; }
    public decimal NightRatio { get; set; }
    public decimal DiscountPercent { get; set; }
}

/// <summary>역방향 결과.</summary>
public sealed class TollSuggestResultInfo
{
    public string SectionType { get; set; } = "CLOSED";
    public string RuleSetCode { get; set; } = string.Empty;
    public string Anchor { get; set; } = "ENTRY";
    public DateTime AnchorAtKst { get; set; }
    public decimal TargetDiscount { get; set; }
    public decimal? RequiredRatio { get; set; }
    public bool Reachable { get; set; }

    /// <summary>
    /// 어떤 길로 찾았나 — <c>DURATION</c> 시각을 두고 소요시간 조절 ·
    /// <c>SHIFT</c> 소요시간을 두고 <b>시각을 옮김</b> · <c>NONE</c> 못 찾음.
    /// 추천 카드의 글자가 이것으로 갈린다.
    /// </summary>
    public string Mode { get; set; } = "NONE";

    public List<TollSuggestOptionInfo> Options { get; set; } = [];
    public decimal BestRatio { get; set; }
    public decimal BestDiscountPercent { get; set; }
    public int BestDurationMinutes { get; set; }
    public int MinDurationMinutes { get; set; }
    public int MaxDurationMinutes { get; set; }
    public string NightWindowLabel { get; set; } = string.Empty;
    public List<EligibilityCheckInfo> Checks { get; set; } = [];
    /// <summary>
    /// 이 차량이 그 할인을 <b>받을 수 있나</b> — <c>OK</c> · <c>CHECK</c> · <c>NO</c> · <c>NONE</c>.
    ///
    /// <para>
    /// 할인율과 다른 물음이다. 할인율은 야간 이용비율로만 정해져 차를 바꿔도 안 바뀌는데,
    /// 「그래서 이 차가 받나」는 차마다 다르다.
    /// </para>
    /// </summary>
    public string EligibilityVerdict { get; set; } = "NONE";

    public string EligibilityNote { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}

/// <summary>계산 이력 한 줄.</summary>
public sealed class TollCalcHistoryInfo
{
    public long CalcId { get; set; }
    public string Mode { get; set; } = "CALC";
    public string SectionType { get; set; } = "CLOSED";
    public DateTime? EntryAtKst { get; set; }
    public DateTime? ExitAtKst { get; set; }
    public int? TotalMinutes { get; set; }
    public int? NightMinutes { get; set; }
    public decimal? NightRatio { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? TargetDiscount { get; set; }
    public string? RuleSetCode { get; set; }
    public string? PlateNo { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// 번호판이 말한 것.
///
/// <para>
/// 차량번호로 차종·축수를 주는 <b>무료 공개 API 가 없어서</b>, 바깥을 부르는 대신
/// 번호판을 읽는다. 심야할인 판정에 필요한 셋 중 둘(화물차인가 · 사업용인가)이
/// 번호판에 이미 적혀 있다. 읽기는 <b>서버가 한다</b> — 화면이 따로 읽으면 두 곳의
/// 해석이 갈리고, 갈린 쪽을 나중에 가려낼 방법이 없다.
/// </para>
/// </summary>
public sealed class PlateReadInfo
{
    public string PlateNo { get; set; } = string.Empty;
    public string? Region { get; set; }
    public int? ClassNumber { get; set; }
    public string? UsageChar { get; set; }
    public string Kind { get; set; } = "UNKNOWN";
    public string KindName { get; set; } = string.Empty;
    public string Usage { get; set; } = "UNKNOWN";
    public string UsageName { get; set; } = string.Empty;

    /// <summary>사업용인가. <b>모르면 null</b> — 아니라고 단정하지 않는다.</summary>
    public bool? IsBusiness { get; set; }

    public bool? IsFreight { get; set; }

    /// <summary>제안 차종. 번호판은 축수를 말해 주지 않아 4·5종을 가르지 못한다.</summary>
    public string? SuggestedClass { get; set; }

    public bool Readable { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>내 차량 한 대.</summary>
public sealed class VehicleInfo
{
    public long VehicleId { get; set; }
    public string PlateNo { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string VehicleClass { get; set; } = "C4";
    public string VehicleClassName { get; set; } = string.Empty;
    public short? AxleCount { get; set; }
    public decimal? Tonnage { get; set; }
    public bool IsBusiness { get; set; } = true;
    public bool HasHipass { get; set; } = true;
    public bool IsDefault { get; set; }
    public string? Memo { get; set; }

    /// <summary>번호판이 말한 것. 저장된 값과 견주면 사람이 덮어썼는지가 보인다.</summary>
    public PlateReadInfo? Plate { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>고르개에 보일 이름 — 별칭이 있으면 그것이 먼저다.</summary>
    public string Display => string.IsNullOrWhiteSpace(Nickname) ? PlateNo : $"{Nickname} ({PlateNo})";
}

/// <summary>차량 등록·수정 요청.</summary>
public sealed class VehicleSaveRequest
{
    public string PlateNo { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string VehicleClass { get; set; } = "C4";
    public short? AxleCount { get; set; }
    public decimal? Tonnage { get; set; }
    public bool IsBusiness { get; set; } = true;
    public bool HasHipass { get; set; } = true;
    public bool IsDefault { get; set; }
    public string? Memo { get; set; }
}

/// <summary>정방향 요청.</summary>
public sealed class TollCalcRequest
{
    public string SectionType { get; set; } = "CLOSED";
    public DateTime EntryAt { get; set; }
    public DateTime? ExitAt { get; set; }
    public long? VehicleId { get; set; }
    public long? EntryPlazaId { get; set; }
    public long? ExitPlazaId { get; set; }
    public bool Save { get; set; }
}

/// <summary>역방향 요청.</summary>
public sealed class TollSuggestRequest
{
    public string SectionType { get; set; } = "CLOSED";
    public string Anchor { get; set; } = "ENTRY";
    public DateTime AnchorAt { get; set; }
    public decimal TargetDiscount { get; set; }
    public int? MinDurationMinutes { get; set; }
    public int? MaxDurationMinutes { get; set; }
    public long? VehicleId { get; set; }
    public bool Save { get; set; }
}

/// <summary>
/// 구간 하나의 통행료.
///
/// <para>
/// <c>NormalFare</c> 는 고른 차종의 정상요금, <c>DiscountedFare</c> 는 거기에 심야할인율을
/// 먹인 값이다. 할인액을 바깥에서 받아 오지 않는다 — 비율로 할인율을 정하는 곳은 한 군데다.
/// </para>
/// </summary>
public sealed class TollFareInfo
{
    public string FromCode { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string ToCode { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }

    /// <summary>도로공사가 보는 주행시간. 소요시간 칩의 바닥값으로 쓴다.</summary>
    public int DriveMinutes { get; set; }

    public string VehicleClass { get; set; } = "C4";
    public string VehicleClassName { get; set; } = string.Empty;
    public int? NormalFare { get; set; }
    public int? DiscountedFare { get; set; }
    public int? SavedFare { get; set; }
    public decimal DiscountPercent { get; set; }
    public string Summary { get; set; } = string.Empty;

    public bool HasFare => NormalFare is > 0;
}
