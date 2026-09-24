using JSini.Web.Components.Data;

namespace JSini.Web.CargoTrust.Admin.Api;

/// <summary>
/// 코드 하나의 이름과 딱지 색.
/// </summary>
/// <param name="Code">서버가 주는 대문자 코드.</param>
/// <param name="Name">사람이 읽는 이름.</param>
/// <param name="Tone">
/// 딱지 꾸밈 — <c>on</c> · <c>off</c> · <c>warn</c> · <c>err</c> 또는 <c>null</c>(기본 테두리).
/// </param>
public sealed record CargoCode(string Code, string Name, string? Tone = null);

/// <summary>
/// 한 종류의 코드 묶음(결제 상태 · 신고 사유 …). 이름 · 딱지 · 고르개 목록을 한 벌로 낸다.
/// </summary>
public sealed class CargoCodeSet
{
    private readonly Dictionary<string, CargoCode> _byCode;

    public CargoCodeSet(params CargoCode[] codes)
    {
        All = codes;
        _byCode = codes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        Options = [.. codes.Select(c => new SchOption(c.Code, c.Name))];
        Filter = [new SchOption(null, SchSummary.Any), .. Options];
    }

    /// <summary>정해 둔 차례 그대로의 코드들.</summary>
    public IReadOnlyList<CargoCode> All { get; }

    /// <summary>편집 창의 고르개 — 「전체」가 없다.</summary>
    public IReadOnlyList<SchOption> Options { get; }

    /// <summary>조회 조건의 고르개 — 맨 앞이 「전체」(값 <c>null</c>).</summary>
    public IReadOnlyList<SchOption> Filter { get; }

    /// <summary>
    /// 이름. <b>모르는 코드는 글자 그대로 돌려준다</b> — 서버가 코드를 하나 더했을 때
    /// 빈칸이 되면 「값이 없다」로 읽힌다.
    /// </summary>
    public string NameOf(string? code) =>
        string.IsNullOrWhiteSpace(code) ? "-"
        : _byCode.TryGetValue(code, out var hit) ? hit.Name
        : code;

    /// <summary>딱지 클래스(<c>jsini-badge</c> + 꾸밈).</summary>
    public string BadgeOf(string? code) =>
        code is not null && _byCode.TryGetValue(code, out var hit) && hit.Tone is { } tone
            ? $"jsini-badge jsini-badge--{tone}"
            : "jsini-badge";
}

/// <summary>
/// 운송관리 관리자 화면이 쓰는 코드표. 정본은 docs/cargotrust/05-api-design.md 의 「코드표」다.
/// </summary>
/// <remarks>
/// <para>
/// [한 곳에 두는 이유]
/// </para>
///
/// <para>
/// 같은 코드가 여러 화면에 나온다 — 결제 상태만 해도 대시보드 · 거래 · 결제 · 통계
/// 넷이다. 화면마다 <c>switch</c> 를 적으면 한 화면에서만 「지연 지급」이 「지연」이
/// 되고, 딱지 색이 화면마다 갈린다. 여기서 이름과 색을 함께 정한다.
/// </para>
///
/// <para>
/// [색은 판정이 아니다]
/// </para>
///
/// <para>
/// 설계안 12절 — 이 서비스는 업체를 판정하지 않는다. 딱지 색은 <b>처리가 필요한가</b>
/// 를 관리자에게 알리는 것뿐이고, 이름에 「악성」·「위험」 같은 말을 쓰지 않는다.
/// </para>
/// </remarks>
public static class CargoAdminCodes
{
    /// <summary>결제 상태(<c>PaymentStatus</c>).</summary>
    public static readonly CargoCodeSet PaymentStatus = new(
        new("SCHEDULED", "예정"),
        new("PAID", "정상 지급", "on"),
        new("DELAYED", "지연 지급", "warn"),
        new("PARTIAL", "일부 지급", "warn"),
        new("UNPAID", "미지급", "err"),
        new("DISPUTE", "분쟁", "err"));

    /// <summary>거래 검증 상태(<c>ReviewStatus</c>). 의심(FLAGGED)이 관리자가 먼저 볼 것이다.</summary>
    public static readonly CargoCodeSet ReviewStatus = new(
        new("NORMAL", "일반"),
        new("FLAGGED", "의심", "err"),
        new("VERIFIED", "확인됨", "on"),
        new("HIDDEN", "통계 제외", "off"));

    /// <summary>사용자 유형(<c>UserType</c>).</summary>
    public static readonly CargoCodeSet UserType = new(
        new("DRIVER", "차주"),
        new("CARRIER", "운송사/주선사"),
        new("ADMIN", "관리자", "on"));

    /// <summary>사용자 상태(<c>UserStatus</c>).</summary>
    public static readonly CargoCodeSet UserStatus = new(
        new("ACTIVE", "정상", "on"),
        new("BLOCKED", "차단", "err"));

    /// <summary>거래처 상태(<c>CompanyStatus</c>).</summary>
    public static readonly CargoCodeSet CompanyStatus = new(
        new("ACTIVE", "정상", "on"),
        new("CLOSED", "폐업", "off"),
        new("HIDDEN", "숨김", "warn"));

    /// <summary>후기 노출(<c>ReviewVisibility</c>).</summary>
    public static readonly CargoCodeSet ReviewVisibility = new(
        new("VISIBLE", "보임", "on"),
        new("HIDDEN", "숨김", "off"));

    /// <summary>이의제기 사유(<c>DisputeReason</c>).</summary>
    public static readonly CargoCodeSet DisputeReason = new(
        new("NO_TRANSACTION", "거래 사실 없음"),
        new("ALREADY_PAID", "이미 지급 완료"),
        new("WRONG_AMOUNT", "금액 오류"),
        new("WRONG_COMPANY", "다른 업체"),
        new("OTHER", "기타"));

    /// <summary>이의제기 상태(<c>DisputeStatus</c>). 접수 · 검토중이 처리 대기다.</summary>
    public static readonly CargoCodeSet DisputeStatus = new(
        new("RECEIVED", "접수", "warn"),
        new("REVIEWING", "검토중", "warn"),
        new("ACCEPTED", "인용", "on"),
        new("REJECTED", "기각", "off"));

    /// <summary>신고 대상(<c>ReportTarget</c>).</summary>
    public static readonly CargoCodeSet ReportTarget = new(
        new("COMPANY", "거래처"),
        new("TRANSACTION", "거래"),
        new("REVIEW", "후기"));

    /// <summary>신고 사유(<c>ReportReason</c>).</summary>
    public static readonly CargoCodeSet ReportReason = new(
        new("FAKE", "허위 거래"),
        new("DUPLICATE", "중복 거래"),
        new("WRONG_COMPANY", "사업자 오인"),
        new("ABUSE", "욕설/비방"),
        new("PRIVACY", "개인정보 노출"),
        new("SPAM", "광고/스팸"),
        new("OTHER", "기타"));

    /// <summary>신고 처리 상태(<c>ReportStatus</c>). 접수 · 검토중이 처리 대기다.</summary>
    public static readonly CargoCodeSet ReportStatus = new(
        new("RECEIVED", "접수", "warn"),
        new("REVIEWING", "검토중", "warn"),
        new("REJECTED", "반려", "off"),
        new("REVISION", "수정 요청"),
        new("DELETED", "삭제", "off"),
        new("DONE", "처리 완료", "on"));

    /// <summary>
    /// 감사 기록의 대상 종류.
    ///
    /// <para>
    /// <b>계약서에 값이 적혀 있지 않다.</b> 관리자가 바꿀 수 있는 것(거래처 · 거래 ·
    /// 후기 · 신고 · 이의제기 · 사용자)과 사용자가 바꾸는 거래 · 결제에서 뽑았다.
    /// 서버가 다른 글자를 쓰면 표에는 그 글자가 그대로 보이고(<see cref="CargoCodeSet.NameOf"/>),
    /// 조회 고르개에서만 빠진다 — 그때 여기를 맞춘다.
    /// </para>
    /// </summary>
    public static readonly CargoCodeSet AuditTarget = new(
        new("COMPANY", "거래처"),
        new("TRANSACTION", "거래"),
        new("PAYMENT", "결제"),
        new("REVIEW", "후기"),
        new("REPORT", "신고"),
        new("DISPUTE", "이의제기"),
        new("USER", "사용자"));

    /// <summary>
    /// 신고 · 이의제기가 아직 처리 대기인가 — 접수 · 검토중.
    /// 서버가 <c>resolved_at</c> 을 채우는 기준(「접수·검토중이 아니면」)과 같다.
    /// </summary>
    public static bool IsOpen(string? status) =>
        string.Equals(status, "RECEIVED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "REVIEWING", StringComparison.OrdinalIgnoreCase);
}
