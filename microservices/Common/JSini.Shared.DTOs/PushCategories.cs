namespace JSini.Shared.DTOs;

/// <summary>
/// 알림구분(<c>category</c>) 코드값. 정본은 공통코드 묶음 <c>NOTI_CATEGORY</c> 이고, 여기 상수는 보내는 쪽이 적을 이름표일 뿐이다.
/// 그래서 이 파일에 없는 구분을 보내도 되며, 서비스마다 글자를 따로 적으면 같은 구분이 갈라지므로 상수를 공용 프로젝트에 둔다.
/// </summary>
public static class PushCategories
{
    /// <summary>공통코드 묶음 코드. 화면이 이 이름으로 목록을 읽는다.</summary>
    public const string GroupCode = "NOTI_CATEGORY";

    /// <summary>사람이 손으로 보낸 공지·안내(포털관리 「메시지 발송」 따위).</summary>
    public const string Notice = "NOTICE";

    /// <summary>배포 결과 알림.</summary>
    public const string Deploy = "DEPLOY";

    /// <summary>기상 특보·예보 알림.</summary>
    public const string Weather = "WEATHER";

    /// <summary>쪽지가 왔다는 알림.</summary>
    public const string Note = "NOTE";

    /// <summary>생일 축하 알림.</summary>
    public const string Birthday = "BIRTHDAY";

    /// <summary>가입 신청이 들어왔다는 알림.</summary>
    public const string Signup = "SIGNUP";

    /// <summary>새 기기가 알림을 구독했다는 알림.</summary>
    public const string Subscription = "SUBSCRIPTION";

    /// <summary>헬프데스크 요청 알림.</summary>
    public const string HelpDesk = "HELPDESK";

/// <summary>
/// 내 요청글에 댓글이 달렸다는 알림. <see cref="HelpDesk"/> 와 갈래를 나눈 것은 받는 사람이 이것만 따로 끌 수 있어서다.
/// </summary>
    public const string HelpDeskComment = "HELPDESK_COMMENT";

/// <summary>내가 쓴 요청글이 접수·완료·종료 되었다는 알림. 받는 사람과 끄는 자리가 <see cref="HelpDesk"/> 와 다르다.</summary>
    public const string HelpDeskStatus = "HELPDESK_STATUS";

    /// <summary>AI 작업 요청·결과 알림.</summary>
    public const string AiTask = "AI_TASK";

/// <summary>회사 소개 사이트 문의 접수 알림.</summary>
    public const string SiteInquiry = "SITE_INQUIRY";

    /// <summary>설정 화면의 「시험 발송」.</summary>
    public const string Test = "TEST";

/// <summary>구분이 없는 줄을 찾는 조회용 이름표. 저장되는 값이 아니며 공통코드에 같은 값을 만들면 안 된다.</summary>
    public const string Unset = "__unset__";

/// <summary>
/// 보관할 꼴로 다듬는다. 비었으면 <c>null</c> — 빈 글자로 저장하지 않는다(「없음」이 두 모양이 되면 조회가 갈린다).
/// </summary>
    public static string? Normalize(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;

        var trimmed = category.Trim();

        return string.Equals(trimmed, Unset, StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed.ToUpperInvariant();
    }
}
