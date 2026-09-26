using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ProjectScheduler
{
    [Inject] private WbsClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        SchSummary.Or(_kindNames),
        _completeStateName);

    /// <summary>
    /// 고른 값의 <b>이름</b>들. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _kindNames;

    private string? _completeStateName;

    /// <summary>
    /// 새 줄의 기본 구분. 고른 구분이 「전체」일 때 쓴다 — 비워 두면 어느
    /// 화면에서도 안 보이는 줄이 된다(머리말).
    /// </summary>
    private const string DefaultKind = "Public";

    /// <summary>
    /// 이 화면의 보기. <b>구간도 이것이 정한다</b> — 둘을 따로 두면 화면에
    /// 그려지는 달과 읽어 온 달이 어긋날 수 있다(머리말).
    /// </summary>
    private enum CalView { Day, Week, Month, Agenda, Table }

    /// <summary>보기 고르개에 서는 차례. 구글과 같이 좁은 것부터 넓은 것으로 간다.</summary>
    private static readonly (CalView View, string Text, string? Key)[] Views =
    [
        (CalView.Day, "일", "D"),
        (CalView.Week, "주", "W"),
        (CalView.Month, "월", "M"),
        (CalView.Agenda, "일정", "A"),
        (CalView.Table, "표", null),
    ];

    private IReadOnlyList<WbsItemDto> _rows = [];

    private CalView _view = CalView.Month;
    private DateTime _anchor = DateTime.Today;

    private string? _projectCode;
    private string? _completeState;

    /// <summary>체크로 고른 구분. <b>비어 있으면 전체</b>다(머리말).</summary>
    private IReadOnlyList<string> _scheduleTypes = [];

    private DxSchedulerDataStorage? _storage;
    private CalendarItem[] _items = [];

    /// <summary>휴대폰인가. 격자 칸 높이와 한 칸에 담는 막대 수가 갈린다(머리말).</summary>
    private bool _isPhone;

    /// <summary>격자의 칸들. 앞뒤 달의 자투리까지 들어 있다.</summary>
    private IReadOnlyList<CalDay> _days = [];

    /// <summary>주 한 줄씩 자리를 나눠 담은 것. 월 보기가 이것을 그린다.</summary>
    private IReadOnlyList<WeekRow> _weeks = [];

    /// <summary>고른 날. 휴대폰에서 격자 아래 목록이 이 날의 일정을 편다.</summary>
    private DateOnly _selected = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>작은 달력이 보고 있는 달 — 큰 달력에서 <b>몇 달 떨어져 있나</b>.</summary>
    private int _miniOffset;

    /// <summary>끌고 있는 일정. 놓을 때 이것을 옮긴다.</summary>
    private WbsItemDto? _drag;

    /// <summary>끌기가 지금 걸쳐 있는 칸. 그 칸만 도드라지게 그린다.</summary>
    private DateOnly? _dragOver;

    /// <summary>쓸기 판정에 쓰는 손가락 시작점. 화면에 그려지는 값이 아니다.</summary>
    private double _touchX;
    private double _touchY;

    private ConfirmDialog? _confirm;

    private bool _peeking;
    private DateOnly _peekDay = DateOnly.FromDateTime(DateTime.Today);

    private bool _editing;
    private bool _isNew;
    private bool _complete;
    private WbsItemDto _form = new();

    private bool _canCreate;
    private bool _canUpdate;
    private bool _canDelete;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>보기가 정하는 조회 구간의 단위(머리말).</summary>
    private DateRangePreset Preset => _view switch
    {
        CalView.Day => DateRangePreset.Day,
        CalView.Week => DateRangePreset.Week,
        _ => DateRangePreset.Month,
    };

    private DateRange Range => DateRange.Of(Preset, _anchor);

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string Hint => $"{_rows.Count}건";

    /// <summary>주·일 보기에서만 쓴다 — 월은 우리가 그린다(머리말).</summary>
    private SchedulerViewType ViewType =>
        _view == CalView.Day ? SchedulerViewType.Day : SchedulerViewType.Week;

    /// <summary>
    /// 머리줄에 적는 지금 보고 있는 곳. 구글과 같은 말투다 —
    /// 달이면 「2026년 9월」, 주면 「2026년 9월 1일 – 7일」, 날이면 요일까지.
    /// </summary>
    private string BarTitle
    {
        get
        {
            if (_view == CalView.Day)
            {
                return $"{_anchor.Year}년 {_anchor.Month}월 {_anchor.Day}일 ({DowName(_anchor.DayOfWeek)})";
            }

            if (_view != CalView.Week)
            {
                return $"{_anchor.Year}년 {_anchor.Month}월";
            }

            var range = Range;

            return range.Start.Month == range.End.Month
                ? $"{range.Start.Year}년 {range.Start.Month}월 {range.Start.Day}일 – {range.End.Day}일"
                : $"{range.Start.Year}년 {range.Start.Month}월 {range.Start.Day}일 – {range.End.Month}월 {range.End.Day}일";
        }
    }

    /// <summary>
    /// 한 칸에 보이는 막대 수. 넘는 것은 「+N개 더보기」가 된다.
    ///
    /// <para>
    /// 칸 높이와 <b>한 몸</b>이다 — 여기를 늘리면 CSS 의 칸 최소 높이도 같이
    /// 늘려야 마지막 줄이 잘린다. 휴대폰은 한 줄 적게 담는다.
    /// </para>
    /// </summary>
    private int MaxLanes => _isPhone ? 2 : 3;

    /// <summary>
    /// 격자·조회가 함께 쓰는 구간. 달 보기는 <b>앞뒤 달의 자투리 날까지</b>
    /// 넓힌다 — 까닭은 머리말 [보기가 곧 조회 구간이다].
    ///
    /// <para>
    /// 주·일 보기는 손댈 것이 없다. 주는 이미 일요일에서 토요일까지고,
    /// 하루는 넓힐 앞뒤가 없다.
    /// </para>
    /// </summary>
    private DateRange GridSpan
    {
        get
        {
            var range = Range;

            if (Preset != DateRangePreset.Month)
            {
                return range;
            }

            return new DateRange(
                range.Start.AddDays(-(int)range.Start.DayOfWeek),
                range.End.AddDays(6 - (int)range.End.DayOfWeek));
        }
    }

    /// <summary>고른 날의 일정. 격자에서 이미 날짜별로 갈라 둔 것을 집어 온다.</summary>
    private IReadOnlyList<WbsItemDto> SelectedItems =>
        _days.FirstOrDefault(d => d.Date == _selected)?.Items ?? [];

    private IReadOnlyList<WbsItemDto> PeekItems =>
        _days.FirstOrDefault(d => d.Date == _peekDay)?.Items ?? [];

    private string AgendaTitle =>
        $"{_selected.Month}월 {_selected.Day}일 ({DowName(_selected.DayOfWeek)})";

    private string PeekTitle =>
        $"{_peekDay.Month}월 {_peekDay.Day}일 ({DowName(_peekDay.DayOfWeek)})";

    protected override void OnInitialized()
    {
        // `PermissionView` 와 **똑같이** 묻는다. 끌어 옮기기·크기 조절은 그릴지
        // 말지가 아니라 부품의 파라미터로 켜고 끄는 것이라 직접 판정한다.
        var path = CurrentPath;

        _canCreate = Permissions.Can(path, MenuAction.Create);
        _canUpdate = Permissions.Can(path, MenuAction.Update);
        _canDelete = Permissions.Can(path, MenuAction.Delete);
    }

    // **화면을 열 때 여기서 조회하지 않는다.** 프로젝트 고르개가 첫 항목을
    // 스스로 고르면서 조회를 건다(`AutoSelectFirst`). 둘 다 하면 **프로젝트를
    // 안 건 조회와 건 조회가 같이 날아가고**, 늦게 돌아온 쪽이 화면에 남는다 —
    // 프로젝트를 골랐는데 목록은 전체인 상태가 된다. 실제로 그렇게 보였다.

    /// <summary>
    /// 구분 체크가 바뀌었다. <b>바로 조회한다</b> — 프로젝트 고르개와 같다.
    /// 체크를 만지고 「조회」를 또 눌러야 하면, 누르기 전까지 화면의 딱지와
    /// 달력이 서로 다른 이야기를 한다.
    /// </summary>
    private Task OnKindsChangedAsync(IEnumerable<string>? kinds)
    {
        _scheduleTypes = kinds is null ? [] : [.. kinds];
        return SearchAsync();
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        // **격자 구간으로 묻는다** — 달 1일~말일이 아니다(머리말).
        var span = GridSpan;

        _rows = await Api.ListAsync(
            ProjectRid, _completeState, _scheduleTypes,
            DateOnly.FromDateTime(span.Start), DateOnly.FromDateTime(span.End));

        SyncSelected();
        BuildCalendar();
        BuildDays();
        BuildWeeks();

        return _rows.Count;
    }, "그 기간에 일정이 없습니다.", "일정표를 읽지 못했습니다");

    /// <summary>
    /// 화면 크기가 경계를 넘었다. 값만 받는다 — 격자는 같은 것을 그리고
    /// 칸에 담기는 막대 수(<see cref="MaxLanes"/>)만 그리기 시점에 갈린다.
    /// </summary>
    private void OnPhoneChanged(bool active) => _isPhone = active;

    // ── 머리줄 ───────────────────────────────────────────────────

    /// <summary>
    /// 보기를 바꾼다. <b>구간이 따라 바뀌므로 다시 읽는다.</b>
    ///
    /// <para>
    /// 「일」로 갈 때 기준일을 오늘로 당기지 않는다 — 달을 훑다가 어느 날을
    /// 눌러 들어오는 길이 있어서(<see cref="OnDayNumberAsync"/>), 여기서
    /// 되돌리면 그 길이 늘 오늘로 끌려간다.
    /// </para>
    /// </summary>
    private Task SetViewAsync(CalView view)
    {
        if (view == _view)
        {
            return Task.CompletedTask;
        }

        _view = view;
        _miniOffset = 0;

        return SearchAsync();
    }

    private Task GoTodayAsync()
    {
        _anchor = DateTime.Today;
        _selected = Today;
        _miniOffset = 0;

        return SearchAsync();
    }

    /// <summary>앞뒤 구간으로 옮기고 다시 읽는다. 보기의 단위만큼 움직인다.</summary>
    private Task ShiftAsync(int direction)
    {
        _anchor = Preset switch
        {
            DateRangePreset.Day => _anchor.AddDays(direction),
            DateRangePreset.Week => _anchor.AddDays(direction * 7),
            _ => _anchor.AddMonths(direction),
        };

        _miniOffset = 0;

        return SearchAsync();
    }

    /// <summary>
    /// 그 날로 간다. 작은 달력과 날짜 누르기가 함께 쓴다 — <b>보기는 그대로</b>
    /// 두고 기준일만 옮긴다. 구글의 작은 달력도 그렇게 움직인다.
    /// </summary>
    private Task JumpAsync(DateOnly day)
    {
        _anchor = day.ToDateTime(TimeOnly.MinValue);
        _selected = day;
        _miniOffset = 0;

        return SearchAsync();
    }

    /// <summary>
    /// 단축키. 구글과 같은 글쇠다(머리말).
    ///
    /// <para>
    /// <b>표 보기에서는 받지 않는다.</b> 표에는 글자를 치는 칸이 있어서,
    /// 거기서 친 글쇠가 여기로 올라오면 검색어를 적다가 달이 넘어간다.
    /// </para>
    /// </summary>
    private Task OnKeyAsync(KeyboardEventArgs e)
    {
        if (_view == CalView.Table || e.CtrlKey || e.AltKey || e.MetaKey)
        {
            return Task.CompletedTask;
        }

        switch (e.Key)
        {
            case "t":
            case "T":
                return GoTodayAsync();

            case "d":
            case "D":
                return SetViewAsync(CalView.Day);

            case "w":
            case "W":
                return SetViewAsync(CalView.Week);

            case "m":
            case "M":
                return SetViewAsync(CalView.Month);

            case "a":
            case "A":
                return SetViewAsync(CalView.Agenda);

            case "ArrowLeft":
            case "p":
            case "P":
                return ShiftAsync(-1);

            case "ArrowRight":
            case "n":
            case "N":
                return ShiftAsync(1);

            case "c":
            case "C":
                if (_canCreate)
                {
                    StartNew(_selected, _selected);
                }

                return Task.CompletedTask;

            default:
                return Task.CompletedTask;
        }
    }

    // ── 작은 달력 ────────────────────────────────────────────────

    private DateTime MiniMonth => new DateTime(_anchor.Year, _anchor.Month, 1).AddMonths(_miniOffset);

    private string MiniTitle => $"{MiniMonth.Year}년 {MiniMonth.Month}월";

    /// <summary>
    /// 작은 달력의 마흔두 칸. <b>자료를 보지 않는다</b> — 날짜만 세므로
    /// 달을 넘겨도 조회가 나가지 않는다(구글의 작은 달력과 같다).
    /// </summary>
    private IEnumerable<DateOnly> MiniDays
    {
        get
        {
            var first = DateOnly.FromDateTime(MiniMonth);
            var start = first.AddDays(-(int)first.DayOfWeek);

            for (var i = 0; i < 42; i++)
            {
                yield return start.AddDays(i);
            }
        }
    }

    private string MiniDayCss(DateOnly day)
    {
        var css = new List<string>(4) { "pm-gc-mini__day" };

        if (day.Month != MiniMonth.Month || day.Year != MiniMonth.Year)
        {
            css.Add("pm-gc-mini__day--out");
        }

        if (day == Today)
        {
            css.Add("pm-gc-mini__day--today");
        }

        // 지금 보고 있는 구간에 드는 날은 옅게 깔아 둔다 — 큰 달력이 어디를
        // 보고 있는지가 작은 달력에 그대로 비친다.
        if (day >= DateOnly.FromDateTime(Range.Start) && day <= DateOnly.FromDateTime(Range.End))
        {
            css.Add("pm-gc-mini__day--in");
        }

        return string.Join(' ', css);
    }

    // ── 달력 재료 ────────────────────────────────────────────────

    /// <summary>
    /// 읽어 온 줄을 달력 약속으로 옮긴다(주·일 보기의 `DxScheduler` 가 쓴다).
    ///
    /// <para>
    /// **날짜만 있고 시각이 없는 자료다.** 종일 일정으로 그린다 — 시간표처럼
    /// 그리면 모든 일정이 자정에 붙어 한 줄로 겹친다.
    /// </para>
    ///
    /// <para>
    /// 끝을 그날 <c>23:59:59</c> 로 둔다. 자정으로 두면 부품이 끝을 여는
    /// 구간으로 볼 때 **마지막 날이 통째로 빠진다** — 하루짜리 일정이 아예
    /// 안 그려진다.
    /// </para>
    /// </summary>
    private void BuildCalendar()
    {
        var today = Today;

        _items = [.. _rows
            .Select(w => (Row: w, Span: Span(w)))
            .Where(x => x.Span is not null)
            .Select(x => new CalendarItem
            {
                Id = x.Row.WbsId,
                Subject = Title(x.Row),
                Description = Describe(x.Row),
                Start = x.Span!.Value.Start.ToDateTime(TimeOnly.MinValue),
                End = x.Span!.Value.End.ToDateTime(new TimeOnly(23, 59, 59)),
                AllDay = true,
                LabelId = LabelOf(x.Row, today),
            })];

        _storage = new DxSchedulerDataStorage
        {
            AppointmentsSource = _items,
            AppointmentMappings = new DxSchedulerAppointmentMappings
            {
                Id = nameof(CalendarItem.Id),
                Start = nameof(CalendarItem.Start),
                End = nameof(CalendarItem.End),
                Subject = nameof(CalendarItem.Subject),
                Description = nameof(CalendarItem.Description),
                AllDay = nameof(CalendarItem.AllDay),
                LabelId = nameof(CalendarItem.LabelId),
            },
            AppointmentLabelsSource = StateLabels,
            AppointmentLabelMappings = new DxSchedulerAppointmentLabelMappings
            {
                Id = nameof(StateLabel.Id),
                Caption = nameof(StateLabel.Caption),
                Color = nameof(StateLabel.Color),
            },
        };
    }

    /// <summary>말풍선에 띄울 한 줄. 담당과 종류가 없으면 제목만으로는 누구 일인지 모른다.</summary>
    private static string Describe(WbsItemDto w)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(w.DevUser)) parts.Add($"담당 {w.DevUser}");
        if (!string.IsNullOrWhiteSpace(w.ProcTp)) parts.Add(w.ProcTp);
        if (!string.IsNullOrWhiteSpace(w.Comm)) parts.Add(w.Comm);

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// 색을 고른다. 서버가 내는 상태는 대기·진행·완료 셋이고, **지연**은
    /// 여기서 센다 — 계획 종료일이 지났는데 아직 안 끝난 것(머리말).
    /// </summary>
    private static string LabelOf(WbsItemDto w, DateOnly today) => w.WbsState switch
    {
        "COMP" => "COMP",
        _ when w.PlanEdt is not null && w.PlanEdt < today => "OVER",
        "RUNNING" => "RUNNING",
        _ => "READY",
    };

    private static string StateName(WbsItemDto w) => LabelOf(w, Today) switch
    {
        "COMP" => "완료",
        "OVER" => "지연",
        "RUNNING" => "진행",
        _ => "대기",
    };

    private static string StateClass(WbsItemDto w) => LabelOf(w, Today) switch
    {
        "COMP" => "jsini-badge--on",
        "OVER" => "jsini-badge--err",
        "RUNNING" => "jsini-badge--warn",
        _ => "jsini-badge--off",
    };

    // ── 월 격자 ──────────────────────────────────────────────────

    /// <summary>
    /// 쓸기로 칠 최소 거리(px). 작게 두면 <b>칸을 누르는 손짓이 쓸기로 새서</b>
    /// 날을 고르려다 달이 넘어간다.
    /// </summary>
    private const double SwipeMin = 48;

    private static readonly DayOfWeek[] Weekdays =
    [
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
    ];

    /// <summary>
    /// 요일 이름. <b>문화권을 타지 않게</b> 직접 적는다 — 서버의
    /// <c>CurrentCulture</c> 가 무엇이냐에 따라 달력 머리가 <c>Sun</c> 으로
    /// 바뀌면 안 된다.
    /// </summary>
    private static string DowName(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => "일",
        DayOfWeek.Monday => "월",
        DayOfWeek.Tuesday => "화",
        DayOfWeek.Wednesday => "수",
        DayOfWeek.Thursday => "목",
        DayOfWeek.Friday => "금",
        _ => "토",
    };

    private static string DowCss(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => "pm-gc__sun",
        DayOfWeek.Saturday => "pm-gc__sat",
        _ => string.Empty,
    };

    /// <summary>
    /// 격자를 다시 짠다. <b>읽어 온 줄을 날짜별로 갈라 두는 것</b>이 전부다 —
    /// 그려질 때마다 세면 한 달에 마흔두 번 훑게 된다.
    /// </summary>
    /// <remarks>
    /// 걸치는 날을 <b>구간 안에서만</b> 돈다. 계획이 몇 해에 걸친 줄이 실제로
    /// 있어서, 자르지 않고 돌면 한 줄 때문에 천 번을 돈다.
    /// </remarks>
    private void BuildDays()
    {
        var span = GridSpan;
        var first = DateOnly.FromDateTime(span.Start);
        var last = DateOnly.FromDateTime(span.End);

        var byDay = new Dictionary<DateOnly, List<WbsItemDto>>();

        foreach (var w in _rows.OrderBy(w => w.PlanSdt).ThenBy(w => w.ProcNm, StringComparer.Ordinal))
        {
            if (Span(w) is not { } range)
            {
                continue;
            }

            var from = range.Start < first ? first : range.Start;
            var to = range.End > last ? last : range.End;

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (!byDay.TryGetValue(day, out var bucket))
                {
                    byDay[day] = bucket = [];
                }

                bucket.Add(w);
            }
        }

        var days = new List<CalDay>();

        for (var day = first; day <= last; day = day.AddDays(1))
        {
            days.Add(new CalDay(
                day,
                InAnchorMonth(day),
                byDay.TryGetValue(day, out var items) ? items : []));
        }

        _days = days;
    }

    /// <summary>
    /// 주 한 줄씩 막대를 <b>자리(lane)에 나눠 담는다.</b> 구글의 월 보기가
    /// 하는 일이 이것이다 — 여러 날에 걸친 일이 칸을 가로질러 한 줄로 흐르고,
    /// 서로 겹치는 것만 아래 줄로 내려간다.
    ///
    /// <para>
    /// 먼저 시작한 것, 같이 시작했으면 <b>긴 것</b>이 위에 선다. 긴 것을
    /// 위로 올려야 짧은 것들이 그 아래에서 자리를 나눠 갖고, 한 주가
    /// 들쭉날쭉해지지 않는다.
    /// </para>
    ///
    /// <para>
    /// 넘치는 것을 여기서 자르지 않는다 — 몇 줄까지 보일지(<see cref="MaxLanes"/>)
    /// 는 화면 폭에 딸린 값이라 <b>그리는 때</b> 갈린다. 여기서 자르면 폭이
    /// 바뀔 때마다 다시 짜야 한다.
    /// </para>
    /// </summary>
    private void BuildWeeks()
    {
        var weeks = new List<WeekRow>();

        for (var i = 0; i + 7 <= _days.Count; i += 7)
        {
            var days = _days.Skip(i).Take(7).ToList();
            var from = days[0].Date;
            var to = days[6].Date;

            // 위에 설 차례를 여기서 정한다 — 먼저 시작한 것, 같이 시작했으면
            // 긴 것. 자리를 나누는 계산은 `CalendarLanes` 가 한다.
            var ordered = _rows
                .Select(w => (Row: w, Span: Span(w)))
                .Where(x => x.Span is not null)
                .Select(x => (x.Row, x.Span!.Value.Start, x.Span!.Value.End))
                .Where(x => x.Start <= to && x.End >= from)
                .OrderBy(x => x.Start)
                .ThenByDescending(x => x.End.DayNumber - x.Start.DayNumber)
                .ThenBy(x => x.Row.ProcNm, StringComparer.Ordinal)
                .ToList();

            var slots = CalendarLanes.Pack([.. ordered.Select(x => (x.Start, x.End))], from);

            var bars = slots
                .Select(s => new Bar(
                    ordered[s.Index].Row, s.Col, s.Span, s.Lane, s.ClipStart, s.ClipEnd))
                .ToList();

            weeks.Add(new WeekRow(days, bars));
        }

        _weeks = weeks;
    }

    /// <summary>그 칸에서 <b>접힌</b> 일정 수. 「N개 더보기」가 이것을 적는다.</summary>
    private int HiddenAt(WeekRow week, int col) =>
        week.Bars.Count(b => b.Lane >= MaxLanes && col >= b.Col && col < b.Col + b.Span);

    /// <summary>
    /// 이 날이 <b>보고 있는 달</b>의 날인가. 아니면 흐리게 그린다.
    /// 주·일 보기에는 흐릴 자투리가 없으므로 전부 참이다.
    /// </summary>
    private bool InAnchorMonth(DateOnly day) =>
        Preset != DateRangePreset.Month
        || (day.Year == _anchor.Year && day.Month == _anchor.Month);

    /// <summary>
    /// 고른 날을 <b>보고 있는 구간 안으로</b> 붙든다.
    ///
    /// <para>
    /// 달을 넘기면 고른 날이 화면 밖으로 나간다. 그대로 두면 아래 목록이
    /// <b>안 보이는 날</b>의 일정을 편 채로 남고, 거기서 「등록」을 누르면
    /// 만든 줄이 지난달로 들어가 저장하자마자 사라진 것처럼 보인다.
    /// </para>
    ///
    /// <para>
    /// <b>재는 자가 둘이다.</b> 「그냥 둘까」는 격자(<see cref="GridSpan"/>)로,
    /// 「어디로 데려갈까」는 달(<see cref="Range"/>)로 잰다. 앞의 것을 달로
    /// 재면 자투리 칸(9월 격자의 10/1)을 누른 순간 조건 하나만 건드려도
    /// 고른 날이 1일로 튀고, 뒤의 것을 격자로 재면 「등록」의 기본 날이
    /// <b>지난달</b>이 된다(2025-01 을 여는데 2024-12-29 가 잡힌다).
    /// </para>
    /// </summary>
    private void SyncSelected()
    {
        var span = GridSpan;

        // 격자에 그려지는 날이면 그대로 둔다 — 눈에 보이는 것을 빼앗지 않는다.
        if (_selected >= DateOnly.FromDateTime(span.Start)
            && _selected <= DateOnly.FromDateTime(span.End))
        {
            return;
        }

        var start = DateOnly.FromDateTime(Range.Start);
        var end = DateOnly.FromDateTime(Range.End);
        var today = Today;

        // 「이번 달」을 열었을 때 오늘이 펴져 있는 것이 가장 자주 맞는 답이다.
        _selected = today >= start && today <= end ? today : start;
    }

    /// <summary>
    /// 칸의 <b>빈 자리</b>를 눌렀다.
    ///
    /// <para>
    /// 넓은 화면에서는 구글처럼 <b>새 일정</b>이 열린다. 휴대폰에서는 그 날을
    /// 고르기만 한다 — 손가락은 빗나가고, 빗나간 자리에서 창이 열리면 매번
    /// 닫아야 한다. 대신 격자 아래 목록이 그 날로 바뀐다.
    /// </para>
    ///
    /// <para>
    /// 만들 수 없는 사람에게는 그 날을 <b>펼쳐</b> 보여 준다 — 누를 곳이
    /// 아무 일도 안 하면 화면이 고장 난 것처럼 보인다.
    /// </para>
    /// </summary>
    private Task OnDayClickAsync(DateOnly day)
    {
        _selected = day;

        if (_isPhone)
        {
            return Task.CompletedTask;
        }

        if (_canCreate)
        {
            StartNew(day, day);
        }
        else
        {
            Peek(day);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 날짜 숫자를 눌렀다 — 구글과 같이 <b>그 날 보기</b>로 들어간다.
    /// 휴대폰에서는 보기를 갈지 않고 그 날을 편다(아래 목록이 그 일을 한다).
    /// </summary>
    private Task OnDayNumberAsync(DateOnly day)
    {
        _selected = day;

        if (_isPhone)
        {
            return Task.CompletedTask;
        }

        _anchor = day.ToDateTime(TimeOnly.MinValue);
        _view = CalView.Day;
        _miniOffset = 0;

        return SearchAsync();
    }

    /// <summary>그 날을 작은 창으로 펼친다 — 접힌 것만이 아니라 <b>그 날 전부</b>다.</summary>
    private void Peek(DateOnly day)
    {
        _peekDay = day;
        _selected = day;
        _peeking = true;
    }

    private string DayCss(CalDay day)
    {
        var css = new List<string>(6) { "pm-gc-day" };

        if (!day.InMonth)
        {
            css.Add("pm-gc-day--out");
        }

        if (day.Date == Today)
        {
            css.Add("pm-gc-day--today");
        }

        if (day.Date == _selected)
        {
            css.Add("pm-gc-day--on");
        }

        if (_drag is not null && _dragOver == day.Date)
        {
            css.Add("pm-gc-day--drop");
        }

        css.Add(day.Date.DayOfWeek switch
        {
            DayOfWeek.Sunday => "pm-gc-day--sun",
            DayOfWeek.Saturday => "pm-gc-day--sat",
            _ => "pm-gc-day--wd",
        });

        return string.Join(' ', css);
    }

    /// <summary>
    /// 막대를 칸 위에 얹는다. <c>grid-column</c> 으로 <b>칸을 가로지른다</b> —
    /// 이것이 구글의 월 보기와 우리 옛 판이 갈리는 지점이다.
    /// </summary>
    private static string BarStyle(Bar bar) =>
        $"grid-column: {bar.Col + 1} / span {bar.Span}; grid-row: {bar.Lane + 1};";

    private string MoreStyle(int col) =>
        $"grid-column: {col + 1}; grid-row: {MaxLanes + 1};";

    /// <summary>
    /// 막대의 꼴. 주를 넘어가는 쪽은 모서리를 펴 둔다 — 잘린 끝이 둥글면
    /// <b>거기서 끝난 것</b>으로 보인다.
    /// </summary>
    private static string BarCss(Bar bar)
    {
        var css = new List<string>(4) { "pm-gc-bar", $"pm-gc-bar--{Label(bar.Item)}" };

        if (bar.ClipStart) css.Add("pm-gc-bar--clip-s");
        if (bar.ClipEnd) css.Add("pm-gc-bar--clip-e");

        return string.Join(' ', css);
    }

    private static string BarTip(WbsItemDto w)
    {
        var rest = Describe(w);

        return rest.Length == 0
            ? $"{Title(w)} · {Period(w)}"
            : $"{Title(w)} · {Period(w)} · {rest}";
    }

    /// <summary>
    /// 칸을 읽어 주는 말. 막대는 글자가 잘려 있고 「+2」 는 소리로 아무
    /// 뜻이 없어서, 화면 낭독기에는 <b>날짜와 건수</b>를 준다.
    /// </summary>
    private static string DayLabel(CalDay day) =>
        day.Items.Count == 0
            ? $"{day.Date.Month}월 {day.Date.Day}일"
            : $"{day.Date.Month}월 {day.Date.Day}일, 일정 {day.Items.Count}건";

    /// <summary>제목. 비어 있는 줄이 실제로 있어서 빈칸으로 두지 않는다.</summary>
    private static string Title(WbsItemDto w) =>
        string.IsNullOrWhiteSpace(w.ProcNm) ? "(제목 없음)" : w.ProcNm;

    /// <summary>목록 한 줄의 아랫줄 — 기간에 담당·종류·메모를 잇는다.</summary>
    private static string Detail(WbsItemDto w)
    {
        var rest = Describe(w);

        return rest.Length == 0 ? Period(w) : $"{Period(w)} · {rest}";
    }

    private static string Period(WbsItemDto w) => Span(w) is not { } range
        ? "기간 없음"
        : range.Start == range.End
            ? $"{range.Start:MM.dd}"
            : $"{range.Start:MM.dd} ~ {range.End:MM.dd}";

    private static string Label(WbsItemDto w) => LabelOf(w, Today);

    /// <summary>
    /// 한 줄이 걸치는 날. 한쪽만 적힌 줄은 그 날 하루로 본다.
    ///
    /// <para>
    /// <b>거꾸로 들어간 줄이 있다.</b> 그대로 두면 달력 부품이 음수 길이를
    /// 만나고, 격자 쪽은 하루도 안 그린다. 여기 한 곳에서 바로잡는다 —
    /// 달력과 격자가 <b>같은 자를 써야</b> 같은 일정이 같은 날에 뜬다.
    /// </para>
    /// </summary>
    private static (DateOnly Start, DateOnly End)? Span(WbsItemDto w)
    {
        if (w.PlanSdt is null && w.PlanEdt is null)
        {
            return null;
        }

        var start = w.PlanSdt ?? w.PlanEdt!.Value;
        var end = w.PlanEdt ?? start;

        return (start, end < start ? start : end);
    }

    // ── 끌어 옮기기 (월 격자) ────────────────────────────────────

    /// <summary>
    /// 막대를 다른 날에 놓았다 — <b>기간은 그대로 두고</b> 통째로 옮긴다.
    /// 구글에서 사흘짜리 일을 끌면 사흘짜리인 채로 옮겨지는 것과 같다.
    /// </summary>
    /// <remarks>
    /// 놓은 날이 <b>시작일</b>이다. 막대의 어느 지점을 집었는지 브라우저가
    /// 알려 주지 않으므로, 「집은 자리만큼 어긋나게」는 만들 수 없다.
    /// </remarks>
    private async Task DropAsync(DateOnly day)
    {
        var row = _drag;

        _drag = null;
        _dragOver = null;

        if (row is null || !_canUpdate || Span(row) is not { } range || range.Start == day)
        {
            return;
        }

        var next = Copy(row);
        next.PlanSdt = day;
        next.PlanEdt = day.AddDays(range.End.DayNumber - range.Start.DayNumber);

        if (await RunAsync(() => Api.UpdateAsync(next), "옮겼습니다.", "옮기지 못했습니다"))
        {
            await SearchAsync();
        }
    }

    private void OnTouchStart(TouchEventArgs e)
    {
        if (e.Touches.Length == 0)
        {
            return;
        }

        _touchX = e.Touches[0].ClientX;
        _touchY = e.Touches[0].ClientY;
    }

    /// <summary>
    /// 좌우로 쓸면 앞뒤 구간으로 간다.
    ///
    /// <para>
    /// <b>세로로 더 많이 움직였으면 아무 일도 하지 않는다.</b> 이 격자는
    /// 화면 위쪽에 있어서 쪽을 굴리는 손짓이 여기서 시작한다 — 가로만 보고
    /// 넘기면 <b>목록을 보려고 내릴 때마다 달이 바뀐다.</b>
    /// </para>
    ///
    /// <para>
    /// 쓸기는 <b>덤</b>이다. 안 먹어도 머리줄의 ‹ › 가 같은 일을 한다.
    /// </para>
    /// </summary>
    private Task OnTouchEndAsync(TouchEventArgs e)
    {
        if (e.ChangedTouches.Length == 0)
        {
            return Task.CompletedTask;
        }

        var dx = e.ChangedTouches[0].ClientX - _touchX;
        var dy = e.ChangedTouches[0].ClientY - _touchY;

        if (Math.Abs(dx) < SwipeMin || Math.Abs(dy) >= Math.Abs(dx))
        {
            return Task.CompletedTask;
        }

        // 왼쪽으로 쓸면 다음 달이다 — 종이를 넘기는 쪽과 같다.
        return ShiftAsync(dx < 0 ? 1 : -1);
    }

    // ── 편집 창 ──────────────────────────────────────────────────

    /// <summary>
    /// 부품이 주는 편집 창을 취소하고 우리 창을 연다(머리말).
    /// 빈 칸을 더블클릭했으면 새로, 일정을 눌렀으면 그 줄을 고치러 연다.
    /// </summary>
    private void OnFormShowing(SchedulerAppointmentFormEventArgs e)
    {
        e.Cancel = true;

        var appointment = e.Appointment;

        if (appointment.IsNew || appointment.Id is null)
        {
            StartNew(Day(appointment.Start), LastDay(appointment));
            return;
        }

        var row = Find(appointment.Id);

        if (row is not null)
        {
            StartEdit(row);
        }
    }

    private void StartNew(DateOnly from, DateOnly to)
    {
        _form = new WbsItemDto
        {
            PrjRid = ProjectRid,
            ScheduleType = NewKind,
            PlanSdt = from,
            PlanEdt = to < from ? from : to,
        };

        _complete = false;
        _isNew = true;
        _peeking = false;
        _editing = true;
    }

    /// <summary>
    /// 편집 창에 <b>복사본</b>을 띄운다. 목록의 줄을 그대로 묶으면 취소해도
    /// 화면에는 이미 바뀐 값이 남는다.
    /// </summary>
    private void StartEdit(WbsItemDto w)
    {
        _form = Copy(w);
        _complete = w.DevEdt is not null;
        _isNew = false;
        _peeking = false;
        _editing = true;
    }

    /// <summary>
    /// 새 줄에 붙일 구분. <b>딱 하나만 고른 상태일 때만</b> 그것을 따른다 —
    /// 여럿을 고른 채로 등록하면 어느 것에 넣어야 할지 알 수 없고, 임의로
    /// 첫 번째를 집으면 <b>고른 적 없는 구분으로 조용히 들어간다</b>(머리말).
    /// </summary>
    private string NewKind =>
        _scheduleTypes.Count == 1 ? _scheduleTypes[0] : DefaultKind;

    /// <summary>
    /// 완료 표시를 값으로 옮긴다 — 원본 `OnSubmit` 의 규칙 그대로다.
    ///
    /// <para>
    /// 상태(<c>WbsState</c>)는 적는 칸이 아니다. 서버가 개발 시작·종료일로
    /// 정하므로 여기서 바꾸는 것도 그 두 날이다.
    /// </para>
    ///
    /// <para>
    /// **표시를 끄면 개발 종료일을 지운다.** 원본은 끄기만 하고 날짜를 그대로
    /// 둬서 상태가 완료에 눌어붙었다 — 체크를 풀어도 아무 일이 없었다.
    /// </para>
    /// </summary>
    private void ApplyComplete()
    {
        var today = Today;

        if (!_complete)
        {
            _form.DevEdt = null;
            return;
        }

        _form.DevSdt ??= today;
        _form.DevEdt ??= today;

        // 계획이 끝난 날보다 뒤에 남아 있으면 「완료인데 아직 계획 중」이 된다.
        if (_form.PlanSdt > _form.DevEdt) _form.PlanSdt = _form.DevEdt;
        if (_form.PlanEdt > _form.DevEdt) _form.PlanEdt = _form.DevEdt;
    }

    /// <param name="moveToToday">원본의 「오늘로 옮겨 저장」. 계획을 오늘 하루로 옮긴다.</param>
    private async Task SaveFormAsync(bool moveToToday)
    {
        if (string.IsNullOrWhiteSpace(_form.ProcNm))
        {
            Say("일정 이름을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        if (_form.PrjRid is null)
        {
            Say("프로젝트를 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (moveToToday)
        {
            _form.PlanSdt = Today;
            _form.PlanEdt = _form.PlanSdt;
        }

        if (_form.PlanEdt < _form.PlanSdt)
        {
            Say("종료일이 시작일보다 앞섭니다.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_form.ScheduleType))
        {
            _form.ScheduleType = NewKind;
        }

        ApplyComplete();

        var isNew = _isNew;

        if (await RunAsync(
                () => isNew ? Api.CreateAsync(_form) : Api.UpdateAsync(_form),
                isNew ? "등록했습니다." : "저장했습니다.",
                isNew ? "등록하지 못했습니다" : "저장하지 못했습니다"))
        {
            _editing = false;
            await SearchAsync();
        }
    }

    private async Task DeleteFormAsync()
    {
        if (_isNew || _confirm is null)
        {
            return;
        }

        if (!await _confirm.AskAsync($"「{_form.ProcNm}」 을(를) 지웁니다.\n되돌릴 수 없습니다."))
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteAsync(_form.WbsId), "지웠습니다.", "지우지 못했습니다"))
        {
            _editing = false;
            await SearchAsync();
        }
    }

    // ── 주·일 보기에서 바로 옮기기·지우기 ────────────────────────

    /// <summary>
    /// 끌어 옮기거나 크기를 바꾼 것. **계획 날짜만 바뀐다** — 나머지 칸은
    /// 읽어 둔 줄에서 그대로 가져간다(달력이 모르는 칸이 지워지지 않게).
    /// </summary>
    private async Task OnUpdatingAsync(SchedulerAppointmentOperationEventArgs e)
    {
        var row = Find(e.Appointment.Id);

        if (row is null)
        {
            return;
        }

        var next = Copy(row);
        next.PlanSdt = Day(e.Appointment.Start);
        next.PlanEdt = LastDay(e.Appointment);

        if (next.PlanSdt == row.PlanSdt && next.PlanEdt == row.PlanEdt)
        {
            return;
        }

        // 부품이 자기 저장소를 먼저 고쳤다. 실패했으면 다시 읽어 되돌린다.
        await RunAsync(() => Api.UpdateAsync(next), "옮겼습니다.", "옮기지 못했습니다");
        await SearchAsync();
    }

    /// <summary>
    /// 달력에서 지운 것. **부품이 지우기 전에 막고**(<c>Cancel</c>) 먼저 묻는다 —
    /// 「아니오」를 눌렀는데 화면에서만 사라져 있으면 지운 줄 안다.
    /// </summary>
    private async Task OnRemovingAsync(SchedulerAppointmentOperationEventArgs e)
    {
        e.Cancel = true;

        var row = Find(e.Appointment.Id);

        if (row is null || _confirm is null)
        {
            return;
        }

        if (!await _confirm.AskAsync($"「{row.ProcNm}」 을(를) 지웁니다.\n되돌릴 수 없습니다."))
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteAsync(row.WbsId), "지웠습니다.", "지우지 못했습니다"))
        {
            await SearchAsync();
        }
    }

    // ── 표 쪽 ────────────────────────────────────────────────────

    /// <summary>
    /// 새 일정은 <b>고른 프로젝트와 구분에 붙는다.</b> 구분을 비워 두면 어느
    /// 화면에서도 안 보이는 줄이 된다.
    /// </summary>
    private void FillNew(WbsItemDto w)
    {
        w.PrjRid = ProjectRid;
        w.ScheduleType = NewKind;

        // 고른 기간의 첫날을 기본으로 둔다 — 안 그러면 만든 줄이 지금 보고
        // 있는 구간 밖으로 떨어져 저장 직후 목록에서 사라진다.
        w.PlanSdt = DateOnly.FromDateTime(Range.Start);
        w.PlanEdt = w.PlanSdt;
    }

    private async Task SaveRowAsync((WbsItemDto Item, bool IsNew) e)
    {
        if (e.IsNew)
        {
            await Api.CreateAsync(e.Item);
        }
        else
        {
            await Api.UpdateAsync(e.Item);
        }
    }

    private Task DeleteAsync(WbsItemDto w) => Api.DeleteAsync(w.WbsId);

    // ── 도우미 ───────────────────────────────────────────────────

    private WbsItemDto? Find(object? id) =>
        int.TryParse(id?.ToString(), out var wbsId)
            ? _rows.FirstOrDefault(w => w.WbsId == wbsId)
            : null;

    private static DateOnly Day(DateTime value) => DateOnly.FromDateTime(value);

    /// <summary>
    /// 약속의 <b>마지막 날</b>. 끝이 자정이면 그 앞날이다 — 종일 일정의 끝을
    /// 여는 구간(다음 날 0시)으로 주는 자리가 있어서, 그대로 받으면 하루가
    /// 늘어난다.
    /// </summary>
    private static DateOnly LastDay(DxSchedulerAppointmentItem appointment)
    {
        var end = appointment.End;

        if (end.TimeOfDay == TimeSpan.Zero && end.Date > appointment.Start.Date)
        {
            end = end.AddDays(-1);
        }

        return DateOnly.FromDateTime(end);
    }

    private static WbsItemDto Copy(WbsItemDto w) => new()
    {
        WbsId = w.WbsId,
        PrjRid = w.PrjRid,
        ProcId = w.ProcId,
        Gb1 = w.Gb1,
        Gb2 = w.Gb2,
        ProcNm = w.ProcNm,
        ProcTp = w.ProcTp,
        ProcLvl = w.ProcLvl,
        BuildUser = w.BuildUser,
        BuildStatus = w.BuildStatus,
        DevUser = w.DevUser,
        PlanSdt = w.PlanSdt,
        PlanEdt = w.PlanEdt,
        DevSdt = w.DevSdt,
        DevEdt = w.DevEdt,
        DevChk = w.DevChk,
        BuildChk = w.BuildChk,
        BuildChkDt = w.BuildChkDt,
        QcUser = w.QcUser,
        QcChk = w.QcChk,
        QcChkDt = w.QcChkDt,
        Comm = w.Comm,
        ScheduleType = w.ScheduleType,
    };

    /// <summary>주·일 보기의 약속. 부품이 끌어 옮길 때 여기 값을 고치므로 <b>쓸 수 있어야 한다</b>.</summary>
    private sealed class CalendarItem
    {
        public int Id { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool AllDay { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LabelId { get; set; } = "READY";
    }

    /// <summary>
    /// 격자의 한 칸.
    /// </summary>
    /// <param name="Date">그 날</param>
    /// <param name="InMonth">보고 있는 달의 날인가 — 아니면 흐리게 그린다</param>
    /// <param name="Items">그 날에 걸치는 일정. <b>걸치기만 해도 들어간다</b></param>
    private sealed record CalDay(DateOnly Date, bool InMonth, IReadOnlyList<WbsItemDto> Items);

    /// <summary>
    /// 한 주 안에 놓인 막대 하나.
    /// </summary>
    /// <param name="Item">그 일정</param>
    /// <param name="Col">이 주의 몇째 칸에서 시작하나 (0=일요일)</param>
    /// <param name="Span">몇 칸을 가로지르나</param>
    /// <param name="Lane">위에서 몇째 줄인가</param>
    /// <param name="ClipStart">앞 주에서 이어져 왔나 — 왼쪽 모서리를 편다</param>
    /// <param name="ClipEnd">다음 주로 이어지나 — 오른쪽 모서리를 편다</param>
    private sealed record Bar(
        WbsItemDto Item, int Col, int Span, int Lane, bool ClipStart, bool ClipEnd);

    /// <summary>월 격자의 한 주. 칸 일곱과 그 위에 얹히는 막대들이다.</summary>
    private sealed record WeekRow(IReadOnlyList<CalDay> Days, IReadOnlyList<Bar> Bars);

    /// <summary>상태 색. 구글 캘린더의 색조로 옮긴 넷이다.</summary>
    private sealed record StateLabel(string Id, string Caption, System.Drawing.Color Color);

    private static readonly StateLabel[] StateLabels =
    [
        new("READY", "대기", System.Drawing.Color.FromArgb(0xF9, 0xAB, 0x00)),
        new("RUNNING", "진행", System.Drawing.Color.FromArgb(0x1A, 0x73, 0xE8)),
        new("OVER", "지연", System.Drawing.Color.FromArgb(0xD9, 0x30, 0x25)),
        new("COMP", "완료", System.Drawing.Color.FromArgb(0x80, 0x86, 0x8B)),
    ];
}
