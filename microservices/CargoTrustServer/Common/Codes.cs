namespace CargoTrustServer.Common;

// 코드값은 docs/cargotrust/05-api-design.md 「코드표」 그대로다.
// DB 에는 이름 문자열로, JSON 에도 이름 문자열로 나간다 — 그래서 멤버 이름이 곧 코드다.
// 대문자 멤버 이름이 C# 관례와 어긋나지만, 이름을 바꾸면 DB 값과 계약이 함께 깨진다.

/// <summary>결제 상태</summary>
public enum PaymentStatus { SCHEDULED, PAID, DELAYED, PARTIAL, UNPAID, DISPUTE }

/// <summary>
/// 「아직 다 받지 못한 것」의 정의. <b>한 곳에만 둔다.</b>
///
/// <para>
/// 미수금(<c>/receivables</c>)과 결제 등록 화면이 같은 묶음을 봐야 한다 —
/// 한쪽에 있고 다른 쪽에 없으면 사용자는 「받을 돈이 사라졌다」로 읽는다.
/// 예전에는 이 목록이 서버의 미수금 쪽과 화면 쪽에 따로 적혀 있었다.
/// </para>
/// </summary>
public static class Receivable
{
    /// <summary>미수금에 드는 상태 — 다 받은 것(PAID · DELAYED)을 뺀 나머지.</summary>
    public static readonly PaymentStatus[] Statuses =
        [PaymentStatus.SCHEDULED, PaymentStatus.PARTIAL, PaymentStatus.UNPAID, PaymentStatus.DISPUTE];
}

/// <summary>거래 검증 상태</summary>
public enum ReviewStatus { NORMAL, FLAGGED, VERIFIED, HIDDEN }

/// <summary>사용자 유형</summary>
public enum UserType { DRIVER, CARRIER, ADMIN }

/// <summary>사용자 상태</summary>
public enum UserStatus { ACTIVE, BLOCKED }

/// <summary>거래처 상태</summary>
public enum CompanyStatus { ACTIVE, CLOSED, HIDDEN }

/// <summary>후기 공개 여부</summary>
public enum ReviewVisibility { VISIBLE, HIDDEN }

/// <summary>이의제기 사유</summary>
public enum DisputeReason { NO_TRANSACTION, ALREADY_PAID, WRONG_AMOUNT, WRONG_COMPANY, OTHER }

/// <summary>이의제기 상태</summary>
public enum DisputeStatus { RECEIVED, REVIEWING, ACCEPTED, REJECTED }

/// <summary>신고 대상</summary>
public enum ReportTarget { COMPANY, TRANSACTION, REVIEW }

/// <summary>신고 사유</summary>
public enum ReportReason { FAKE, DUPLICATE, WRONG_COMPANY, ABUSE, PRIVACY, SPAM, OTHER }

/// <summary>신고 처리 상태</summary>
public enum ReportStatus { RECEIVED, REVIEWING, REJECTED, REVISION, DELETED, DONE }

/// <summary>통계의 데이터 규모</summary>
public enum Confidence { NONE, LOW, MEDIUM, HIGH }

// ── 톨게이트 심야할인 ──────────────────────────────────────────

/// <summary>
/// 구간 유형. <b>할인 규칙이 여기서 갈린다</b> — 야간창도 다르고 세는 법도 다르다.
///
/// <para>
/// 폐쇄식은 진입·진출 영업소가 나뉘어 「얼마나 오래 밤에 있었나」를 비율로 재고,
/// 개방식은 요금소를 한 번 지날 뿐이라 <b>통과 시각 한 점</b>으로 본다.
/// </para>
/// </summary>
public enum SectionType { CLOSED, OPEN }

/// <summary>
/// 고속도로 통행료 차종. <b>할인율의 띠를 바꾸지 않는다</b> —
/// 금액과 「심야할인 대상인가」만 가른다.
/// </summary>
public enum VehicleClass { LIGHT, C1, C2, C3, C4, C5 }

/// <summary>
/// 번호판 앞자리 숫자가 말하는 차종. <b>통행료 차종(<see cref="VehicleClass"/>)과 다른 것이다</b> —
/// 이쪽은 차의 생김새이고, 저쪽은 요금을 매기는 구분이다(축수가 가른다).
/// </summary>
public enum VehicleKind { UNKNOWN, PASSENGER, VAN, FREIGHT, SPECIAL }

/// <summary>번호판 한글 한 자가 말하는 용도.</summary>
public enum PlateUsage { UNKNOWN, PRIVATE, BUSINESS, DELIVERY, RENTAL }

/// <summary>
/// 요청 본문의 코드값을 읽는다.
///
/// 요청 DTO 의 코드 칸은 enum 이 아니라 문자열로 받는다. enum 으로 받으면 틀린 값이
/// 바인딩 단계에서 봉투 없는 400 으로 떨어져, 화면이 「무엇이 틀렸는지」를 못 보여 준다.
/// </summary>
public static class Code
{
    /// <summary>비었으면 null, 알 수 없는 값이면 false.</summary>
    public static bool TryParse<T>(string? raw, out T? value) where T : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        // 숫자 문자열("1")은 Enum.TryParse 가 받아 주므로 따로 막는다.
        if (raw.Trim().All(char.IsDigit)) return false;
        if (Enum.TryParse<T>(raw.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    /// <summary>허용 값 목록 — 오류 메시지에 싣는다.</summary>
    public static string Allowed<T>() where T : struct, Enum => string.Join(", ", Enum.GetNames<T>());
}
