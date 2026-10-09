namespace JSini.Web.Admin.Api;

// ============================================================
// 보고서 메일 배치 — 서버(`AuthServer/DTOs/ReportMailDto.cs`) 와 짝이다.
//
// **이 모듈에만 둔다.** 「두 모듈이 쓰면 복제, 세 번째부터 승격」(web/CLAUDE.md)
// 인데 이 자료를 읽는 화면은 포털관리의 한 장뿐이다.
//
// **시각은 UTC 로 받는다**(`LastSentAt` · `NextRunAt`). 한국 시각으로 옮기는
// 일은 화면이 보여 주기 직전에 `Kst(...)` 로 한 번만 한다.
// 다만 사람이 고른 보낼 시각(`SendHourKst` · `SendMinuteKst`)만은 처음부터
// **한국 벽시계**다 — 옮기지 않는다.
// ============================================================

/// <summary>고를 수 있는 보고서 하나. 서버가 메뉴에서 읽어 준다.</summary>
public sealed class ReportCatalogItemDto
{
    /// <summary>화면이 선언한 열쇠. 배치가 가리키는 값이다</summary>
    public string RouteKey { get; set; } = string.Empty;

    public string MenuId { get; set; } = string.Empty;

    /// <summary>메뉴 제목 (「주간 리포트」)</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>메뉴 경로. 메일의 링크가 이 값으로 만들어진다</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>메뉴 아이콘 이름</summary>
    public string? Icon { get; set; }
}

/// <summary>배치 한 줄.</summary>
public sealed class ReportMailScheduleDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary><c>DAILY</c> · <c>WEEKDAY</c> · <c>WEEKLY</c> · <c>MONTHLY</c></summary>
    public string Frequency { get; set; } = string.Empty;

    /// <summary>주간일 때의 요일. 1(월) ~ 7(일)</summary>
    public int? DayOfWeek { get; set; }

    /// <summary>월간일 때의 날. 1 ~ 31</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>보낼 시각(시). <b>한국 벽시계</b></summary>
    public int SendHourKst { get; set; }

    /// <summary>보낼 시각(분). <b>한국 벽시계</b></summary>
    public int SendMinuteKst { get; set; }

    public bool IsActive { get; set; }

    public string? Remark { get; set; }

    /// <summary>마지막으로 보낸 때. <b>UTC 다</b></summary>
    public DateTime? LastSentAt { get; set; }

    /// <summary>마지막 발송의 결과 한 줄</summary>
    public string? LastResult { get; set; }

    /// <summary>다음에 보낼 때. <b>UTC 다.</b> 서버가 주기에서 셈해 준다</summary>
    public DateTime? NextRunAt { get; set; }

    public List<string> ReportKeys { get; set; } = [];

    public List<string> RoleIds { get; set; } = [];

    public List<string> RoleNames { get; set; } = [];

    /// <summary>고른 보고서의 제목들. 지금 메뉴에 있는 것만</summary>
    public List<string> ReportTitles { get; set; } = [];

    /// <summary>
    /// 고를 때는 있었는데 지금은 메뉴에 없는 보고서의 열쇠.
    /// <b>화면이 짚어 준다</b> — 조용히 빼면 세 건인 줄 알고 두 건을 받는다.
    /// </summary>
    public List<string> MissingReportKeys { get; set; } = [];
}

/// <summary>배치를 만들거나 고칠 때 보내는 것.</summary>
public sealed class SaveReportMailScheduleDto
{
    public string? Name { get; set; }

    public string? Frequency { get; set; }

    public int? DayOfWeek { get; set; }

    public int? DayOfMonth { get; set; }

    public int SendHourKst { get; set; } = 8;

    public int SendMinuteKst { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Remark { get; set; }

    public List<string> ReportKeys { get; set; } = [];

    public List<string> RoleIds { get; set; } = [];
}

/// <summary>이 배치의 메일을 받게 되는 사람 한 명.</summary>
public sealed class ReportMailRecipientDto
{
    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    /// <summary>그 사람이 이 배치에 들어온 역할의 이름들</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>대표 이메일. <b>없으면 비어 있고, 그때는 메일이 안 간다</b></summary>
    public string? Email { get; set; }

    /// <summary>표에 그대로 적을 역할 이름 한 줄</summary>
    public string RoleText => string.Join(" · ", RoleNames);

    /// <summary>주소가 없는 사람을 표에서 가린다</summary>
    public string EmailText => string.IsNullOrWhiteSpace(Email) ? "(이메일 없음)" : Email!;
}

/// <summary>수신자 미리보기 한 벌.</summary>
public sealed class ReportMailRecipientsDto
{
    public List<ReportMailRecipientDto> Recipients { get; set; } = [];

    /// <summary>그중 실제로 메일이 갈 사람 수</summary>
    public int DeliverableCount { get; set; }
}

/// <summary>
/// 「미리받아보기」로 보내 달라고 할 때 보내는 것.
/// </summary>
/// <remarks>
/// <b>저장한 배치가 아니어도 된다</b> — 지금 화면에서 고르고 있는 그대로를
/// 담아 보낸다. 받을 역할 칸이 없는 까닭은 이 메일이 <b>누른 사람 본인에게만</b>
/// 가기 때문이다.
/// </remarks>
public sealed class ReportMailPreviewDto
{
    public string? Name { get; set; }

    public string? Frequency { get; set; }

    public int? DayOfWeek { get; set; }

    public int? DayOfMonth { get; set; }

    /// <summary>보낼 시각(시). <b>한국 벽시계</b></summary>
    public int SendHourKst { get; set; } = 8;

    /// <summary>보낼 시각(분). <b>한국 벽시계</b></summary>
    public int SendMinuteKst { get; set; }

    public string? Remark { get; set; }

    /// <summary>지금 고른 보고서들의 열쇠</summary>
    public List<string> ReportKeys { get; set; } = [];
}

/// <summary>미리받아보기의 결과. <b>어디로 갔는지</b>를 함께 받는다.</summary>
public sealed class ReportMailPreviewResultDto
{
    /// <summary>보낸 주소 (본인의 대표 이메일)</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>실제로 담긴 보고서 수</summary>
    public int ReportCount { get; set; }

    /// <summary>메뉴에서 사라져 뺀 보고서의 열쇠</summary>
    public List<string> MissingReportKeys { get; set; } = [];

    /// <summary>화면에 그대로 띄울 한 줄</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 주기의 값과 그 이름. <b>화면과 서버가 같은 글자를 쓴다</b> —
/// 고르개에 적힌 값이 그대로 서버의 <c>frequency</c> 칸으로 간다.
/// </summary>
public static class ReportMailFrequency
{
    public const string Daily = "DAILY";
    public const string Weekday = "WEEKDAY";
    public const string Weekly = "WEEKLY";
    public const string Monthly = "MONTHLY";

    /// <summary>고르개에 쓰는 목록. 익명 형식을 쓰지 않는 까닭은 web/CLAUDE.md 에 있다.</summary>
    public static readonly IReadOnlyList<ReportMailOption> Options =
    [
        new(Daily, "날마다"),
        new(Weekday, "주중(월~금)"),
        new(Weekly, "주마다"),
        new(Monthly, "달마다"),
    ];

    /// <summary>값 → 이름. 못 찾으면 값을 그대로 돌려준다.</summary>
    public static string NameOf(string? value) =>
        Options.FirstOrDefault(o => o.Value == value)?.Text ?? value ?? string.Empty;

    /// <summary>주기 한 줄. 서버의 <c>ReportMailSchedulePlan.Describe</c> 와 같은 말이다.</summary>
    public static string Describe(ReportMailScheduleDto s)
    {
        var time = $"{s.SendHourKst:00}:{s.SendMinuteKst:00}";

        return s.Frequency switch
        {
            Daily => $"날마다 {time}",
            Weekday => $"주중(월~금) {time}",
            Weekly => $"주마다 {DayName(s.DayOfWeek)}요일 {time}",
            Monthly => $"달마다 {s.DayOfMonth ?? 1}일 {time}",
            _ => time,
        };
    }

    /// <summary>1(월) ~ 7(일) 을 한 글자로.</summary>
    public static string DayName(int? isoDayOfWeek) => isoDayOfWeek switch
    {
        1 => "월",
        2 => "화",
        3 => "수",
        4 => "목",
        5 => "금",
        6 => "토",
        7 => "일",
        _ => "월",
    };

    /// <summary>요일 고르개의 목록.</summary>
    public static readonly IReadOnlyList<ReportMailOption> DayOfWeekOptions =
    [
        .. Enumerable.Range(1, 7).Select(i => new ReportMailOption(i.ToString(), $"{DayName(i)}요일")),
    ];
}

/// <summary>고르개 한 줄. 값으로 이름을 되찾을 수 있어야 접힌 조회줄이 코드를 안 적는다.</summary>
/// <param name="Value">저장되는 값</param>
/// <param name="Text">사람이 읽는 글자</param>
public sealed record ReportMailOption(string Value, string Text);
