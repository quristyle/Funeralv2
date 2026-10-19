using System.Globalization;
using JSini.Web.Components.Data;

namespace JSini.Web.CargoTrust.Api;

/// <summary>
/// 서버 코드값(대문자 문자열)을 이름과 배지 색으로 푸는 한 곳. 화면마다 switch 를 쓰면 같은 DELAYED 가 곳마다 다른 말·색으로 읽힌다.
/// 판정어(악성·위험·사기 등)는 쓰지 않는다. 이름은 코드표(05-api-design.md) 그대로다.
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
    /// 배지 색. 정상·지연은 받을 것을 다 받은 상태라 미지급과 같은 색을 주지 않는다 — 지연 지급은 늦었어도 받은 것이다.
    /// </summary>
    public static string PaymentStatusBadge(string? code) => code switch
    {
        Paid => "jsini-badge jsini-badge--on",
        Delayed or Partial => "jsini-badge jsini-badge--warn",
        Unpaid or Dispute => "jsini-badge jsini-badge--err",
        _ => "jsini-badge jsini-badge--off",
    };

    /// <summary>
    /// 결제 결과를 더 받을 수 있는 상태인가. 서버의 미수금 정의(SCHEDULED·PARTIAL·UNPAID·DISPUTE)와 같다 — 미지급으로 적었다가 뒤늦게 받는 일이 흔하다.
    /// </summary>
    public static bool IsOpen(string? code) =>
        code is Scheduled or Partial or Unpaid or Dispute;

    /// <summary>
    /// 미수금 칸(bucket). DELAYED 는 「늦게 받았다」가 아니라 「예정일이 지났는데 아직 못 받았다」를 뜻한다.
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
    /// 거래 검증 상태. FLAGGED 를 「의심」이라 부르지 않는다 — 같은 날 많이 올렸다는 표시일 뿐이라 사람을 판정하는 말이 되면 안 된다.
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
    /// 건수가 적은 통계를 크게 읽지 않게 한다(설계안 27). 2건 중 2건과 2,000건 중 1,700건을 같은 무게로 보이면 안 된다.
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
    /// 운송 유형 고르개. 맨 줄은 「고르지 않음」이다 — 입력 칸이라 「전체」로 두면 모든 유형이라는 뜻으로 읽힌다.
    /// </summary>
    public static readonly IReadOnlyList<SchOption> TransportTypeOptions =
        [new(null, "고르지 않음"), .. TransportTypes.Select(t => new SchOption(t, t))];

    // ── 톨게이트 심야할인 ────────────────────────────────────

    public const string Closed = "CLOSED";
    public const string Open = "OPEN";

    /// <summary>
    /// 구간 유형. 이 한 칸이 할인 규칙을 통째로 바꾼다(야간창도, 세는 법도 다르다). 그래서 「전체」가 없다.
    /// </summary>
    public static readonly IReadOnlyList<SchOption> SectionTypeOptions =
    [
        new(Closed, "폐쇄식 (진입·진출 영업소가 다름)"),
        new(Open, "개방식 (요금소를 한 번 지남)"),
    ];

    /// <summary>
    /// 칩에 쓰는 짧은 이름. 긴 이름은 390px 에서 칩 하나가 두 줄을 먹으니 설명은 칩 아래로 내린다.
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
