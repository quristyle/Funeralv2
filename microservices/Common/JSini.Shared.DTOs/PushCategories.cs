namespace JSini.Shared.DTOs;

/// <summary>
/// 알림구분(<c>category</c>) 코드값.
/// </summary>
/// <remarks>
/// <para>
/// [정본은 공통코드다 — 여기 있는 것은 보내는 쪽이 쓰는 이름표뿐이다]
/// </para>
///
/// <para>
/// 사람이 읽는 이름과 목록은 <c>scom.common_codes</c> 의 묶음
/// <see cref="GroupCode"/>(<c>NOTI_CATEGORY</c>)가 갖는다 — 포털관리의
/// 「공통코드」(<c>/admin/system/common-code</c>)에서 늘리고 고친다.
/// <b>여기에 상수를 두는 것은 서비스가 보낼 때 적을 값이 필요하기 때문</b>이고,
/// 화면의 조회 조건과 표에 뜨는 글자는 언제나 공통코드에서 온다.
/// </para>
///
/// <para>
/// 그래서 <b>이 파일에 없는 구분을 보내도 된다.</b> 공통코드에 값을 넣어
/// 두면 조회 조건에 그대로 나온다. 반대로 여기 있는 값을 공통코드에서 지우면
/// 기록은 남되 이름이 안 붙어 코드값 그대로 보인다.
/// </para>
///
/// <para>
/// 상수를 이 공용 프로젝트에 둔 까닭은 <b>보내는 자리가 다섯 서비스에
/// 흩어져 있기 때문</b>이다(알림·인증·헬프데스크·프로젝트관리, 그리고 포털
/// 화면). 서비스마다 글자를 적으면 같은 구분이 <c>DEPLOY</c>·<c>deploy</c>·
/// <c>배포</c> 로 갈라지고, 그러면 구분으로 거르는 일이 조용히 반쪽이 된다.
/// </para>
/// </remarks>
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

    /// <summary>AI 작업 요청·결과 알림.</summary>
    public const string AiTask = "AI_TASK";

    /// <summary>설정 화면의 「시험 발송」.</summary>
    public const string Test = "TEST";

    /// <summary>
    /// <b>구분이 없는 줄</b>을 찾는 조회 조건. 저장되는 값이 아니다.
    /// </summary>
    /// <remarks>
    /// 구분을 붙이기 전에 쌓인 기록과, 구분 없이 보내는 바깥 호출이 있다.
    /// 그 줄들은 <c>category</c> 가 비어 있어서 코드값으로는 못 찾는데,
    /// 「구분 없는 것만 모아 보기」가 곧 <b>어디서 구분을 빠뜨렸는지 찾는
    /// 자리</b>라 조회 쪽에만 이 이름표를 둔다. 공통코드에 같은 값을 만들면
    /// 안 된다 — 앞뒤로 밑줄을 둘씩 두는 것이 그 표시다.
    /// </remarks>
    public const string Unset = "__unset__";

    /// <summary>
    /// 보관할 꼴로 다듬는다. 비었으면 <c>null</c> — <b>빈 글자로 저장하지
    /// 않는다</b>(그러면 「없음」이 두 가지 모양이 되어 조회가 갈린다).
    /// </summary>
    public static string? Normalize(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;

        var trimmed = category.Trim();

        // 조회용 이름표는 저장되는 값이 아니다. 실수로 실려 와도 받아 두지 않는다.
        return string.Equals(trimmed, Unset, StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed.ToUpperInvariant();
    }
}
