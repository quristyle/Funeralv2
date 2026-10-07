using System.Globalization;
using JSini.Web.Components.Data;

namespace JSini.Web.CargoTrust.Api;

/// <summary>
/// 서버 코드값(대문자 문자열)을 사람이 읽는 이름과 배지 색으로 푸는 한 곳.
///
/// <para>
/// [왜 한 곳인가]
/// </para>
///
/// <para>
/// 같은 <c>DELAYED</c> 가 검색 카드 · 상세 표 · 내 거래 · 미수금 네 곳에 뜬다.
/// 화면마다 <c>switch</c> 를 적으면 한 곳은 「지연」, 다른 곳은 「지연 지급」이
/// 되고, 색까지 갈리면 **같은 상태가 다른 무게로 읽힌다.** 이 서비스는 판정을
/// 하지 않고 숫자만 보여 주는 것이 원칙이라(설계안 12·33) 그 무게가 곧 판정이다.
/// </para>
///
/// <para>
/// [「악성」「위험」「사기」를 쓰지 않는다]
/// </para>
///
/// <para>
/// 이름은 코드표(05-api-design.md) 그대로다. 미지급을 빨강으로 칠하는 것까지가
/// 이 화면이 하는 말의 전부다 — 거래처를 한 단어로 부르는 이름표는 어디에도 없다.
/// </para>
/// </summary>
public static class CargoCodes
{
    // ── 결제 상태 ────────────────────────────────────────────

    public const string Scheduled = "SCHEDULED";
    public const string Paid = "PAID";
    public const string Delayed = "DELAYED";
    public const string Partial = "PARTIAL";
    public const string Unpaid = "UNPAID";
    public const string Dispute = "DISPUTE";

    /// <summary>고르개 순서 = 코드표 순서. 「전체」는 값이 없다(<see cref="SchOption"/>).</summary>
    public static readonly IReadOnlyList<SchOption> PaymentStatusOptions =
    [
        new(null, SchSummary.Any),
        new(Scheduled, "예정"),
        new(Paid, "정상 지급"),
        new(Delayed, "지연 지급"),
        new(Partial, "일부 지급"),
        new(Unpaid, "미지급"),
        new(Dispute, "분쟁"),
    ];

    public static string PaymentStatusName(string? code) => code switch
    {
        Scheduled => "예정",
        Paid => "정상 지급",
        Delayed => "지연 지급",
        Partial => "일부 지급",
        Unpaid => "미지급",
        Dispute => "분쟁",
        null or "" => "-",
        _ => code,
    };

    /// <summary>
    /// 배지 색. 받을 것을 다 받은 둘(정상·지연)과 못 받은 것을 가른다 —
    /// 지연 지급은 늦었어도 **받은 것**이라 미지급과 같은 색을 주지 않는다.
    /// </summary>
    public static string PaymentStatusBadge(string? code) => code switch
    {
        Paid => "jsini-badge jsini-badge--on",
        Delayed or Partial => "jsini-badge jsini-badge--warn",
        Unpaid or Dispute => "jsini-badge jsini-badge--err",
        _ => "jsini-badge jsini-badge--off",
    };

    /// <summary>
    /// 결제 결과를 더 받을 수 있는 상태인가. 서버의 미수금 정의
    /// (<c>SCHEDULED·PARTIAL·UNPAID·DISPUTE</c>)와 같다 — 미지급으로 적었다가
    /// 뒤늦게 받는 일이 흔하다.
    /// </summary>
    public static bool IsOpen(string? code) =>
        code is Scheduled or Partial or Unpaid or Dispute;

    /// <summary>
    /// 미수금 칸(<c>bucket</c>). 상태와 거의 같지만 <c>DELAYED</c> 의 뜻이 다르다 —
    /// 여기서는 「늦게 받았다」가 아니라 「예정일이 지났는데 아직 못 받았다」다.
    /// </summary>
    public static string BucketName(string? code) => code switch
    {
        Scheduled => "정상 예정",
        Delayed => "지급 지연",
        Unpaid => "미지급",
        Partial => "일부 지급",
        Dispute => "분쟁",
        _ => PaymentStatusName(code),
    };

    public static string BucketBadge(string? code) => code switch
    {
        Scheduled => "jsini-badge jsini-badge--off",
        Delayed or Partial => "jsini-badge jsini-badge--warn",
        _ => "jsini-badge jsini-badge--err",
    };

    // ── 거래 검증 상태 ───────────────────────────────────────

    /// <summary>
    /// 거래 검증 상태. <c>FLAGGED</c> 를 「의심」이라 적지 않는다 — 등록한 본인이
    /// 보는 칸인데, 같은 날 열 건을 넘게 올렸다는 것뿐인 표시를 의심이라 부르면
    /// 사람을 판정하는 말이 된다.
    /// </summary>
    public static string ReviewStatusName(string? code) => code switch
    {
        "NORMAL" => "일반",
        "FLAGGED" => "검토 대상",
        "VERIFIED" => "확인됨",
        "HIDDEN" => "통계 제외",
        null or "" => "-",
        _ => code,
    };

    public static string ReviewStatusBadge(string? code) => code switch
    {
        "VERIFIED" => "jsini-badge jsini-badge--on",
        "FLAGGED" => "jsini-badge jsini-badge--warn",
        _ => "jsini-badge jsini-badge--off",
    };

    // ── 사용자 · 회사 ────────────────────────────────────────

    public static string UserTypeName(string? code) => code switch
    {
        "DRIVER" => "차주",
        "CARRIER" => "운송사/주선사",
        "ADMIN" => "관리자",
        null or "" => "-",
        _ => code,
    };

    public static string UserStatusName(string? code) => code switch
    {
        "ACTIVE" => "사용 중",
        "BLOCKED" => "이용 제한",
        null or "" => "-",
        _ => code,
    };

    public static string CompanyStatusName(string? code) => code switch
    {
        "ACTIVE" => "정상",
        "CLOSED" => "폐업",
        "HIDDEN" => "숨김",
        null or "" => "-",
        _ => code,
    };

    public static string CompanyStatusBadge(string? code) => code switch
    {
        "ACTIVE" => "jsini-badge jsini-badge--on",
        "CLOSED" => "jsini-badge jsini-badge--warn",
        _ => "jsini-badge jsini-badge--off",
    };

    // ── 데이터 규모 ──────────────────────────────────────────

    /// <summary>
    /// 건수가 적어 통계를 크게 읽으면 안 되는가(설계안 27). 2건 중 2건 정상과
    /// 2,000건 중 1,700건 정상을 같은 무게로 보이면 안 된다.
    /// </summary>
    public static bool IsThin(string? confidence) => confidence is "NONE" or "LOW";

    public static string ConfidenceBadge(string? confidence) => confidence switch
    {
        "HIGH" => "jsini-badge jsini-badge--on",
        "MEDIUM" => "jsini-badge",
        _ => "jsini-badge jsini-badge--warn",
    };

    /// <summary>서버가 이름을 안 주면(옛 응답) 코드표 기준으로 짓는다.</summary>
    public static string ConfidenceName(string? confidence, string? label) =>
        !string.IsNullOrWhiteSpace(label) ? label! : confidence switch
        {
            "NONE" => "거래 경험 없음",
            "LOW" => "거래 경험 1~4건",
            "MEDIUM" => "거래 경험 5~19건",
            "HIGH" => "거래 경험 20건 이상",
            _ => "-",
        };

    // ── 이의제기 · 신고 ──────────────────────────────────────

    /// <summary>이의 사유. 설계안 14절은 체크칸이지만 서버가 하나만 받으므로 하나를 고른다.</summary>
    public static readonly IReadOnlyList<SchOption> DisputeReasonOptions =
    [
        new("NO_TRANSACTION", "거래 사실 없음"),
        new("ALREADY_PAID", "이미 지급 완료"),
        new("WRONG_AMOUNT", "금액 오류"),
        new("WRONG_COMPANY", "다른 업체"),
        new("OTHER", "기타"),
    ];

    public static string DisputeReasonName(string? code) => NameIn(DisputeReasonOptions, code);

    public static string DisputeStatusName(string? code) => code switch
    {
        "RECEIVED" => "접수",
        "REVIEWING" => "검토중",
        "ACCEPTED" => "인용",
        "REJECTED" => "기각",
        null or "" => "-",
        _ => code,
    };

    public static string DisputeStatusBadge(string? code) => code switch
    {
        "ACCEPTED" => "jsini-badge jsini-badge--on",
        "REJECTED" => "jsini-badge jsini-badge--off",
        _ => "jsini-badge jsini-badge--warn",
    };

    public static readonly IReadOnlyList<SchOption> ReportReasonOptions =
    [
        new("FAKE", "허위 거래"),
        new("DUPLICATE", "중복 거래"),
        new("WRONG_COMPANY", "사업자 오인"),
        new("ABUSE", "욕설/비방"),
        new("PRIVACY", "개인정보 노출"),
        new("SPAM", "광고/스팸"),
        new("OTHER", "기타"),
    ];

    public static string ReportReasonName(string? code) => NameIn(ReportReasonOptions, code);

    public static readonly IReadOnlyList<SchOption> ReportStatusOptions =
    [
        new(null, SchSummary.Any),
        new("RECEIVED", "접수"),
        new("REVIEWING", "검토중"),
        new("REJECTED", "반려"),
        new("REVISION", "수정 요청"),
        new("DELETED", "삭제"),
        new("DONE", "처리 완료"),
    ];

    public static string ReportStatusName(string? code) =>
        string.IsNullOrEmpty(code) ? "-" : NameIn(ReportStatusOptions, code);

    public static string ReportStatusBadge(string? code) => code switch
    {
        "DONE" or "DELETED" => "jsini-badge jsini-badge--on",
        "REJECTED" => "jsini-badge jsini-badge--off",
        _ => "jsini-badge jsini-badge--warn",
    };

    public static string ReportTargetName(string? code) => code switch
    {
        "COMPANY" => "거래처",
        "TRANSACTION" => "거래",
        "REVIEW" => "후기",
        null or "" => "-",
        _ => code,
    };

    /// <summary>운송 유형 권장값. 자유 입력이라 고르개는 제안일 뿐이다.</summary>
    public static readonly IReadOnlyList<string> TransportTypes =
        ["일반", "냉장/냉동", "컨테이너", "중량물", "이사", "기타"];

    /// <summary>
    /// 운송 유형 고르개(<c>NativeSelect</c>). 맨 줄은 「고르지 않음」이다 —
    /// 이 칸은 비워 둔 채로도 거래가 등록된다(<c>TransactionSaveRequest</c>).
    ///
    /// <para>
    /// 「전체」가 아니라 「고르지 않음」인 까닭은 **조회 조건이 아니라 입력 칸**
    /// 이어서다. 적는 자리에서 「전체」는 모든 유형이라는 뜻으로 읽힌다.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<SchOption> TransportTypeOptions =
        [new(null, "고르지 않음"), .. TransportTypes.Select(t => new SchOption(t, t))];

    // ── 톨게이트 심야할인 ────────────────────────────────────

    public const string Closed = "CLOSED";
    public const string Open = "OPEN";

    /// <summary>
    /// 구간 유형. <b>이 한 칸이 할인 규칙을 통째로 바꾼다</b> —
    /// 야간창도 다르고(21~06 vs 23~05) 세는 법도 다르다(비율 vs 통과 한 점).
    /// 그래서 「전체」가 없다. 반드시 하나를 고른다.
    /// </summary>
    public static readonly IReadOnlyList<SchOption> SectionTypeOptions =
    [
        new(Closed, "폐쇄식 (진입·진출 영업소가 다름)"),
        new(Open, "개방식 (요금소를 한 번 지남)"),
    ];

    /// <summary>
    /// 칩에 쓰는 짧은 이름. 설명은 칩 아래 한 줄로 내린다 —
    /// 390px 에서 긴 이름은 칩 하나가 두 줄을 먹고, 그러면 고를 것이 넷인 화면이
    /// 손가락 하나만큼도 안 남는다.
    /// </summary>
    public static readonly IReadOnlyList<SchOption> SectionTypeChips =
    [
        new(Closed, "폐쇄식"),
        new(Open, "개방식"),
    ];

    /// <summary>칩 아래에 적는 설명.</summary>
    public static string SectionTypeNote(string? code) => code switch
    {
        Open => "요금소를 한 번 지나는 구간입니다. 통과 시각 한 점으로 셉니다.",
        _ => "진입·진출 영업소가 다른 구간입니다. 야간에 머문 비율로 셉니다.",
    };

    public static string SectionTypeName(string? code) => code switch
    {
        Closed => "폐쇄식",
        Open => "개방식",
        null or "" => "-",
        _ => code,
    };

    /// <summary>고속도로 통행료 차종. 할인율의 띠를 바꾸지 않는다 — 금액과 대상 자격만 가른다.</summary>
    public static readonly IReadOnlyList<SchOption> VehicleClassOptions =
    [
        new("LIGHT", "경차"),
        new("C1", "1종 (승용 · 2.5톤 미만 화물)"),
        new("C2", "2종 (2.5~5.5톤 화물)"),
        new("C3", "3종 (5.5~10톤 화물)"),
        new("C4", "4종 (3축 · 10~20톤 화물)"),
        new("C5", "5종 (4축 이상 · 20톤 이상 화물)"),
    ];

    public static string VehicleClassName(string? code) => NameIn(VehicleClassOptions, code);

    /// <summary>「5시간 40분」 — 분으로만 적으면 긴 운행에서 크기가 안 읽힌다.</summary>
    public static string Minutes(int? minutes)
    {
        if (minutes is null) return "-";
        var value = minutes.Value;
        if (value < 60) return $"{value}분";
        var h = value / 60;
        var m = value % 60;
        return m == 0 ? $"{h}시간" : $"{h}시간 {m}분";
    }

    /// <summary>KST 벽시계 — 서버가 오프셋 없이 주므로 <b>그대로</b> 적는다.</summary>
    public static string Kst(DateTime? at) =>
        at?.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";

    public static string KstFull(DateTime? at) =>
        at?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";

    /// <summary>할인율이 클수록 진한 배지. 0% 는 배지를 달지 않는다(알릴 것이 없다).</summary>
    public static string DiscountBadge(decimal percent) => percent switch
    {
        >= 50m => "jsini-badge jsini-badge--on",
        > 0m => "jsini-badge jsini-badge--warn",
        _ => "jsini-badge jsini-badge--off",
    };

    // ── 서식 ─────────────────────────────────────────────────

    private static readonly CultureInfo Ko = CultureInfo.GetCultureInfo("ko-KR");

    public static string Won(decimal amount) => amount.ToString("#,0", Ko) + "원";

    public static string Day(DateOnly? day) =>
        day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-";

    public static string Moment(DateTime? at) =>
        at?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";

    /// <summary>평균 일수. 값이 없으면 「-」 — 0일과 「잴 거래가 없다」는 다르다.</summary>
    public static string Days(double? days) =>
        days is null ? "-" : days.Value.ToString("0.#", CultureInfo.InvariantCulture) + "일";

    public static string Days(int? days) => days is null ? "-" : $"{days}일";

    public static string Rate(decimal? rate) =>
        rate is null ? "-" : rate.Value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static string RouteText(string? from, string? to) =>
        string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)
            ? "-"
            : $"{(string.IsNullOrWhiteSpace(from) ? "?" : from)} → {(string.IsNullOrWhiteSpace(to) ? "?" : to)}";

    private static string NameIn(IReadOnlyList<SchOption> options, string? code) =>
        options.FirstOrDefault(o => o.Value == code)?.Text ?? (string.IsNullOrEmpty(code) ? "-" : code);
}
