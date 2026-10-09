namespace AuthServer.DTOs;

// ============================================================
// 보고서 메일 배치 — 프론트의 `JSini.Web.Admin/Api/ReportMailModels.cs` 와 짝이다.
//
// **시각은 UTC 로 주고받는다**(`LastSentAt` · `NextRunAt`). 다만 사람이 고른
// 보낼 시각(`SendHourKst` · `SendMinuteKst`)만은 **한국 벽시계**다 — 그 까닭은
// `ReportMailSchedule` 엔티티 머리말에 있다.
// ============================================================

/// <summary>
/// 고를 수 있는 보고서 하나. 목록은 <b>메뉴에서 온다</b>.
/// </summary>
/// <remarks>
/// 보고서 이름을 코드에 적어 두지 않는다. 운영 리포트 메뉴가 하나 늘면
/// 이 화면의 목록도 그날부터 함께 는다 — 적어 두면 반드시 한쪽이 뒤처진다.
/// </remarks>
public sealed class ReportCatalogItemDto
{
    /// <summary>화면이 선언한 열쇠. 배치가 가리키는 값이다</summary>
    public string RouteKey { get; set; } = string.Empty;

    /// <summary>메뉴 식별자. 권한을 보러 갈 때 쓴다</summary>
    public string MenuId { get; set; } = string.Empty;

    /// <summary>메뉴 제목 (「주간 리포트」)</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>메뉴 경로. 메일의 링크가 이 값으로 만들어진다</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>메뉴 아이콘 이름. 화면이 같은 글리프를 쓴다</summary>
    public string? Icon { get; set; }
}

/// <summary>배치 한 줄 (목록용 + 상세용). 고른 것들을 함께 담는다.</summary>
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

    /// <summary>
    /// 다음에 보낼 때. <b>UTC 다.</b> 서버가 주기에서 셈한 값이고 저장하지 않는다.
    /// </summary>
    /// <remarks>
    /// 화면이 다시 셈하지 않게 하려고 함께 보낸다 — 같은 규칙을 두 곳에서
    /// 계산하면 「다음 발송」과 실제가 어긋나고, 그 어긋남은 아무도 못 믿는
    /// 숫자를 하나 만든다.
    /// </remarks>
    public DateTime? NextRunAt { get; set; }

    /// <summary>고른 보고서들의 열쇠</summary>
    public List<string> ReportKeys { get; set; } = [];

    /// <summary>받을 역할 식별자들</summary>
    public List<string> RoleIds { get; set; } = [];

    /// <summary>받을 역할의 이름들. 표에 그대로 적는다</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>
    /// 고른 보고서의 제목들. <b>지금 메뉴에 있는 것만</b> 담긴다 —
    /// 없어진 보고서는 <see cref="MissingReportKeys"/> 로 간다.
    /// </summary>
    public List<string> ReportTitles { get; set; } = [];

    /// <summary>
    /// 고를 때는 있었는데 지금은 메뉴에 없는 보고서의 열쇠.
    /// </summary>
    /// <remarks>
    /// <b>조용히 빼지 않는다.</b> 빼 버리면 배치가 세 건을 보내는 줄 알고
    /// 두 건만 나가는데, 화면 어디에도 그 사실이 없다.
    /// </remarks>
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

    /// <summary>고른 보고서들의 열쇠. 통째로 받아 덮어쓴다</summary>
    public List<string> ReportKeys { get; set; } = [];

    /// <summary>받을 역할 식별자들. 통째로 받아 덮어쓴다</summary>
    public List<string> RoleIds { get; set; } = [];
}

/// <summary>
/// 이 배치의 메일을 실제로 받게 되는 사람 한 명.
/// </summary>
/// <remarks>
/// <b>보내기 전에 보여 주려고 있다.</b> 「역할을 골랐는데 아무에게도 안 간다」는
/// 일이 조용히 일어나는 자리가 둘이다 — 그 역할에 걸린 사람이 없거나, 걸려
/// 있어도 계정에 이메일이 없거나. 둘은 고치는 자리가 다르므로 갈라서 말한다.
/// </remarks>
public sealed class ReportMailRecipientDto
{
    /// <summary>포털 로그인 아이디</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>이름</summary>
    public string? UserName { get; set; }

    /// <summary>그 사람이 이 배치에 들어온 역할의 이름들</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>대표 이메일. <b>없으면 비어 있고, 그때는 메일이 안 간다</b></summary>
    public string? Email { get; set; }
}

/// <summary>수신자 미리보기 한 벌.</summary>
public sealed class ReportMailRecipientsDto
{
    /// <summary>받을 사람들. 이메일이 없는 사람도 담는다</summary>
    public List<ReportMailRecipientDto> Recipients { get; set; } = [];

    /// <summary>그중 실제로 메일이 갈 사람 수 (이메일이 있는 사람)</summary>
    public int DeliverableCount { get; set; }
}
