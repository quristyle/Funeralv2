namespace JSini.Shared.DTOs;

/// <summary>
/// 알림 이벤트 코드값. 정본은 DB 의 <c>scom.notification_events</c> 표이고, 표에 없는 코드는 정책이 안 걸린 채 그냥 나간다.
/// 구분(<c>category</c>)은 기록을 묶는 이름표이고, 이벤트는 정책을 매다는 열쇠다.
/// </summary>
public static class NotificationEvents
{
    /// <summary>사람이 손으로 보내는 공지·안내. 구분 <c>NOTICE</c>.</summary>
    public const string Notice = "NOTICE";

    /// <summary>배포가 끝났다. 구분 <c>DEPLOY</c>.</summary>
    public const string Deploy = "DEPLOY";

    /// <summary>가입 신청이 들어왔다. 구분 <c>SIGNUP</c>.</summary>
    public const string Signup = "SIGNUP";

    /// <summary>생일 축하 메시지가 왔다. 구분 <c>BIRTHDAY</c>.</summary>
    public const string Birthday = "BIRTHDAY";

    /// <summary>새 기기가 알림을 구독했다. 구분 <c>SUBSCRIPTION</c>.</summary>
    public const string Subscription = "SUBSCRIPTION";

    /// <summary>쪽지가 왔다. 구분 <c>NOTE</c>.</summary>
    public const string Note = "NOTE";

    /// <summary>헬프데스크 요청이 올라왔다. 구분 <c>HELPDESK</c>.</summary>
    public const string HelpDesk = "HELPDESK";

    /// <summary>내 요청글에 댓글이 달렸다. 구분 <c>HELPDESK_COMMENT</c>.</summary>
    public const string HelpDeskComment = "HELPDESK_COMMENT";

/// <summary>
/// 내 요청글이 접수·완료·종료 되었다. 구분 <c>HELPDESK_STATUS</c>.
/// 앱 푸시와 메일을 <c>HelpDeskServer</c> 가 각각 내므로 <c>email_from_push</c> 는 거짓이어야 한다.
/// </summary>
    public const string HelpDeskStatus = "HELPDESK_STATUS";

    /// <summary>AI 작업이 요청되었거나 끝났다. 구분 <c>AI_TASK</c>.</summary>
    public const string AiTask = "AI_TASK";

    /// <summary>기상 특보·실황·내 위치 날씨. 구분 <c>WEATHER</c>.</summary>
    public const string Weather = "WEATHER";

/// <summary>보고서 메일(주기 발송). 구분이 없어 알림함에 쌓이지 않는다.</summary>
    public const string ReportMail = "REPORT_MAIL";

/// <summary>
/// 회사 소개 사이트 문의 접수. 구분 <c>SITE_INQUIRY</c>.
/// 메일은 SiteServer 가 따로 내므로 <c>email_from_push</c> 는 거짓이어야 한다.
/// </summary>
    public const string SiteInquiry = "SITE_INQUIRY";

/// <summary>계정 안내 메일(비밀번호 재설정 · 가입 승인·거절). 정책 대상이 아니지만 알림관리 목록에 실제로 있어 코드를 적는다.</summary>
    public const string AccountMail = "ACCOUNT_MAIL";

/// <summary>
/// 보관할 꼴로 다듬는다(대문자·앞뒤 공백 제거). 비었으면 <c>null</c>.
/// <see cref="PushCategories.Normalize"/> 와 규칙이 같아야 한다 — 글자가 갈리면 정책이 조용히 안 걸린다.
/// </summary>
    public static string? Normalize(string? eventCode)
        => string.IsNullOrWhiteSpace(eventCode) ? null : eventCode.Trim().ToUpperInvariant();

/// <summary>
/// <c>구분.하위</c> 에서 앞머리(구분)만. 점이 없으면 그대로 돌려준다.
/// 세부 사건에 정책 줄이 없을 때 구분 줄로 한 번 더 묻는 데 쓴다.
/// </summary>
    public static string? RootOf(string? eventCode)
    {
        var normalized = Normalize(eventCode);
        if (normalized is null) return null;

        var dot = normalized.IndexOf('.');
        return dot <= 0 ? normalized : normalized[..dot];
    }
}

/// <summary>
/// 알림이 나가는 길. 알림관리 화면의 체크 칸 하나가 이것 하나다.
/// 카카오 알림톡은 확장점만 있어 아직 값을 두지 않는다 — 길이 열릴 때 값과 표 칸을 함께 더한다.
/// </summary>
public enum NotificationChannel
{
    /// <summary>브라우저 웹푸시(PWA 앱 푸시).</summary>
    Push = 0,

    /// <summary>이메일(SMTP 직발송).</summary>
    Email = 1,
}
