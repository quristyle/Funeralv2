using JSini.Web.Components.Data;

namespace JSini.Web.CargoTrust.Api;

// 요청 DTO 와 떼어 둔 폼 모델. DxDateEdit 은 DateTime? 를 다루고 서버는 DateOnly 만 받으니 ToRequest 에서 옮긴다.

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
    public DateTime? TransportDate { get; set; } = AppTime.TodayDate;
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
    /// 서버에 보내기 전에 막는다. 칸을 다 채운 뒤 왕복 없이 무엇이 틀렸는지 듣게 한다.
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

/// <summary>결제 등록 팝업의 거래 요약. 내 거래와 미수금 양쪽에서 열려 공통 칸만 모았다.</summary>
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

    /// <summary>미지급 거래 목록에서. 그 목록은 <c>UNPAID</c> 만 모으므로 상태가 칸에 없다.</summary>
    public static PaymentTarget From(UnpaidTransaction u) =>
        new(u.TransactionId, u.CompanyName, u.TransportDate, u.Amount, u.PaidAmount,
            u.ExpectedPaymentDate, CargoCodes.Unpaid);
}

/// <summary>결제 등록 폼. 결과를 셋 중에서 고른다 — 받았다 · 미지급 확정 · 분쟁.</summary>
public sealed class PaymentDraft
{
    /// <summary>화면 전용 값. 서버에는 「받았다」가 <c>result = null</c> 로 간다.</summary>
    public const string Received = "RECEIVED";

    public string Outcome { get; set; } = Received;
    public DateTime? PaidDate { get; set; } = AppTime.TodayDate;
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

    /// <summary>저장될 상태를 미리 짐작해 보여 주기만 한다. 실제 상태는 서버 응답을 쓴다.</summary>
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

/// <summary>
/// 요청 DTO 와 떼어 둔 폼 모델. 편집기는 decimal 0 을 들고 서버는 「안 적었음」을 null 로 받으니, 0톤은 보낼 때 null 로 옮긴다.
/// </summary>
public sealed class VehicleDraft
{
    public long? VehicleId { get; set; }
    public string PlateNo { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string VehicleClass { get; set; } = "C4";

    /// <summary>
    /// 축수. 고르개 값이라 문자열이고 "0" 이 모름이다. 번호판은 축수를 말해 주지 않으니 등록을 막지 않는다(선택).
    /// </summary>
    public string AxleCount { get; set; } = "0";

    public decimal Tonnage { get; set; }
    public bool IsBusiness { get; set; } = true;
    public bool HasHipass { get; set; } = true;
    public bool IsDefault { get; set; }
    public string? Memo { get; set; }

    public bool IsNew => VehicleId is null;

    public static VehicleDraft From(VehicleInfo v) => new()
    {
        VehicleId = v.VehicleId,
        PlateNo = v.PlateNo,
        Nickname = v.Nickname,
        VehicleClass = v.VehicleClass,
        AxleCount = (v.AxleCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
        Tonnage = v.Tonnage ?? 0,
        IsBusiness = v.IsBusiness,
        HasHipass = v.HasHipass,
        IsDefault = v.IsDefault,
        Memo = v.Memo,
    };

    /// <summary>서버에 보내기 전에 막는다. 왕복 없이 무엇이 틀렸는지 듣게 한다.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(PlateNo))
        {
            return "차량번호를 넣으십시오.";
        }

        return null;
    }

    public VehicleSaveRequest ToRequest() => new()
    {
        PlateNo = PlateNo.Trim(),
        Nickname = Nickname,
        VehicleClass = VehicleClass,
        AxleCount = short.TryParse(AxleCount, out var axles) && axles > 0 ? axles : null,
        Tonnage = Tonnage == 0 ? null : Tonnage,
        IsBusiness = IsBusiness,
        HasHipass = HasHipass,
        IsDefault = IsDefault,
        Memo = Memo,
    };
}
