namespace JSini.Web.Models;

// ============================================================
// 메뉴 사용기록 — **누가 어떤 화면을 언제 보았나.**
//
// ── 여기 있는 이유 — 쌓는 쪽과 보는 쪽이 다른 프로젝트다 ─────
//
// 쌓는 것은 공용(`JSini.Web.Components` 의 `MenuUsageRecorder`)이다. 화면을
// 옮길 때마다 레이아웃이 한 줄씩 밀어 넣으므로 그 자리가 셸이어야 하고,
// 꺼내 보는 것은 포털관리 모듈의 「메뉴 사용기록」 화면이다. 모듈에 두면
// 공용이 못 쓰고(의존 규칙 4), 공용에 두면 그 타입이 화면 DTO 로 쓰이게 되어
// 자리가 어긋난다 — `PortalError.cs` 가 같은 까닭으로 여기 있다.
//
// 칸 이름은 AuthServer 의 DTO 와 맞춘다 — 봉투를 그대로 주고받는다.
//
// ── 시각은 UTC 로 온다 ──────────────────────────────────────
//
// 한국 시각은 **보여 주기 직전에 한 번만** 만든다(docs/utc-time.md).
// 날짜 칸(`Date`)만 한국 달력으로 끊은 값이고, 그 일은 서버가 한다.
// ============================================================

/// <summary>
/// 「이 화면을 열었다」고 알릴 때 보내는 것.
/// </summary>
/// <remarks>
/// <b>사람은 담지 않는다.</b> 게이트웨이가 붙여 주는 <c>X-User-Id</c> 로
/// 서버가 정한다 — 몸체에서 받으면 남의 이름으로 기록을 쌓을 수 있다.
/// </remarks>
public sealed class MenuUsageRecordDto
{
    /// <summary>메뉴 경로(<c>MenuNode.Path</c>). 이것이 없으면 서버가 버린다.</summary>
    public string? MenuPath { get; set; }

    /// <summary>화면이 선언한 열쇠(<c>MenuNode.RouteKey</c>).</summary>
    public string? RouteKey { get; set; }

    /// <summary>그때 사이드바에 적혀 있던 제목.</summary>
    public string? MenuTitle { get; set; }

    /// <summary>실제로 열린 주소. 메뉴 경로와 다를 수 있다(<c>RouteAliases</c>).</summary>
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

    /// <summary>표에 그릴 이름. 이름을 모르면 아이디만 적는다.</summary>
    public string Who => string.IsNullOrWhiteSpace(UserName) ? UserId : $"{UserName} ({UserId})";

    /// <summary>표에 그릴 화면 이름. 제목이 없으면 경로를 그대로 보여 준다.</summary>
    public string Screen => string.IsNullOrWhiteSpace(MenuTitle) ? MenuPath : MenuTitle!;
}

/// <summary>한 사람의 기간 열람량. 사람별 표의 한 줄이다.</summary>
public sealed class MenuUsageByUserDto
{
    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? DepartmentName { get; set; }

    public int Views { get; set; }

    /// <summary>
    /// 그중 서로 다른 화면의 수.
    /// </summary>
    /// <remarks>
    /// <b>건수와 함께 봐야 뜻이 생긴다.</b> 「한 화면을 백 번 새로고친 사람」과
    /// 「백 개를 돌아본 사람」이 건수만으로는 같아 보인다.
    /// </remarks>
    public int Screens { get; set; }

    /// <summary>기록이 있는 날의 수. <b>한국 달력으로 센다.</b></summary>
    public int Days { get; set; }

    /// <summary>가장 많이 본 화면의 제목.</summary>
    public string? TopMenuTitle { get; set; }

    /// <summary>그 화면을 본 횟수.</summary>
    public int TopMenuViews { get; set; }

    /// <summary>처음 본 때(UTC).</summary>
    public DateTime FirstViewAt { get; set; }

    /// <summary>마지막으로 본 때(UTC).</summary>
    public DateTime LastViewAt { get; set; }

    /// <summary>표에 그릴 이름.</summary>
    public string Who => string.IsNullOrWhiteSpace(UserName) ? UserId : $"{UserName} ({UserId})";
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

    /// <summary>표에 그릴 화면 이름.</summary>
    public string Screen => string.IsNullOrWhiteSpace(MenuTitle) ? MenuPath : MenuTitle!;
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

    /// <summary>앞 칸에서 몇 분 지나 열었나. 첫 칸은 0 이다.</summary>
    /// <remarks>
    /// <b>그 화면에 머문 시간이 아니다.</b> 떠난 순간은 아무도 재지 않는다 —
    /// 그날 마지막으로 연 화면은 포털을 닫을 때까지 떠 있었을 수도 있고
    /// 1초 뒤에 닫혔을 수도 있다. 「다음 화면까지의 사이」라고만 읽어야 한다.
    /// </remarks>
    public int GapMinutes { get; set; }

    /// <summary>타임라인에 그릴 화면 이름.</summary>
    public string Screen => string.IsNullOrWhiteSpace(MenuTitle) ? MenuPath : MenuTitle!;

    /// <summary>타임라인에 그릴 사람 이름.</summary>
    public string Who => string.IsNullOrWhiteSpace(UserName) ? UserId : UserName!;
}

/// <summary>하루치 타임라인.</summary>
public sealed class MenuUsageTimelineDayDto
{
    /// <summary>어느 날인가(<c>yyyy-MM-dd</c>). <b>한국 달력 날짜</b>다.</summary>
    public string Date { get; set; } = string.Empty;

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
