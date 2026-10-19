using System.Text.RegularExpressions;
using CargoTrustServer.Common;

namespace CargoTrustServer.Vehicles;

/// <summary>
/// 번호판이 스스로 말하는 것을 읽는다. 차량번호로 차종을 주는 무료 공개 API 가 없어 바깥을 부르지 않는다.
/// 읽은 것은 제안일 뿐이라 규칙에 안 맞으면 UNKNOWN 이고 사람이 덮어쓸 수 있다(번호판이 말한 것과 사람이 고른 것을 나란히 저장한다).
/// 번호판 규칙을 코드에 둔 까닭은 고시라 거의 안 바뀌고 지난 판정의 뜻이 달라지지 않기 때문이다.
/// </summary>
public static class PlateNumber
{
    // 지역명(2자) · 차종 숫자(2~3자) · 용도 한글(1자) · 일련번호(4자). 사이 공백은 사람마다 달라 어디에 있든 받아 준다.
    private static readonly Regex Shape = new(
        @"^\s*(?<region>[가-힣]{2})?\s*(?<num>\d{2,3})\s*(?<usage>[가-힣])\s*(?<serial>\d{4})\s*$",
        RegexOptions.Compiled);

    /// <summary>운수사업용 — 심야할인이 보는 「사업용」이 이것이다.</summary>
    private static readonly char[] BusinessChars = ['아', '바', '사', '자'];

    /// <summary>택배.</summary>
    private const char DeliveryChar = '배';

    /// <summary>렌터카.</summary>
    private static readonly char[] RentalChars = ['하', '허', '호'];

    /// <summary>번호판에서 읽어 낸 것. 아무것도 못 읽었으면 전부 <c>UNKNOWN</c> 이다.</summary>
    public sealed record Reading(
        string Normalized,
        string? Region,
        int? ClassNumber,
        string? UsageChar,
        VehicleKind Kind,
        PlateUsage Usage)
    {
        /// <summary>모양조차 못 읽었나.</summary>
        public bool Unreadable => Kind == VehicleKind.UNKNOWN && Usage == PlateUsage.UNKNOWN;

        /// <summary>사업용으로 읽혔나. 모르면 null — <b>아니라고 단정하지 않는다.</b></summary>
        public bool? IsBusiness => Usage switch
        {
            PlateUsage.BUSINESS => true,
            PlateUsage.PRIVATE or PlateUsage.RENTAL => false,
            _ => null,
        };

        /// <summary>화물차로 읽혔나.</summary>
        public bool? IsFreight => Kind switch
        {
            VehicleKind.FREIGHT => true,
            VehicleKind.PASSENGER or VehicleKind.VAN or VehicleKind.SPECIAL => false,
            _ => null,
        };

        /// <summary>
        /// 통행료 차종 제안. <b>번호판은 축수를 말해 주지 않으므로 4·5종을 가를 수 없다.</b>
        /// 화물차면 가장 흔한 4종을 제안하고, 축수는 사람이 고른다.
        /// </summary>
        public VehicleClass? SuggestedClass => Kind switch
        {
            VehicleKind.FREIGHT => VehicleClass.C4,
            VehicleKind.VAN => VehicleClass.C3,
            VehicleKind.PASSENGER => VehicleClass.C1,
            _ => null,
        };

        /// <summary>화면이 그대로 띄우는 한 줄.</summary>
        public string Summary()
        {
            if (Unreadable)
                return "번호판을 읽지 못했습니다. 차종과 사업용 여부를 직접 고르십시오.";

            var parts = new List<string>();
            if (Region is not null) parts.Add(Region);
            parts.Add(KindName(Kind));
            parts.Add(UsageName(Usage));
            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// 차량번호를 읽는다. <paramref name="raw"/> 가 비었거나 꼴이 안 맞으면
    /// <see cref="Reading.Unreadable"/> 인 결과를 돌려준다 — 예외를 던지지 않는다.
    /// </summary>
    public static Reading Read(string? raw)
    {
        var normalized = Normalize(raw);
        var match = Shape.Match(raw ?? string.Empty);
        if (!match.Success)
            return new Reading(normalized, null, null, null, VehicleKind.UNKNOWN, PlateUsage.UNKNOWN);

        var region = match.Groups["region"].Success ? match.Groups["region"].Value : null;
        var digits = match.Groups["num"].Value;
        var usageChar = match.Groups["usage"].Value[0];

        return new Reading(
            normalized,
            region,
            int.TryParse(digits, out var n) ? n : null,
            usageChar.ToString(),
            KindOf(digits),
            UsageOf(usageChar));
    }

    /// <summary>저장하는 꼴 — 공백만 걷는다. 지역명은 남긴다(사업용의 표시다).</summary>
    public static string Normalize(string? raw) =>
        new((raw ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());

    /// <summary>앞자리 숫자가 말하는 차종. 두 자리는 01~69 승용 · 70~79 승합 · 80~97 화물 · 98~99 특수다. 세 자리 번호판은 비사업용 승용에 주로 나가므로 구간 밖이면 UNKNOWN 이다.</summary>
    private static VehicleKind KindOf(string digits)
    {
        if (!int.TryParse(digits, out var n)) return VehicleKind.UNKNOWN;

        return digits.Length switch
        {
            2 => n switch
            {
                >= 1 and <= 69 => VehicleKind.PASSENGER,
                >= 70 and <= 79 => VehicleKind.VAN,
                >= 80 and <= 97 => VehicleKind.FREIGHT,
                >= 98 and <= 99 => VehicleKind.SPECIAL,
                _ => VehicleKind.UNKNOWN,
            },
            3 => n switch
            {
                >= 100 and <= 699 => VehicleKind.PASSENGER,
                >= 700 and <= 799 => VehicleKind.VAN,
                >= 800 and <= 979 => VehicleKind.FREIGHT,
                >= 980 and <= 999 => VehicleKind.SPECIAL,
                _ => VehicleKind.UNKNOWN,
            },
            _ => VehicleKind.UNKNOWN,
        };
    }

    /// <summary>한글 한 자가 말하는 용도. 특별한 몇 자만 꼽고 나머지는 비사업용으로 본다 — 글자가 늘 때 빠진 글자가 「알 수 없음」이 되어 멀쩡한 자가용이 사라지는 것을 막는다.</summary>
    private static PlateUsage UsageOf(char usage)
    {
        if (BusinessChars.Contains(usage)) return PlateUsage.BUSINESS;
        if (usage == DeliveryChar) return PlateUsage.DELIVERY;
        if (RentalChars.Contains(usage)) return PlateUsage.RENTAL;
        return char.IsBetween(usage, '가', '힣') ? PlateUsage.PRIVATE : PlateUsage.UNKNOWN;
    }

    public static string KindName(VehicleKind kind) => kind switch
    {
        VehicleKind.PASSENGER => "승용차",
        VehicleKind.VAN => "승합차",
        VehicleKind.FREIGHT => "화물차",
        VehicleKind.SPECIAL => "특수차",
        _ => "차종 모름",
    };

    public static string UsageName(PlateUsage usage) => usage switch
    {
        PlateUsage.BUSINESS => "사업용(영업용)",
        PlateUsage.DELIVERY => "택배",
        PlateUsage.RENTAL => "렌터카",
        PlateUsage.PRIVATE => "비사업용(자가용)",
        _ => "용도 모름",
    };
}
