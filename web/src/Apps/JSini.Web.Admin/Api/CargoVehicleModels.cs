namespace JSini.Web.Admin.Api;

// 계정에 매달린 차량. 계약 정본은 docs/cargotrust/06-toll-night-discount.md 다.
//
// 운송관리 모듈(JSini.Web.CargoTrust)에도 비슷한 모양이 있지만 복제한다 —
// 두 모듈이 쓰면 복제가 규칙이고(web/CLAUDE.md), 여기 것에는 **누구 차인지**가
// 실려 있다. 한 벌로 합치면 차주 화면에서 남의 번호판을 그릴 수 있게 된다.

/// <summary>
/// 번호판이 말한 것.
///
/// <para>
/// 차량번호로 차종·축수를 주는 <b>무료 공개 API 가 없어서</b>, 바깥을 부르는 대신
/// 번호판을 읽는다 — 심야할인 판정에 필요한 셋 중 둘(화물차인가 · 사업용인가)이
/// 번호판에 이미 적혀 있다. 읽기는 <b>서버가 한다.</b>
/// </para>
/// </summary>
public sealed class PlateReadDto
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
    public string? SuggestedClass { get; set; }
    public bool Readable { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>관리자가 보는 차량 한 대.</summary>
public sealed class CargoVehicleDto
{
    public long VehicleId { get; set; }
    public long UserId { get; set; }

    /// <summary>포털 로그인 아이디. 계정과 차량을 잇는 열쇠다.</summary>
    public string ExternalUserId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
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
    public PlateReadDto? Plate { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>등록·수정에 보내는 것. 등록일 때만 계정 칸이 쓰인다.</summary>
public sealed class CargoVehicleSaveDto
{
    public string ExternalUserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
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

/// <summary>화면 폼. 톤수·축수를 편집기가 다루는 <c>decimal</c> 로 들고 있다가 보낼 때 옮긴다.</summary>
public sealed class CargoVehicleForm
{
    public long? VehicleId { get; set; }
    public string? LoginId { get; set; }
    public string? UserName { get; set; }
    public string PlateNo { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string VehicleClass { get; set; } = "C4";

    /// <summary>축수. 고르개 값이라 문자열이고 <c>"0"</c> 이 <b>「모름」</b>이다. 선택이다.</summary>
    public string AxleCount { get; set; } = "0";

    public decimal Tonnage { get; set; }
    public bool IsBusiness { get; set; } = true;
    public bool HasHipass { get; set; } = true;
    public bool IsDefault { get; set; }
    public string? Memo { get; set; }

    public bool IsNew => VehicleId is null;

    public static CargoVehicleForm From(CargoVehicleDto v) => new()
    {
        VehicleId = v.VehicleId,
        LoginId = v.ExternalUserId,
        UserName = v.DisplayName,
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

    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(LoginId))
        {
            return "차량을 매달 계정을 고르십시오.";
        }

        if (string.IsNullOrWhiteSpace(PlateNo))
        {
            return "차량번호를 넣으십시오.";
        }

        return null;
    }

    public CargoVehicleSaveDto ToRequest() => new()
    {
        ExternalUserId = LoginId?.Trim() ?? string.Empty,
        DisplayName = UserName,
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

/// <summary>차종·축수 고르개. 운송관리 쪽과 같은 값이다(코드표가 정본).</summary>
public static class CargoVehicleCodes
{
    /// <summary>축수. <b>「모름」이 첫 줄이다</b> — 안 고르면 모르는 것이지 0축인 차가 있는 게 아니다.</summary>
    public static readonly IReadOnlyList<object> AxleOptions =
    [
        new { Value = "0", Text = "모름" },
        new { Value = "2", Text = "2축" },
        new { Value = "3", Text = "3축" },
        new { Value = "4", Text = "4축" },
        new { Value = "5", Text = "5축" },
        new { Value = "6", Text = "6축 이상" },
    ];

    public static readonly IReadOnlyList<object> ClassOptions =
    [
        new { Value = "LIGHT", Text = "경차" },
        new { Value = "C1", Text = "1종 (승용 · 2.5톤 미만 화물)" },
        new { Value = "C2", Text = "2종 (2.5~5.5톤 화물)" },
        new { Value = "C3", Text = "3종 (5.5~10톤 화물)" },
        new { Value = "C4", Text = "4종 (3축 · 10~20톤 화물)" },
        new { Value = "C5", Text = "5종 (4축 이상 · 20톤 이상 화물)" },
    ];
}
