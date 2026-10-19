namespace CargoTrustServer.Common;

// 코드값은 docs/cargotrust/05-api-design.md 의 코드표 그대로다. DB 와 JSON 에 이름 문자열로 나가므로 멤버 이름이 곧 코드다.

/// <summary>결제 상태</summary>
public enum PaymentStatus { SCHEDULED, PAID, DELAYED, PARTIAL, UNPAID, DISPUTE }

/// <summary>아직 다 받지 못한 것의 정의. 미수금과 결제 등록 화면이 같은 묶음을 봐야 하므로 한 곳에만 둔다.</summary>
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

/// <summary>구간 유형. 할인 규칙이 여기서 갈린다 — 폐쇄식은 야간 비율로, 개방식은 통과 시각 한 점으로 본다.</summary>
public enum SectionType { CLOSED, OPEN }

/// <summary>통행료 차종. 할인율의 띠를 바꾸지 않고 금액과 심야할인 대상 여부만 가른다.</summary>
public enum VehicleClass { LIGHT, C1, C2, C3, C4, C5 }

/// <summary>
/// 번호판 앞자리 숫자가 말하는 차종. <b>통행료 차종(<see cref="VehicleClass"/>)과 다른 것이다</b> —
/// 이쪽은 차의 생김새이고, 저쪽은 요금을 매기는 구분이다(축수가 가른다).
/// </summary>
public enum VehicleKind { UNKNOWN, PASSENGER, VAN, FREIGHT, SPECIAL }

/// <summary>번호판 한글 한 자가 말하는 용도.</summary>
public enum PlateUsage { UNKNOWN, PRIVATE, BUSINESS, DELIVERY, RENTAL }

/// <summary>요청 본문의 코드값을 읽는다. 코드 칸은 enum 이 아니라 문자열로 받아야 틀린 값의 까닭을 화면이 보여 줄 수 있다.</summary>
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
