using JSini.Web.Components.Data;

namespace JSini.Web.CargoTrust.Api;

// ────────────────────────────────────────────────────────────────
// 화면 폼이 묶이는 모델. 요청 DTO 와 떼어 둔 이유는 날짜 하나다 —
// DxDateEdit 은 DateTime? 을 다루고 서버는 DateOnly("yyyy-MM-dd")만 받는다.
// 화면에서 메서드로 옮기면 @bind 가 거절하므로 폼 모델이 DateTime? 을 들고,
// 보낼 때 한 번 옮긴다(ToRequest).
// ────────────────────────────────────────────────────────────────

/// <summary>고른 거래처. 검색 결과든 방금 등록한 것이든 이 셋만 있으면 된다.</summary>
public sealed record CompanyPick(long CompanyId, string CompanyName, string? BusinessNumber)
{
    public static CompanyPick From(CompanySummary c) => new(c.CompanyId, c.CompanyName, c.BusinessNumber);

    public static CompanyPick From(CompanyInfo c) => new(c.CompanyId, c.CompanyName, c.BusinessNumber);
}

/// <summary>거래 등록·수정 폼.</summary>
public sealed class TransactionDraft
{
    public CompanyPick? Company { get; set; }
    public DateTime? TransportDate { get; set; } = DateTime.Today;
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TransportType { get; set; }
    public string? DispatchChannel { get; set; }
    public decimal Amount { get; set; }
    public DateTime? ExpectedPaymentDate { get; set; }
    public string? Memo { get; set; }

    public static TransactionDraft From(MyTransaction t) => new()
    {
        Company = new CompanyPick(t.CompanyId, t.CompanyName, t.BusinessNumber),
        TransportDate = t.TransportDate?.ToDateTime(TimeOnly.MinValue),
        Origin = t.Origin,
        Destination = t.Destination,
        TransportType = t.TransportType,
        DispatchChannel = t.DispatchChannel,
        Amount = t.Amount,
        ExpectedPaymentDate = t.ExpectedPaymentDate?.ToDateTime(TimeOnly.MinValue),
        Memo = t.Memo,
    };

    /// <summary>
    /// 서버에 보내기 전에 막을 것. 서버도 같은 것을 따지지만(설계안 13.2 의 3번)
    /// 여기서 먼저 막으면 칸을 다 채운 폼이 왕복 한 번 없이 무엇이 틀렸는지 듣는다.
    /// </summary>
    public string? Problem()
    {
        if (Company is null)
        {
            return "거래처를 고르십시오.";
        }

        if (TransportDate is null)
        {
            return "운송일을 넣으십시오.";
        }

        if (Amount <= 0)
        {
            return "운송료를 넣으십시오.";
        }

        if (ExpectedPaymentDate is not null && ExpectedPaymentDate.Value.Date < TransportDate.Value.Date)
        {
            return "예정 지급일이 운송일보다 앞섭니다.";
        }

        return null;
    }

    public TransactionSaveRequest ToRequest() => new()
    {
        CompanyId = Company!.CompanyId,
        TransportDate = DateOnly.FromDateTime(TransportDate!.Value),
        Origin = Trim(Origin),
        Destination = Trim(Destination),
        TransportType = Trim(TransportType),
        DispatchChannel = Trim(DispatchChannel),
        Amount = Amount,
        ExpectedPaymentDate = ExpectedPaymentDate is null ? null : DateOnly.FromDateTime(ExpectedPaymentDate.Value),
        Memo = Trim(Memo),
    };

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// 결제 등록 팝업이 보여 줄 거래 요약. 내 거래(<see cref="MyTransaction"/>)와
/// 미수금 한 건(<see cref="ReceivableItem"/>) 양쪽에서 열리므로 공통 칸만 모았다.
/// </summary>
public sealed record PaymentTarget(
    long TransactionId,
    string CompanyName,
    DateOnly? TransportDate,
    decimal Amount,
    decimal PaidAmount,
    DateOnly? ExpectedPaymentDate,
    string PaymentStatus)
{
    public decimal Outstanding => Math.Max(0, Amount - PaidAmount);

    public static PaymentTarget From(MyTransaction t) =>
        new(t.TransactionId, t.CompanyName, t.TransportDate, t.Amount, t.PaidAmount,
            t.ExpectedPaymentDate, t.PaymentStatus);

    public static PaymentTarget From(ReceivableItem r) =>
        new(r.TransactionId, r.CompanyName, r.TransportDate, r.Amount, r.PaidAmount,
            r.ExpectedPaymentDate, r.PaymentStatus);
}

/// <summary>결제 등록 폼. 결과를 셋 중에서 고른다 — 받았다 · 미지급 확정 · 분쟁.</summary>
public sealed class PaymentDraft
{
    /// <summary>화면 전용 값. 서버에는 「받았다」가 <c>result = null</c> 로 간다.</summary>
    public const string Received = "RECEIVED";

    public string Outcome { get; set; } = Received;
    public DateTime? PaidDate { get; set; } = DateTime.Today;
    public decimal PaidAmount { get; set; }
    public string? Memo { get; set; }

    public static readonly IReadOnlyList<SchOption> Outcomes =
    [
        new(Received, "받았다"),
        new(CargoCodes.Unpaid, "미지급으로 확정"),
        new(CargoCodes.Dispute, "분쟁"),
    ];

    public bool IsReceived => Outcome == Received;

    public string? Problem()
    {
        if (!IsReceived)
        {
            return null;
        }

        if (PaidDate is null)
        {
            return "받은 날을 넣으십시오.";
        }

        return PaidAmount <= 0 ? "받은 금액을 넣으십시오." : null;
    }

    /// <summary>
    /// 서버가 정할 상태를 미리 짐작한다(05-api-design.md 「결제 판정」과 같은 규칙).
    /// **보여 주기만 한다** — 저장된 상태는 서버가 돌려준 값을 쓴다.
    /// </summary>
    public string Preview(PaymentTarget target)
    {
        if (!IsReceived)
        {
            return Outcome;
        }

        if (target.PaidAmount + PaidAmount < target.Amount)
        {
            return CargoCodes.Partial;
        }

        return target.ExpectedPaymentDate is null
               || PaidDate is null
               || DateOnly.FromDateTime(PaidDate.Value) <= target.ExpectedPaymentDate.Value
            ? CargoCodes.Paid
            : CargoCodes.Delayed;
    }

    /// <summary>예정일보다 며칠 늦게 받았는가. 늦지 않았으면 0.</summary>
    public int DelayDays(PaymentTarget target) =>
        !IsReceived || PaidDate is null || target.ExpectedPaymentDate is null
            ? 0
            : Math.Max(0, DateOnly.FromDateTime(PaidDate.Value).DayNumber - target.ExpectedPaymentDate.Value.DayNumber);

    public PaymentRequest ToRequest() => new()
    {
        PaidDate = IsReceived && PaidDate is not null ? DateOnly.FromDateTime(PaidDate.Value) : null,
        PaidAmount = IsReceived ? PaidAmount : 0,
        Result = IsReceived ? null : Outcome,
        Memo = string.IsNullOrWhiteSpace(Memo) ? null : Memo.Trim(),
    };
}
