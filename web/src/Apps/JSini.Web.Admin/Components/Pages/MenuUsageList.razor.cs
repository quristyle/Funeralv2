using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Models;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class MenuUsageList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    // ── 보기 넷 ─────────────────────────────────────────────
    //
    // **값은 화면 안에서만 쓴다.** 서버로 나가지 않으므로 글자를 바꿔도
    // 조회가 깨지지 않는다 — 기간·사용자·검색어와 다른 종류의 조건이다.

    private const string ViewByUser = "user";
    private const string ViewByMenu = "menu";
    private const string ViewLog = "log";
    private const string ViewTimeline = "timeline";

    private static readonly SchOption[] ViewOptions =
    [
        new(ViewByUser, "사용자별"),
        new(ViewByMenu, "화면별"),
        new(ViewLog, "기록"),
        new(ViewTimeline, "타임라인"),
    ];

    /// <summary>
    /// 기록 목록이 한 번에 받는 최대 줄 수. <b>서버의 값과 같아야 한다</b>
    /// (<c>MenuUsageEndpoints.MaxTake</c>) — 작으면 「잘렸다」 안내가 안 뜨고,
    /// 크면 안 잘린 목록에 그 안내가 뜬다.
    /// </summary>
    private const int LogCap = 500;

    /// <inheritdoc cref="LogCap"/>
    private const int TimelineCap = 1000;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        SchSummary.NameOf(ViewOptions, o => o.Value, o => o.Text, _view),
        SchSummary.NameOf(_users, u => u.UserId, u => u.Who, _userId),
        SchSummary.Or(_keyword));

    private IReadOnlyList<MenuUsageByUserDto> _users = [];
    private IReadOnlyList<MenuUsageByMenuDto> _menus = [];
    private IReadOnlyList<MenuUsageDto> _logs = [];
    private IReadOnlyList<MenuUsageTimelineDayDto> _days = [];

    private DateTime? _from = AppTime.TodayDate.AddDays(-7);
    private DateTime? _to = AppTime.TodayDate;
    private string _view = ViewByUser;
    private string? _userId;
    private string? _keyword;

    /// <summary>사람별 표에서 고른 줄. 오른쪽 클릭 창의 「타임라인」이 쓴다.</summary>
    private MenuUsageByUserDto? _pickedRow;

    /// <summary>
    /// 고르개에 세울 사람. <b>아이디를 들고 목록에서 되찾는다</b> — 줄 자체를
    /// 들고 있으면 다시 조회했을 때 새로 만들어진 줄과 같지 않아 고르개가 빈다.
    /// </summary>
    private MenuUsageByUserDto? PickedUser =>
        _userId is null ? null : _users.FirstOrDefault(u => u.UserId == _userId);

    // ── 타일 넷 ─────────────────────────────────────────────
    //
    // **화면별 집계에서 읽는다.** 그쪽이 고른 사람까지 반영한 값이라,
    // 보기를 무엇으로 두든 지금 보고 있는 것과 숫자가 맞는다.

    private int TotalViews => _menus.Sum(m => m.Views);

    /// <summary>
    /// 사람 수. 한 사람을 고르면 1 이다 — 사람별 집계는 고른 사람과 무관하게
    /// (고르개를 채워야 해서) 전체를 들고 있으므로 그 수를 그대로 쓰면 안 된다.
    /// </summary>
    private int PeopleCount => _userId is null ? _users.Count : 1;

    private int ScreenCount => _menus.Count;

    private string LastViewText => _menus.Count == 0
        ? "-"
        : _menus.Max(m => m.LastViewAt).Kst("MM-dd HH:mm");

    /// <summary>타일 아래에 적을 기간. 사람이 고른 날짜 그대로다.</summary>
    private string PeriodText => SchSummary.Period(_from, _to) ?? "전체 기간";

    /// <summary>타임라인이 받아 온 칸의 총수. 잘렸는지 따지는 데 쓴다.</summary>
    private int TimelineViews => _days.Sum(d => d.Views);

    /// <summary>
    /// 서버가 잘랐으면 그 한도, 아니면 <c>null</c>. 집계 보기(사람별·화면별)는
    /// 자르지 않으므로 언제나 <c>null</c> 이다.
    /// </summary>
    private int? Truncated => _view switch
    {
        ViewLog when _logs.Count >= LogCap => LogCap,
        ViewTimeline when TimelineViews >= TimelineCap => TimelineCap,
        _ => null,
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>
    /// 다시 읽는다.
    /// </summary>
    /// <remarks>
    /// <b>집계 둘은 보기와 무관하게 늘 읽는다</b> — 타일 넷과 사용자 고르개가
    /// 그것으로 서기 때문이다. 줄이 많은 쪽(기록 · 타임라인)만 보고 있을 때
    /// 읽는다. 셋을 다 읽으면 타임라인 한 번에 수천 줄이 같이 넘어온다.
    /// </remarks>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var from = DateOf(_from);
        var to = DateOf(_to);

        _users = await Api.GetMenuUsageByUserAsync(from, to, _keyword);
        _menus = await Api.GetMenuUsageByMenuAsync(from, to, _userId, _keyword);

        _logs = _view == ViewLog
            ? await Api.GetMenuUsageLogsAsync(from, to, _userId, _keyword, LogCap)
            : [];

        _days = _view == ViewTimeline
            ? await Api.GetMenuUsageTimelineAsync(from, to, _userId, _keyword, TimelineCap)
            : [];

        // 고른 사람이 이 기간에는 기록이 없을 수 있다. 그때 고르개만 값을
        // 들고 있으면 「전체」로 보이면서 조건은 걸려 있는 상태가 된다.
        if (_userId is not null && _users.All(u => u.UserId != _userId))
        {
            _userId = null;
        }

        return _view switch
        {
            ViewByUser => _users.Count,
            ViewByMenu => _menus.Count,
            ViewLog => _logs.Count,
            _ => _days.Count,
        };
    }, "이 기간에 쌓인 기록이 없습니다.", "메뉴 사용기록을 읽지 못했습니다");

    private Task Reset()
    {
        _from = AppTime.TodayDate.AddDays(-7);
        _to = AppTime.TodayDate;
        _view = ViewByUser;
        _userId = null;
        _keyword = null;
        _pickedRow = null;
        return ReloadAsync();
    }

    /// <summary>
    /// 보기를 바꾼다. <b>바로 다시 읽는다</b> — 보기마다 읽어 오는 것이 달라서
    /// (기록·타임라인은 그때만 받는다) 조회를 다시 누르게 하면 빈 판이 뜬다.
    /// </summary>
    private Task PickViewAsync(string? view)
    {
        _view = string.IsNullOrWhiteSpace(view) ? ViewByUser : view;
        return ReloadAsync();
    }

    private Task PickUserAsync(MenuUsageByUserDto? user)
    {
        _userId = user?.UserId;
        return ReloadAsync();
    }

    /// <summary>
    /// 고른 줄을 들고 있는다. <b>값과 알림을 같이 준다</b> — 알림만 받으면
    /// 화면이 보관하지 않는다는 뜻이 되어 강조가 곧 풀린다(CommGrd 머리말).
    /// </summary>
    private void OnUserPicked(MenuUsageByUserDto? row) => _pickedRow = row;

    /// <summary>
    /// 그 사람으로 좁혀 타임라인을 연다.
    /// </summary>
    /// <remarks>
    /// 창을 띄우지 않고 <b>보고 있는 판을 갈아 끼운다</b>. 타임라인은 하루가
    /// 길면 줄이 수백이라 창에 넣으면 거기서 또 스크롤하게 된다 — 판이
    /// 넓을수록 읽기 쉬운 종류의 자료다. 조건줄에 사람과 보기가 그대로
    /// 적히므로 어디를 보고 있는지도 그 줄이 말해 준다.
    /// </remarks>
    private Task OpenUserTimelineAsync(MenuUsageByUserDto row)
    {
        _pickedRow = row;
        _userId = row.UserId;
        _view = ViewTimeline;
        return ReloadAsync();
    }

    /// <summary>
    /// 고르개가 주는 <c>DateTime?</c> 을 날짜로 바꾼다.
    /// </summary>
    /// <remarks>
    /// <b>한국 달력 날짜다.</b> 사람이 고른 것이 그것이고, 서버는 이 날짜를
    /// 받아 하루의 경계를 잡는다 — 시각으로 보내면 자정 언저리가 어긋난다
    /// (docs/utc-time.md 의 「달력 날짜만 한국 달력으로 센다」).
    /// </remarks>
    private static DateOnly? DateOf(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);

    /// <summary>
    /// 앞 칸에서 여기까지의 사이. <b>머문 시간이 아니다</b>(화면 머리말).
    /// </summary>
    /// <remarks>
    /// 한 시간이 넘으면 분으로 적지 않는다 — 「+437분」은 사람이 읽고 나서
    /// 한 번 더 나눠야 뜻이 생긴다.
    /// </remarks>
    private static string GapText(int minutes) => minutes switch
    {
        < 60 => $"{minutes}분",
        < 60 * 24 => $"{minutes / 60}시간 {minutes % 60}분",
        _ => $"{minutes / (60 * 24)}일",
    };

    /// <summary>
    /// 날짜 머리띠에 적을 요일. <b>날짜만 적으면 주말을 못 가른다</b> —
    /// 「이날은 왜 이렇게 조용한가」의 답이 대개 그것이다.
    /// </summary>
    private static string WeekdayOf(string date) =>
        DateOnly.TryParse(date, out var day)
            ? day.DayOfWeek switch
            {
                DayOfWeek.Monday => "월",
                DayOfWeek.Tuesday => "화",
                DayOfWeek.Wednesday => "수",
                DayOfWeek.Thursday => "목",
                DayOfWeek.Friday => "금",
                DayOfWeek.Saturday => "토",
                _ => "일",
            }
            : "?";
}
