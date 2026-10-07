using System.Text.RegularExpressions;
using CargoTrustServer.Common;

namespace CargoTrustServer.Vehicles;

/// <summary>
/// 번호판이 스스로 말하는 것 — 차량번호 하나에서 읽어 낸다.
///
/// <para>
/// [왜 바깥 API 를 안 부르는가]
/// </para>
///
/// <para>
/// 차량번호로 차종·축수를 주는 <b>무료 공개 API 가 없다.</b> 국토교통부
/// 자동차종합정보 첨부형API 는 소유자의 휴대폰 본인인증을 거친 제3자 제공 동의와
/// 기관 승인·행정서류·유료 본인인증 서비스 가입이 전제이고, 민간은 전부 유료
/// 계약이다. 그런데 <b>심야할인 판정에 필요한 것은 셋뿐이고</b> 그중 둘이
/// 번호판에 이미 적혀 있다 — 화물차인가(앞자리 숫자), 사업용인가(한글).
/// </para>
///
/// <para>
/// 그래서 바깥을 부르는 대신 번호판을 읽는다. 키도 계약도 수수료도 없고,
/// 네트워크도 타지 않는다. 남는 축수 하나는 사람이 고르되 <b>안 골라도 된다</b> —
/// 모르는 것은 모른다고 두는 편이, 등록 자체를 막아 할인율 계산까지 못 하게
/// 하는 것보다 낫다.
/// </para>
///
/// <para>
/// [읽은 것은 제안이지 판정이 아니다]
/// </para>
///
/// <para>
/// 규칙에 안 맞는 번호판(옛 꼴 · 임시 · 외교 · 건설기계)을 억지로 밀어붙이지
/// 않는다. 못 읽으면 <c>UNKNOWN</c> 이고, 읽었더라도 사람이 덮어쓸 수 있다.
/// 덮어썼는지는 <b>번호판이 말한 것</b>과 <b>사람이 고른 것</b>을 나란히 저장해
/// 두었으므로 견주어 보면 안다 — 따로 표시를 둘 필요가 없다.
/// </para>
///
/// <para>
/// [규칙을 자료가 아니라 코드에 둔 까닭]
/// </para>
///
/// <para>
/// 할인율 표는 <c>toll_discount_band</c> 에 줄로 넣었는데 이것은 코드에 둔다.
/// 할인율은 <b>한시 제도</b>라 자주 바뀌고 지난 계산을 그때 규칙으로 되살려야
/// 하지만, 번호판 규칙은 고시라 좀처럼 안 바뀌고 <b>지난 것을 되살릴 일도 없다</b> —
/// 한 번 읽은 번호판의 뜻이 나중에 달라지지 않는다.
/// </para>
/// </summary>
public static class PlateNumber
{
    // 지역명(2자) · 차종 숫자(2~3자) · 용도 한글(1자) · 일련번호(4자).
    // 사이 공백은 사람마다 넣는 자리가 달라서(「서울 80바 1234」 · 「서울80바1234」)
    // 어디에 있든 받아 준다.
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

    /// <summary>
    /// 앞자리 숫자가 말하는 차종.
    ///
    /// <para>
    /// 두 자리는 <c>01~69</c> 승용 · <c>70~79</c> 승합 · <c>80~97</c> 화물 ·
    /// <c>98~99</c> 특수다.
    /// </para>
    ///
    /// <para>
    /// 2019년 9월부터 세 자리 번호판이 생겼고 같은 구간을 열 배로 늘려 쓴다.
    /// 다만 그것은 <b>비사업용 승용차</b>에 발급된 것이라 화물차에서는 사실상
    /// 나오지 않는다. 그래도 받아 두되, 구간 밖이면 <c>UNKNOWN</c> 이다 —
    /// 틀리게 단정하는 것보다 모른다고 두는 편이 낫다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 한글 한 자가 말하는 용도.
    ///
    /// <para>
    /// 특별한 뜻을 가진 몇 자만 꼽고 <b>나머지는 비사업용</b>으로 본다. 비사업용
    /// 32자를 늘어놓는 쪽이 아니라 이쪽으로 짠 까닭은, 글자가 늘었을 때
    /// 빠뜨린 글자가 조용히 「알 수 없음」이 되기 때문이다 — 그러면 멀쩡한
    /// 자가용이 판정에서 사라진다.
    /// </para>
    /// </summary>
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
