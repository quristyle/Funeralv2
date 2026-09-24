namespace CargoTrustServer.Common;

// 코드값은 docs/cargotrust/05-api-design.md 「코드표」 그대로다.
// DB 에는 이름 문자열로, JSON 에도 이름 문자열로 나간다 — 그래서 멤버 이름이 곧 코드다.
// 대문자 멤버 이름이 C# 관례와 어긋나지만, 이름을 바꾸면 DB 값과 계약이 함께 깨진다.

/// <summary>결제 상태</summary>
public enum PaymentStatus { SCHEDULED, PAID, DELAYED, PARTIAL, UNPAID, DISPUTE }

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
