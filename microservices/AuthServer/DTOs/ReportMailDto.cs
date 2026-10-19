namespace AuthServer.DTOs;

// 보고서 메일 배치의 DTO. 시각은 UTC 로 주고받되, 사람이 고른 보낼 시각(SendHourKst·SendMinuteKst)만 한국 벽시계다.

/// <summary>고를 수 있는 보고서 하나. 목록은 메뉴에서 온다 — 이름을 코드에 적으면 한쪽이 뒤처진다.</summary>
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

    /// <summary>다음에 보낼 때(UTC). 서버가 주기에서 셈한 값이라 저장하지 않는다 — 화면이 다시 셈하면 실제와 어긋난다.</summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>고른 보고서들의 열쇠</summary>
    public List<string> ReportKeys { get; set; } = [];

    /// <summary>받을 역할 식별자들</summary>
    public List<string> RoleIds { get; set; } = [];

    /// <summary>받을 역할의 이름들. 표에 그대로 적는다</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>고른 보고서의 제목들. 지금 메뉴에 있는 것만 담기고, 없어진 보고서는 MissingReportKeys 로 간다.</summary>
    public List<string> ReportTitles { get; set; } = [];

    /// <summary>고를 때는 있었는데 지금은 메뉴에 없는 보고서의 열쇠. 조용히 빼지 않는다 — 빼면 화면 어디에도 그 사실이 남지 않는다.</summary>
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

/// <summary>이 배치의 메일을 실제로 받게 되는 사람 한 명. 보내기 전에 보여 주려고 있다.</summary>
/// 역할은 골랐는데 아무에게도 안 가는 원인이 둘(역할에 사람 없음, 이메일 없음)이라 갈라서 말한다.
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

/// <summary>미리받아보기로 보내 달라는 것. 저장한 배치가 아니어도 되며 누른 사람 본인에게만 간다.</summary>
/// 역할 칸이 필요 없고, 주기 칸은 메일 머리줄에 들어가므로 함께 받는다.
public sealed class ReportMailPreviewDto
{
    /// <summary>배치 이름. 비어 있으면 「미리받아보기」로 적는다</summary>
    public string? Name { get; set; }

    public string? Frequency { get; set; }

    public int? DayOfWeek { get; set; }

    public int? DayOfMonth { get; set; }

    public int SendHourKst { get; set; } = 8;

    public int SendMinuteKst { get; set; }

    /// <summary>메일 머리말</summary>
    public string? Remark { get; set; }

    /// <summary>지금 고른 보고서들의 열쇠. <b>이것만은 비면 안 된다</b></summary>
    public List<string> ReportKeys { get; set; } = [];
}

/// <summary>미리받아보기 결과. 보낸 주소를 돌려준다 — 안 왔다고 할 때 가장 먼저 볼 것이 어디로 갔나이기 때문이다.</summary>
public sealed class ReportMailPreviewResultDto
{
    /// <summary>보낸 주소 (로그인한 본인의 대표 이메일)</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>실제로 담긴 보고서 수</summary>
    public int ReportCount { get; set; }

    /// <summary>메뉴에서 사라져 뺀 보고서의 열쇠</summary>
    public List<string> MissingReportKeys { get; set; } = [];

    /// <summary>화면에 그대로 띄울 한 줄</summary>
    public string Message { get; set; } = string.Empty;
}
