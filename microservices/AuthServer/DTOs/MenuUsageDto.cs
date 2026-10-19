namespace AuthServer.DTOs;

// 메뉴 사용기록 DTO. 시각은 전부 UTC 로 주고받고, 한국 시각은 화면이 보여 주기 직전에 한 번만 만든다(docs/utc-time.md).
// 집계는 DB 가 한다 — 줄을 화면에서 더하면 한 달이면 수만 줄을 회로로 옮기게 된다.

/// <summary>
/// 포털 셸이 「이 화면을 열었다」고 알릴 때 보내는 것.
/// </summary>
/// 사람은 담지 않는다 — 게이트웨이가 붙이는 X-User-Id 로 서버가 정한다. 몸체에서 받으면 남의 이름으로 기록을 쌓을 수 있다.
public sealed class MenuUsageRecordDto
{
    /// <summary>메뉴 경로(<c>scom.system_menus.path</c>). 이것이 없으면 적지 않는다.</summary>
    public string? MenuPath { get; set; }

    /// <summary>화면이 선언한 열쇠.</summary>
    public string? RouteKey { get; set; }

    /// <summary>그때 사이드바에 적혀 있던 제목.</summary>
    public string? MenuTitle { get; set; }

    /// <summary>실제로 열린 주소.</summary>
    public string? Href { get; set; }

    /// <summary>직전에 보던 화면의 메뉴 경로. 첫 화면이면 비어 있다.</summary>
    public string? FromPath { get; set; }
}

/// <summary>열람 한 건. 기록 표의 한 줄이다.</summary>
public sealed class MenuUsageDto
{
    public long Id { get; set; }

    /// <summary>본 때. <b>UTC 다.</b></summary>
    public DateTime OccurredAt { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>이름. <b>계정을 지운 사람은 비어 있다</b> — 그래도 줄은 남는다.</summary>
    public string? UserName { get; set; }

    public string? DepartmentName { get; set; }

    public string MenuPath { get; set; } = string.Empty;

    public string? RouteKey { get; set; }

    /// <summary>그때의 제목. 메뉴가 지워졌어도 남아 있다.</summary>
    public string? MenuTitle { get; set; }

    public string? Href { get; set; }

    /// <summary>직전 화면의 메뉴 경로.</summary>
    public string? FromPath { get; set; }
}

/// <summary>한 사람의 기간 열람량. 사람별 표의 한 줄이다.</summary>
public sealed class MenuUsageByUserDto
{
    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? DepartmentName { get; set; }

    /// <summary>열람 건수.</summary>
    public int Views { get; set; }

    /// <summary>그중 서로 다른 화면의 수. <b>건수와 함께 봐야 뜻이 생긴다</b>.</summary>
    public int Screens { get; set; }

    /// <summary>기록이 있는 날의 수.</summary>
    public int Days { get; set; }

    /// <summary>가장 많이 본 화면의 제목.</summary>
    public string? TopMenuTitle { get; set; }

    /// <summary>그 화면을 본 횟수.</summary>
    public int TopMenuViews { get; set; }

    /// <summary>처음 본 때(UTC).</summary>
    public DateTime FirstViewAt { get; set; }

    /// <summary>마지막으로 본 때(UTC).</summary>
    public DateTime LastViewAt { get; set; }
}

/// <summary>한 화면의 기간 열람량. 화면별 표의 한 줄이다.</summary>
public sealed class MenuUsageByMenuDto
{
    public string MenuPath { get; set; } = string.Empty;

    public string? RouteKey { get; set; }

    public string? MenuTitle { get; set; }

    public int Views { get; set; }

    /// <summary>그 화면을 본 사람의 수.</summary>
    public int Users { get; set; }

    public DateTime LastViewAt { get; set; }
}

/// <summary>타임라인의 한 칸 — 열람 한 건.</summary>
public sealed class MenuUsageTimelineItemDto
{
    /// <summary>그날의 몇 번째인가. 1 부터다.</summary>
    public int Seq { get; set; }

    /// <summary>본 때(UTC).</summary>
    public DateTime OccurredAt { get; set; }

    public string MenuPath { get; set; } = string.Empty;

    public string? RouteKey { get; set; }

    public string? MenuTitle { get; set; }

    public string? Href { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    /// <summary>
    /// 앞 칸에서 몇 분 지나 열었나. 첫 칸은 0 이다.
    /// </summary>
    /// 떠난 순간은 아무도 재지 않는다 — 「다음 화면까지의 사이」로만 읽어야 한다.
    public int GapMinutes { get; set; }
}

/// <summary>하루치 타임라인.</summary>
public sealed class MenuUsageTimelineDayDto
{
    /// <summary>어느 날인가(<c>yyyy-MM-dd</c>). <b>한국 달력 날짜</b>다.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>그날 열람 건수.</summary>
    public int Views { get; set; }

    /// <summary>그날 본 서로 다른 화면의 수.</summary>
    public int Screens { get; set; }

    /// <summary>그날 처음 본 때(UTC).</summary>
    public DateTime FirstAt { get; set; }

    /// <summary>그날 마지막으로 본 때(UTC).</summary>
    public DateTime LastAt { get; set; }

    /// <summary>시간순으로 늘어놓은 칸들.</summary>
    public List<MenuUsageTimelineItemDto> Items { get; set; } = [];
}

/// <summary>
/// 기록이 있는 날 하나. 날짜 고르개가 <b>빈 날을 피하게</b> 해 준다.
/// </summary>
public sealed class MenuUsageDayDto
{
    /// <summary>한국 달력 날짜(<c>yyyy-MM-dd</c>).</summary>
    public string Date { get; set; } = string.Empty;

    public int Views { get; set; }
}
