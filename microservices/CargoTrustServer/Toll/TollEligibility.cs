using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Vehicles;

namespace CargoTrustServer.Toll;

/// <summary>확인 항목 하나. <paramref name="Ok"/> 가 null 이면 「알 수 없음」이다.</summary>
public sealed record EligibilityCheck(string Label, bool? Ok, string Note);

/// <summary>
/// 심야할인 <b>대상인가</b>를 짚어 준다. 막지는 않는다.
///
/// <para>
/// [왜 거절하지 않는가]
/// </para>
///
/// <para>
/// 실제로 깎아 주는 쪽은 도로공사다. 우리가 「대상 아님」으로 막아 버리면
/// 서약서를 내고 받고 있는 사람이 자기 할인율을 못 보게 된다 — 그리고 그 사람은
/// 왜 안 되는지도 알 수 없다. 계산은 늘 해 주고, 조건은 <b>목록으로 보여 준다.</b>
/// 운송관리가 거래처를 「악성」으로 부르지 않는 것과 같은 결이다(설계안 12).
/// </para>
///
/// <para>
/// [차종은 할인율의 띠를 바꾸지 않는다]
/// </para>
///
/// <para>
/// 자주 잘못 아는 자리라 적어 둔다. 할인율은 <b>야간 이용비율</b>로만 정해진다.
/// 차종이 가르는 것은 ① 통행료 금액과 ② 여기 적힌 대상 자격, 둘뿐이다.
/// </para>
/// </summary>
public static class TollEligibility
{
    /// <summary>3축 이상(4·5종)이 본래의 대상이다.</summary>
    public static bool IsHeavyFreight(VehicleClass cls) =>
        cls is VehicleClass.C4 or VehicleClass.C5;

    public static IReadOnlyList<EligibilityCheck> For(Vehicle? vehicle)
    {
        if (vehicle is null)
        {
            return
            [
                new("차량", null, "차량을 고르면 대상 조건을 함께 봅니다. 할인율 계산은 차량 없이도 됩니다."),
            ];
        }

        var heavy = IsHeavyFreight(vehicle.VehicleClass);
        var axles = vehicle.AxleCount;

        // 번호판은 **그 자리에서 다시 읽는다.** 줄에 찍어 둔 값(plate_kind·plate_usage)을
        // 쓰면, 번호판 읽기가 생기기 전에 등록된 차가 영영 「모름」으로 남는다 —
        // 그 차들은 아무도 다시 저장하지 않으므로 스스로 고쳐지지 않는다.
        // 찍어 둔 값은 **그때 그렇게 읽었다**는 기록이고(감사·견주기), 판정은 지금 값으로 한다.
        var plate = PlateNumber.Read(vehicle.PlateNo);

        var checks = new List<EligibilityCheck>();

        // ── 사업용 ───────────────────────────────────────────
        //
        // 번호판이 말한 것을 먼저 본다. 사람이 고른 값과 **어긋날 때**가 중요하다 —
        // 자가용 번호판에 사업용으로 적어 둔 차는 실제로 할인을 못 받는데,
        // 사람이 고른 값만 보면 「대상입니다」로 읽힌다.
        checks.Add(plate.Usage switch
        {
            PlateUsage.BUSINESS when vehicle.IsBusiness =>
                new("사업용 화물차", true, $"번호판({plate.Region ?? "지역명 없음"} · 사업용)으로 확인됩니다."),
            PlateUsage.BUSINESS =>
                new("사업용 화물차", null,
                    "번호판은 사업용인데 차량 정보에는 비사업용으로 되어 있습니다. 어느 쪽이 맞는지 확인하십시오."),
            PlateUsage.PRIVATE or PlateUsage.RENTAL when vehicle.IsBusiness =>
                new("사업용 화물차", null,
                    $"번호판이 {PlateNumber.UsageName(plate.Usage)}으로 읽힙니다. "
                    + "심야할인은 사업용 화물차 제도라, 번호판이 맞다면 대상이 아닙니다."),
            PlateUsage.PRIVATE or PlateUsage.RENTAL =>
                new("사업용 화물차", false,
                    $"{PlateNumber.UsageName(plate.Usage)}입니다. 심야할인은 사업용 화물차 제도입니다."),
            _ => vehicle.IsBusiness
                ? new EligibilityCheck("사업용 화물차", null, "사업용으로 적혀 있습니다. 번호판으로는 확인되지 않았습니다.")
                : new EligibilityCheck("사업용 화물차", false, "비사업용으로 적혀 있습니다. 심야할인은 사업용 화물차 제도입니다."),
        });

        // ── 화물차인가(번호판) ───────────────────────────────
        if (plate.Kind != VehicleKind.UNKNOWN)
        {
            checks.Add(plate.Kind == VehicleKind.FREIGHT
                ? new("화물차", true, "번호판 앞자리로 화물차임이 확인됩니다.")
                : new("화물차", false,
                    $"번호판이 {PlateNumber.KindName(plate.Kind)}로 읽힙니다. 심야할인은 화물차 제도입니다."));
        }

        // ── 통행료 차종 ──────────────────────────────────────
        checks.Add(new("통행료 차종",
            heavy ? true : null,
            heavy
                ? $"{VehicleClassName(vehicle.VehicleClass)} — 3축 이상 대형화물차입니다."
                : $"{VehicleClassName(vehicle.VehicleClass)} — 3축 미만은 서약서를 내고 하이패스로 받습니다."));

        // ── 축수 ─────────────────────────────────────────────
        //
        // **안 적어도 된다.** 모르는 것을 모른다고 두는 편이, 등록을 막아
        // 할인율 계산까지 못 하게 하는 것보다 낫다. 대신 어디서 확인하는지를 적어 둔다 —
        // 실제 통행료 차종은 영업소가 축수와 윤폭으로 재서 찍으므로,
        // 하이패스 이용내역의 차종이 등록원부보다 실무적으로 더 맞다.
        checks.Add(axles switch
        {
            null => new("축수", null, "적지 않았습니다. 하이패스 이용내역에 찍힌 차종으로 확인하실 수 있습니다."),
            >= 3 => new("축수", true, $"{axles}축입니다."),
            _ => new("축수", null, $"{axles}축 — 3축 미만이라 서약서 조건이 붙습니다."),
        });

        checks.Add(new("중복 할인", null,
            "출퇴근 할인 등 다른 할인과는 겹쳐 받지 못합니다. 이 화면은 심야할인만 셉니다."));

        return checks;
    }

    /// <summary>
    /// 자격 목록을 한 줄로 줄인 판정.
    ///
    /// <para>
    /// [할인율과 다른 것을 말한다]
    /// </para>
    ///
    /// <para>
    /// 할인율은 <b>야간 이용비율</b>로만 정해져서 차를 바꿔도 안 바뀐다. 그런데
    /// 「그래서 이 차가 그 할인을 받나」는 차마다 다르다 — 그 둘이 다른 물음인데
    /// 뒤엣것이 접어 둔 목록 안에만 있으면, 차를 바꿔 본 사람은 <b>아무것도 안
    /// 바뀌었다고 읽는다.</b> 그래서 한 줄로 줄여 결과 옆에 세운다.
    /// </para>
    ///
    /// <para>
    /// 갈래는 셋이다 — <c>OK</c> 받는다 · <c>CHECK</c> 조건이 붙는다 ·
    /// <c>NO</c> 대상이 아니다. 자격 목록과 같은 셋(맞음·알 수 없음·아님)이고,
    /// <b>가장 나쁜 칸이 전체를 정한다.</b>
    /// </para>
    /// </summary>
    public static (string Code, string Note) Verdict(Vehicle? vehicle)
    {
        if (vehicle is null)
            return ("NONE", "차량을 고르면 이 차가 대상인지 함께 봅니다.");

        var plate = PlateNumber.Read(vehicle.PlateNo);

        // 막는 것 — 번호판이 아니라고 말하거나, 사람이 비사업용으로 적어 두었을 때.
        var blockers = new List<string>();
        if (plate.IsBusiness == false) blockers.Add(PlateNumber.UsageName(plate.Usage));
        else if (!vehicle.IsBusiness) blockers.Add("비사업용");
        if (plate.IsFreight == false) blockers.Add(PlateNumber.KindName(plate.Kind));

        if (blockers.Count > 0)
            return ("NO", $"대상이 아닙니다 — {string.Join(" · ", blockers.Distinct())}");

        // 막지는 않지만 조건이 붙는 것.
        var pending = new List<string>();
        if (plate.Usage != PlateUsage.BUSINESS) pending.Add("번호판으로 사업용이 확인되지 않음");
        if (vehicle.AxleCount is null) pending.Add("축수 모름");
        else if (vehicle.AxleCount < 3) pending.Add($"{vehicle.AxleCount}축이라 서약서 조건");
        if (!IsHeavyFreight(vehicle.VehicleClass) && !vehicle.HasHipass) pending.Add("하이패스 단말 없음");

        return pending.Count > 0
            ? ("CHECK", $"확인할 것이 있습니다 — {string.Join(" · ", pending)}")
            : ("OK", "이 차량은 심야할인 대상입니다.");
    }

    public static string VehicleClassName(VehicleClass cls) => cls switch
    {
        VehicleClass.LIGHT => "경차",
        VehicleClass.C1 => "1종",
        VehicleClass.C2 => "2종",
        VehicleClass.C3 => "3종",
        VehicleClass.C4 => "4종(3축)",
        VehicleClass.C5 => "5종(4축 이상)",
        _ => cls.ToString(),
    };
}
