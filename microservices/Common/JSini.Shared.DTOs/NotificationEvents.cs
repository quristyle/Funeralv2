namespace JSini.Shared.DTOs;

/// <summary>
/// 알림 <b>이벤트</b> 코드값.
/// </summary>
/// <remarks>
/// <para>
/// [<see cref="PushCategories"/> 와 무엇이 다른가]
/// </para>
///
/// <para>
/// 구분(<c>category</c>)은 <b>쌓인 기록을 갈래로 묶는 이름표</b>다 — 알림함과
/// 발송 이력이 그것으로 거른다. 이벤트는 <b>「어떤 일이 일어났을 때 보내는
/// 알림인가」</b>이고, 포털관리의 「알림관리」 화면이 역할·채널을 매다는 열쇠가
/// 그것이다.
/// </para>
///
/// <para>
/// 지금은 <b>이벤트 코드가 구분 코드와 같은 글자</b>인 것이 대부분이다 — 실제로
/// 알림이 나가는 자리를 세어 보니 구분 하나에 사건 하나였기 때문이다. 같은
/// 구분 아래에서 사건을 더 잘게 갈라야 할 날이 오면 <c>구분.하위</c> 꼴로 적고
/// (<see cref="ReportMail"/> 처럼 구분이 없는 것은 제 이름을 쓴다) 보내는 쪽이
/// <c>eventCode</c> 를 실어 보내면 된다. 정책을 푸는 쪽은 <b>정확히 맞는 줄 →
/// 앞머리(구분) 줄 → 둘 다 없으면 제한 없음</b> 순서로 본다
/// (<c>NotificationPolicyService.ResolveAsync</c>).
/// </para>
///
/// <para>
/// [여기 없는 코드를 보내도 된다]
/// </para>
///
/// <para>
/// 정본은 DB 의 <c>scom.notification_events</c> 다. 이 상수들은 <b>보내는 자리가
/// 적을 글자</b>일 뿐이고, 사람이 읽는 이름과 설명·대상 성격은 그 표가 갖는다.
/// 표에 없는 코드로 보내면 <b>정책이 안 걸린 채 그냥 나간다</b> — 조용히 막히지
/// 않는다는 뜻이고, 그것이 이 설계에서 가장 중요한 성질이다.
/// </para>
/// </remarks>
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

    /// <summary>AI 작업이 요청되었거나 끝났다. 구분 <c>AI_TASK</c>.</summary>
    public const string AiTask = "AI_TASK";

    /// <summary>기상 특보·실황·내 위치 날씨. 구분 <c>WEATHER</c>.</summary>
    public const string Weather = "WEATHER";

    /// <summary>
    /// 보고서 메일(주기 발송). <b>구분이 없다</b> — 메일로만 나가고 알림함에
    /// 쌓이지 않아 붙일 갈래가 없었다.
    /// </summary>
    public const string ReportMail = "REPORT_MAIL";

    /// <summary>회사 소개 사이트의 문의가 접수되었다. 구분이 없다.</summary>
    public const string SiteInquiry = "SITE_INQUIRY";

    /// <summary>
    /// 계정 안내 메일 — 비밀번호 재설정 · 가입 승인·거절 안내.
    /// </summary>
    /// <remarks>
    /// <b>정책이 걸리지 않는다</b>(표의 <c>governed = false</c>). 업무 메일이라
    /// 막으면 사용자가 얻는 것이 아무것도 없는데 화면에는 「보냈습니다」가 뜬다.
    /// 그래도 코드를 적어 보내는 까닭은 <b>알림관리 목록에 이 메일이 실제로
    /// 있기 때문</b>이다 — 안 적으면 「구분 없는 메일」로 섞여 어디서 나가는지
    /// 알 수 없다.
    /// </remarks>
    public const string AccountMail = "ACCOUNT_MAIL";

    /// <summary>
    /// 보관할 꼴로 다듬는다. 비었으면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="PushCategories.Normalize"/> 와 같은 규칙이다 — 대문자로 맞추고
    /// 앞뒤 공백을 걷는다. 코드가 흩어진 서비스에서 적어 오므로 글자가 갈리면
    /// 정책이 조용히 안 걸린다.
    /// </remarks>
    public static string? Normalize(string? eventCode)
        => string.IsNullOrWhiteSpace(eventCode) ? null : eventCode.Trim().ToUpperInvariant();

    /// <summary>
    /// <c>구분.하위</c> 에서 앞머리(구분)만. 점이 없으면 그대로 돌려준다.
    /// </summary>
    /// <remarks>
    /// 잘게 나눈 이벤트에 정책 줄이 없을 때 <b>구분 줄로 한 번 더 묻는</b>
    /// 자리에서 쓴다. 그래야 「HELPDESK 를 설정해 두었는데 세부 사건 하나가
    /// 그물을 빠져나간다」가 안 생긴다.
    /// </remarks>
    public static string? RootOf(string? eventCode)
    {
        var normalized = Normalize(eventCode);
        if (normalized is null) return null;

        var dot = normalized.IndexOf('.');
        return dot <= 0 ? normalized : normalized[..dot];
    }
}

/// <summary>
/// 알림이 나가는 <b>길</b>. 「알림관리」 화면의 체크 칸 하나가 이것 하나다.
/// </summary>
/// <remarks>
/// <para>
/// 카카오 알림톡(<c>IKakaoAlimtalkSender</c>)은 확장점만 서 있고 실제로 나가는
/// 길이 아직 없다(<c>Kakao:Enabled</c> 가 거짓이고 받을 번호를 들고 있는 표도
/// 없다). <b>눌러도 아무 일 없는 체크 칸을 만들지 않는다</b> — 그 길이 열리는
/// 날 여기에 값 하나와 표에 칸 하나를 더한다.
/// </para>
/// </remarks>
public enum NotificationChannel
{
    /// <summary>브라우저 웹푸시(PWA 앱 푸시).</summary>
    Push = 0,

    /// <summary>이메일(SMTP 직발송).</summary>
    Email = 1,
}
