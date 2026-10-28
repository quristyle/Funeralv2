using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
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

    [Inject] private CommonCodes Codes { get; set; } = default!;

    [Inject] private BizOptions Biz { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Inject] private CurrentUser Me { get; set; } = default!;

    /// <summary>마지막에 고른 프로젝트를 브라우저에 적어 둘 때 쓰는 열쇠 앞부분. 뒤에 로그인 아이디가 붙는다.</summary>
    internal const string ProjectStorageKeyPrefix = "jsini-sched-prj:";

    private string ProjectStorageKey =>
        $"{ProjectStorageKeyPrefix}{(string.IsNullOrWhiteSpace(Me?.Username) ? "?" : Me.Username.Trim())}";

    /// <summary>로그인한 사용자의 성명. 없으면 아이디. 새 일정 작성 시 담당자로 자동 지정된다.</summary>
    private string? CurrentUserName =>
        string.IsNullOrWhiteSpace(Me?.DisplayName)
            ? (string.IsNullOrWhiteSpace(Me?.Username) ? null : Me.Username.Trim())
            : Me.DisplayName.Trim();

    /// <summary>기억해 둔 프로젝트를 읽었는가. 그 전에는 프로젝트 고르개를 그리지 않는다.</summary>
    private bool _projectRestored;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        SchSummary.Or(_kindNames),
        _completeStateName,
        _view == CalView.Table && (_tableStart is not null || _tableEnd is not null)
            ? $"{_tableStart:yyyy-MM-dd} ~ {_tableEnd:yyyy-MM-dd}"
            : null);

    /// <summary>고른 값의 이름들. 코드만 들고, 목록은 CodeSelect 가 쥐고 있어 부품이 @bind-Text 로 올려 준다.</summary>
    private string? _projectName;

    private string? _kindNames;

    private string? _completeStateName;

    /// <summary>표 형태일 때 조회할 시작일.</summary>
    private DateOnly? _tableStart;

    /// <summary>표 형태일 때 조회할 종료일.</summary>
    private DateOnly? _tableEnd;

    /// <summary>작은 캘린더에서 범위 선택 시 첫째 날을 찍고 둘째 날을 대기 중인가.</summary>
    private bool _selectingRange;

    private DateTime? TableStartDateTime => _tableStart?.ToDateTime(TimeOnly.MinValue);
    private DateTime? TableEndDateTime => _tableEnd?.ToDateTime(TimeOnly.MinValue);

    private Task OnTableStartChangedAsync(DateTime? val)
    {
        _tableStart = val is { } dt ? DateOnly.FromDateTime(dt) : null;
        _selectingRange = false;
        return SearchAsync();
    }

    private Task OnTableEndChangedAsync(DateTime? val)
    {
        _tableEnd = val is { } dt ? DateOnly.FromDateTime(dt) : null;
        _selectingRange = false;
        return SearchAsync();
    }

    private DateOnly NewItemStart => _view == CalView.Table && _tableStart is not null ? _tableStart.Value : _selected;
    private DateOnly NewItemEnd => _view == CalView.Table && _tableEnd is not null ? _tableEnd.Value : _selected;

    /// <summary>새 줄의 기본 구분. 「전체」일 때 쓴다 — 비워 두면 어느 화면에서도 안 보이는 줄이 된다.</summary>
    private const string DefaultKind = "Public";

    /// <summary>보기. 구간도 이것이 정한다 — 둘을 따로 두면 그려지는 달과 읽어 온 달이 어긋난다.</summary>
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

    /// <summary>표 보기에서 쓰는 구분 공통코드 목록.</summary>
    private IReadOnlyList<CommonCodeItem> _scheduleTypeCodes = [];

    /// <summary>표 보기에서 쓰는 담당자(포털 계정) 목록.</summary>
    private IReadOnlyList<BizOption> _userOptions = [];

    /// <summary>진행 상태 선택 옵션.</summary>
    private static readonly (string Code, string Name)[] StateOptions =
    [
        ("READY", "대기"),
        ("RUNNING", "진행"),
        ("COMP", "완료"),
    ];

    private CalView _view = CalView.Month;
    private DateTime _anchor = AppTime.TodayDate;

    private string? _projectCode;
    private string? _completeState;

    /// <summary>체크로 고른 구분. 비어 있으면 전체다.</summary>
    private IReadOnlyList<string> _scheduleTypes = [];

    private DxSchedulerDataStorage? _storage;
    private CalendarItem[] _items = [];

    /// <summary>휴대폰인가. 격자 칸 높이와 칸에 담는 막대 수가 갈린다.</summary>
    private bool _isPhone;

    /// <summary>격자의 칸들. 앞뒤 달의 자투리까지 들어 있다.</summary>
    private IReadOnlyList<CalDay> _days = [];

    /// <summary>주 한 줄씩 자리를 나눠 담은 것. 월 보기가 이것을 그린다.</summary>
    private IReadOnlyList<WeekRow> _weeks = [];

    /// <summary>고른 날. 휴대폰에서 격자 아래 목록이 이 날의 일정을 편다.</summary>
    private DateOnly _selected = AppTime.Today;

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
    private DateOnly _peekDay = AppTime.Today;

    private bool _editing;
    private bool _isNew;
    private bool _complete;
    private WbsItemDto _form = new();

    private bool _canCreate;
    private bool _canUpdate;
    private bool _canDelete;

    private static DateOnly Today => AppTime.Today;

    /// <summary>보기가 정하는 조회 구간의 단위.</summary>
    private DateRangePreset Preset => _view switch
    {
        CalView.Day => DateRangePreset.Day,
        CalView.Week => DateRangePreset.Week,
        _ => DateRangePreset.Month,
    };

    private DateRange Range => DateRange.Of(Preset, _anchor);

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string Hint => $"{_rows.Count}건";

    /// <summary>주·일 보기에서만 쓴다. 월은 직접 그린다.</summary>
    private SchedulerViewType ViewType =>
        _view == CalView.Day ? SchedulerViewType.Day : SchedulerViewType.Week;

    /// <summary>머리줄에 적는 지금 보고 있는 곳. 달이면 「2026년 9월」, 주면 기간, 날이면 요일까지 적는다.</summary>
    private string BarTitle
    {
        get
        {
            if (_view == CalView.Day)
            {
                return $"{_anchor.Year}년 {_anchor.Month}월 {_anchor.Day}일 ({DowName(_anchor.DayOfWeek)})";
            }

            if (_view == CalView.Table)
            {
                if (_tableStart is not null && _tableEnd is not null)
                {
                    return _tableStart == _tableEnd
                        ? $"{_tableStart.Value:yyyy년 M월 d일}"
                        : $"{_tableStart.Value:yyyy년 M월 d일} – {_tableEnd.Value:yyyy년 M월 d일}";
                }
                if (_tableStart is not null) return $"{_tableStart.Value:yyyy년 M월 d일} ~";
                if (_tableEnd is not null) return $"~ {_tableEnd.Value:yyyy년 M월 d일}";
                return $"{_anchor.Year}년 {_anchor.Month}월";
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

    /// <summary>한 칸에 보이는 막대 수. 넘는 것은 「+N개 더보기」가 된다. 칸 높이와 한 몸이라, 늘리면 CSS 의 칸 최소 높이도 같이 늘려야 마지막 줄이 안 잘린다.</summary>
    private int MaxLanes => _isPhone ? 2 : 3;

    /// <summary>격자·조회가 함께 쓰는 구간. 달 보기는 앞뒤 달의 자투리 날까지 넓히고, 주·일 보기는 그대로다.</summary>
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
        // PermissionView 와 똑같이 묻는다. 끌기·크기 조절은 파라미터로 켜고 끄므로 직접 판정한다.
        var path = CurrentPath;

        _canCreate = Permissions.Can(path, MenuAction.Create);
        _canUpdate = Permissions.Can(path, MenuAction.Update);
        _canDelete = Permissions.Can(path, MenuAction.Delete);

        // 첫 렌더 시 달력 틀이 즉시 표시되도록 초기 격자를 구성한다.
        SyncSelected();
        BuildCalendar();
        BuildDays();
        BuildWeeks();
    }

    // 화면을 열 때 여기서 조회하지 않는다 — 프로젝트 고르개가 첫 항목을 고르며 조회를 건다. 둘 다 하면 늦게 돌아온 쪽이 남아 목록이 전체로 보인다.
    // 기억해 둔 프로젝트가 목록에 있으면 고르개는 아무것도 고르지 않으므로(OnChanged 가 안 온다) 그때만 여기서 조회한다.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || _projectRestored) return;

        await EnsureDropdownDataAsync();

        var remembered = await ReadRememberedProjectAsync();
        var known = false;

        if (!string.IsNullOrWhiteSpace(remembered))
        {
            var list = await Codes.GetAsync("projlist");
            known = list.Any(i => string.Equals(i.Code, remembered, StringComparison.Ordinal));
        }

        if (known)
        {
            _projectCode = remembered;
        }

        _projectRestored = true;
        StateHasChanged();

        if (known)
        {
            await SearchAsync();
            StateHasChanged();
        }
    }

    /// <summary>프로젝트가 바뀌었다(사람이 골랐거나 고르개가 첫 항목을 골랐다). 기억해 두고 조회한다.</summary>
    private Task OnProjectChangedAsync(CommonCodeItem? item)
    {
        _ = RememberProjectAsync(item?.Code);
        return SearchAsync();
    }

    private async Task<string?> ReadRememberedProjectAsync()
    {
        try
        {
            return await JS.InvokeAsync<string?>("localStorage.getItem", ProjectStorageKey);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task RememberProjectAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", ProjectStorageKey, code);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            // 저장소가 막혔거나 창이 닫혔다. 기억만 못 할 뿐이다.
        }
    }

    /// <summary>구분 체크가 바뀌었다. 바로 조회한다 — 「조회」를 또 눌러야 하면 딱지와 달력이 서로 다른 이야기를 한다.</summary>
    private Task OnKindsChangedAsync(IEnumerable<string>? kinds)
    {
        _scheduleTypes = kinds is null ? [] : [.. kinds];
        return SearchAsync();
    }

    private async Task SearchAsync()
    {
        await LoadAsync(async () =>
        {
            DateOnly? from;
            DateOnly? to;

            if (_view == CalView.Table)
            {
                from = _tableStart;
                to = _tableEnd;

                if (from is not null && to is not null && from > to)
                {
                    (from, to) = (to, from);
                    _tableStart = from;
                    _tableEnd = to;
                }
            }
            else
            {
                // 격자 구간으로 묻는다 — 달 1일~말일이 아니다.
                var span = GridSpan;
                from = DateOnly.FromDateTime(span.Start);
                to = DateOnly.FromDateTime(span.End);
            }

            _rows = await Api.ListAsync(
                ProjectRid, _completeState, _scheduleTypes,
                from, to);

            SyncSelected();
            BuildCalendar();
            BuildDays();
            BuildWeeks();

            await RefreshMiniAsync();

            return _rows.Count;
        }, "그 기간에 일정이 없습니다.", "일정표를 읽지 못했습니다");

        StateHasChanged();
    }

    /// <summary>화면 크기가 경계를 넘었다. 값만 받는다 — 칸에 담기는 막대 수(MaxLanes)는 그리는 시점에 갈린다.</summary>
    private void OnPhoneChanged(bool active) => _isPhone = active;

    // ── 머리줄 ───────────────────────────────────────────────────

    /// <summary>보기를 바꾼다. 구간이 따라 바뀌므로 다시 읽는다. 「일」로 갈 때 기준일을 오늘로 당기지 않는다 — 날을 눌러 들어오는 길이 있어, 여기서 되돌리면 그 길이 늘 오늘로 끌려간다.</summary>
    private async Task SetViewAsync(CalView view)
    {
        if (view == _view)
        {
            return;
        }

        _view = view;
        _miniOffset = 0;

        if (_view == CalView.Table)
        {
            await EnsureDropdownDataAsync();
            _tableStart ??= _selected;
            _tableEnd ??= _selected;
            _selectingRange = false;
        }

        await SearchAsync();
    }

    private Task GoTodayAsync()
    {
        _anchor = AppTime.TodayDate;
        _selected = Today;
        _miniOffset = 0;

        if (_view == CalView.Table)
        {
            _tableStart = Today;
            _tableEnd = Today;
            _selectingRange = false;
        }

        return SearchAsync();
    }

    /// <summary>앞뒤 구간으로 옮기고 다시 읽는다. 보기의 단위만큼 움직인다.</summary>
    private Task ShiftAsync(int direction)
    {
        if (_view == CalView.Table && _tableStart is not null && _tableEnd is not null)
        {
            var days = (_tableEnd.Value.DayNumber - _tableStart.Value.DayNumber) + 1;
            if (days < 1) days = 1;
            _tableStart = _tableStart.Value.AddDays(days * direction);
            _tableEnd = _tableEnd.Value.AddDays(days * direction);
            _anchor = _tableStart.Value.ToDateTime(TimeOnly.MinValue);
            _selected = _tableStart.Value;
            _selectingRange = false;
            _miniOffset = 0;
            return SearchAsync();
        }

        _anchor = Preset switch
        {
            DateRangePreset.Day => _anchor.AddDays(direction),
            DateRangePreset.Week => _anchor.AddDays(direction * 7),
            _ => _anchor.AddMonths(direction),
        };

        _miniOffset = 0;

        return SearchAsync();
    }

    /// <summary>그 날로 간다. 작은 달력과 날짜 누르기가 함께 쓴다 — 보기는 그대로 두고 기준일만 옮긴다.</summary>
    private Task JumpAsync(DateOnly day)
    {
        _anchor = day.ToDateTime(TimeOnly.MinValue);
        _selected = day;
        _miniOffset = 0;

        if (_view == CalView.Table)
        {
            _tableStart = day;
            _tableEnd = day;
            _selectingRange = false;
        }

        return SearchAsync();
    }

    /// <summary>작은 달력의 날짜를 클릭했다. 표 형태일 때는 선택한 날 또는 범위로 지정하고, 달력 보기일 때는 그 날로 이동한다.</summary>
    private async Task OnMiniDayClickAsync(DateOnly day, MouseEventArgs? e = null)
    {
        if (_view == CalView.Table)
        {
            if (e is not null && e.ShiftKey && _tableStart is not null)
            {
                var s = _tableStart.Value;
                if (day < s)
                {
                    _tableStart = day;
                    _tableEnd = s;
                }
                else
                {
                    _tableEnd = day;
                }
                _selectingRange = false;
            }
            else if (_selectingRange && _tableStart is not null)
            {
                var s = _tableStart.Value;
                if (day < s)
                {
                    _tableStart = day;
                    _tableEnd = s;
                }
                else
                {
                    _tableEnd = day;
                }
                _selectingRange = false;
            }
            else
            {
                _tableStart = day;
                _tableEnd = day;
                _selected = day;
                _selectingRange = true;
            }

            _anchor = day.ToDateTime(TimeOnly.MinValue);
            await SearchAsync();
        }
        else
        {
            await JumpAsync(day);
        }
    }

    /// <summary>단축키. 표 보기에서는 받지 않는다 — 표의 글자 칸에서 친 글쇠가 여기로 올라와 달이 넘어간다.</summary>
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

    /// <summary>작은 달력의 마흔두 칸이 걸치는 시작일과 종료일.</summary>
    private (DateOnly Start, DateOnly End) MiniSpan
    {
        get
        {
            var first = DateOnly.FromDateTime(MiniMonth);
            var start = first.AddDays(-(int)first.DayOfWeek);
            return (start, start.AddDays(41));
        }
    }

    /// <summary>작은 달력의 마흔두 칸.</summary>
    private IEnumerable<DateOnly> MiniDays
    {
        get
        {
            var (start, _) = MiniSpan;

            for (var i = 0; i < 42; i++)
            {
                yield return start.AddDays(i);
            }
        }
    }

    /// <summary>작은 달력에 표시할 날짜별 일정 갯수.</summary>
    private Dictionary<DateOnly, int> _miniCounts = new();

    private int MiniCount(DateOnly day) => _miniCounts.GetValueOrDefault(day, 0);

    private string MiniDayTitle(DateOnly day, int count)
    {
        var dateStr = $"{day.Year}년 {day.Month}월 {day.Day}일 ({DowName(day.DayOfWeek)})";
        return count > 0 ? $"{dateStr} · 일정 {count}건" : dateStr;
    }

    private async Task StepMiniMonthAsync(int direction)
    {
        _miniOffset += direction;
        await RefreshMiniAsync();
    }

    /// <summary>작은 달력의 날짜별 일정 갯수를 계산한다. 현재 큰 달력의 월과 일치하면 읽어 둔 줄을 재사용하고, 다르면 해당 구간을 읽어 온다.</summary>
    private async Task RefreshMiniAsync()
    {
        var (miniStart, miniEnd) = MiniSpan;
        var (gridStart, gridEnd) = (DateOnly.FromDateTime(GridSpan.Start), DateOnly.FromDateTime(GridSpan.End));

        if (_miniOffset == 0 && _view == CalView.Month && _rows.Count >= 0 && gridStart <= miniStart && gridEnd >= miniEnd)
        {
            BuildMiniCounts(_rows);
            return;
        }

        try
        {
            var rows = await Api.ListAsync(ProjectRid, _completeState, _scheduleTypes, miniStart, miniEnd);
            BuildMiniCounts(rows);
        }
        catch
        {
            _miniCounts = new();
        }
    }

    private void BuildMiniCounts(IEnumerable<WbsItemDto> rows)
    {
        var counts = new Dictionary<DateOnly, int>();
        var (miniStart, miniEnd) = MiniSpan;

        foreach (var w in rows)
        {
            if (Span(w) is not { } span)
            {
                continue;
            }

            var from = span.Start < miniStart ? miniStart : span.Start;
            var to = span.End > miniEnd ? miniEnd : span.End;

            for (var d = from; d <= to; d = d.AddDays(1))
            {
                counts[d] = counts.TryGetValue(d, out var c) ? c + 1 : 1;
            }
        }

        _miniCounts = counts;
    }

    private string MiniDayCss(DateOnly day)
    {
        var css = new List<string>(5) { "pm-gc-mini__day" };

        if (day.Month != MiniMonth.Month || day.Year != MiniMonth.Year)
        {
            css.Add("pm-gc-mini__day--out");
        }

        if (day == Today)
        {
            css.Add("pm-gc-mini__day--today");
        }

        if (_view == CalView.Table)
        {
            if (_tableStart is not null && _tableEnd is not null)
            {
                var s = _tableStart <= _tableEnd ? _tableStart.Value : _tableEnd.Value;
                var e = _tableStart <= _tableEnd ? _tableEnd.Value : _tableStart.Value;

                if (day == s || day == e)
                {
                    css.Add("pm-gc-mini__day--selected");
                }
                else if (day > s && day < e)
                {
                    css.Add("pm-gc-mini__day--range-mid");
                }
            }
            else if (_tableStart is not null && day == _tableStart.Value)
            {
                css.Add("pm-gc-mini__day--selected");
            }
        }
        else
        {
            // 지금 보고 있는 구간에 드는 날은 옅게 깔아, 큰 달력이 보는 곳이 작은 달력에 비친다.
            if (day >= DateOnly.FromDateTime(Range.Start) && day <= DateOnly.FromDateTime(Range.End))
            {
                css.Add("pm-gc-mini__day--in");
            }

            if (day == _selected)
            {
                css.Add("pm-gc-mini__day--selected");
            }
        }

        if (MiniCount(day) > 0)
        {
            css.Add("pm-gc-mini__day--has-events");
        }

        return string.Join(' ', css);
    }

    // ── 달력 재료 ────────────────────────────────────────────────

    /// <summary>읽어 온 줄을 달력 약속으로 옮긴다. 시각이 있으면 시간 격자에, 없으면 종일 일정으로 그린다.</summary>
    private void BuildCalendar()
    {
        var today = Today;

        _items = [.. _rows
            .Select(w => (Row: w, Start: w.PlanSdt ?? w.PlanEdt, End: w.PlanEdt ?? w.PlanSdt))
            .Where(x => x.Start is not null)
            .Select(x =>
            {
                var start = x.Start!.Value;
                var end = x.End!.Value;
                if (end < start) end = start;

                var isAllDay = start.TimeOfDay == TimeSpan.Zero && end.TimeOfDay == TimeSpan.Zero;
                var schedEnd = isAllDay
                    ? end.Date.AddDays(1).AddSeconds(-1)
                    : (end == start ? start.AddHours(1) : end);

                return new CalendarItem
                {
                    Id = x.Row.WbsId,
                    Subject = Title(x.Row),
                    Description = Describe(x.Row),
                    Start = start,
                    End = schedEnd,
                    AllDay = isAllDay,
                    LabelId = LabelOf(x.Row, today),
                };
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

        if (!string.IsNullOrWhiteSpace(w.ReqUser)) parts.Add($"요청 {w.ReqUser}");
        if (!string.IsNullOrWhiteSpace(w.DevUser)) parts.Add($"담당 {w.DevUser}");
        if (!string.IsNullOrWhiteSpace(w.ProcTp)) parts.Add(w.ProcTp);
        if (!string.IsNullOrWhiteSpace(w.Comm)) parts.Add(w.Comm);

        return string.Join(" · ", parts);
    }

    /// <summary>색. 서버 상태는 대기·진행·완료 셋이고, 지연은 여기서 센다 — 계획 종료일이 지났는데 안 끝난 것.</summary>
    private static string LabelOf(WbsItemDto w, DateOnly today) => w.WbsState switch
    {
        "COMP" => "COMP",
        _ when w.PlanEdt is not null && DateOnly.FromDateTime(w.PlanEdt.Value) < today => "OVER",
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

    /// <summary>쓸기로 칠 최소 거리(px). 작게 두면 칸을 누르는 손짓이 쓸기로 새어 달이 넘어간다.</summary>
    private const double SwipeMin = 48;

    private static readonly DayOfWeek[] Weekdays =
    [
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
    ];

    /// <summary>요일 이름. 문화권을 타지 않게 직접 적는다 — 서버 CurrentCulture 에 따라 머리가 바뀌면 안 된다.</summary>
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

    /// <summary>격자를 다시 짠다. 읽어 온 줄을 날짜별로 갈라 두는 것이 핵심이다 — 그려질 때마다 세면 달마다 마흔두 번 훑는다. 걸치는 날은 구간 안에서만 돈다(몇 해짜리 줄이 있어서).</summary>
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

    /// <summary>주 한 줄씩 막대를 자리(lane)에 나눠 담는다. 먼저 시작한 것, 같이 시작했으면 긴 것이 위에 선다. 넘치는 것은 여기서 자르지 않는다 — 몇 줄까지 보일지는 화면 폭에 달려 그리는 때 갈린다.</summary>
    private void BuildWeeks()
    {
        var weeks = new List<WeekRow>();

        for (var i = 0; i + 7 <= _days.Count; i += 7)
        {
            var days = _days.Skip(i).Take(7).ToList();
            var from = days[0].Date;
            var to = days[6].Date;

            // 위에 설 차례 — 먼저 시작한 것, 같이 시작하면 긴 것. 자리 계산은 CalendarLanes 가 한다.
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

    /// <summary>이 날이 보고 있는 달의 날인가. 아니면 흐리게 그린다. 주·일 보기에는 자투리가 없어 전부 참이다.</summary>
    private bool InAnchorMonth(DateOnly day) =>
        Preset != DateRangePreset.Month
        || (day.Year == _anchor.Year && day.Month == _anchor.Month);

    /// <summary>고른 날을 보고 있는 구간 안으로 붙든다. 재는 자가 둘이다 — 「그냥 둘까」는 격자(GridSpan)로, 「데려갈까」는 달(Range)로 잰다. 섞어 재면 자투리 칸을 누를 때 고른 날이 1일로 튀거나 「등록」 기본 날이 지난달이 된다.</summary>
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

    /// <summary>칸의 빈 자리를 눌렀다. 날짜의 빈 영역 클릭 시에는 추가창을 띄우지 않고 그 날을 고르기만 한다.</summary>
    private Task OnDayClickAsync(DateOnly day)
    {
        _selected = day;
        return Task.CompletedTask;
    }

    /// <summary>날짜 숫자를 눌렀다. 구글처럼 그 날 보기로 들어가며, 휴대폰은 보기를 바꾸지 않고 그 날을 편다.</summary>
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

    /// <summary>막대를 칸 위에 얹는다. grid-column 으로 칸을 가로지른다.</summary>
    private static string BarStyle(Bar bar) =>
        $"grid-column: {bar.Col + 1} / span {bar.Span}; grid-row: {bar.Lane + 1};";

    private string MoreStyle(int col) =>
        $"grid-column: {col + 1}; grid-row: {MaxLanes + 1};";

    /// <summary>막대의 꼴. 주를 넘어가는 쪽은 모서리를 펴 둔다 — 잘린 끝이 둥글면 거기서 끝난 것으로 보인다.</summary>
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

    /// <summary>칸을 읽어 주는 말. 낭독기에는 날짜와 건수를 준다(막대의 글자는 잘려 있다).</summary>
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

    private static string Period(WbsItemDto w)
    {
        if (w.PlanSdt is null && w.PlanEdt is null)
        {
            return "기간 없음";
        }

        var start = w.PlanSdt ?? w.PlanEdt!.Value;
        var end = w.PlanEdt ?? start;
        if (end < start) end = start;

        var hasTime = start.TimeOfDay != TimeSpan.Zero || end.TimeOfDay != TimeSpan.Zero;
        if (!hasTime)
        {
            return start.Date == end.Date
                ? $"{start:MM.dd}"
                : $"{start:MM.dd} ~ {end:MM.dd}";
        }

        return start.Date == end.Date
            ? $"{start:MM.dd HH:mm} ~ {end:HH:mm}"
            : $"{start:MM.dd HH:mm} ~ {end:MM.dd HH:mm}";
    }

    private static string Label(WbsItemDto w) => LabelOf(w, Today);

    /// <summary>한 줄이 걸치는 날. 한쪽만 적힌 줄은 그날 하루로 본다. 거꾸로 들어간 줄은 여기서 바로잡는다 — 달력과 격자가 같은 자를 써야 같은 날에 뜬다.</summary>
    private static (DateOnly Start, DateOnly End)? Span(WbsItemDto w)
    {
        if (w.PlanSdt is null && w.PlanEdt is null)
        {
            return null;
        }

        var start = w.PlanSdt ?? w.PlanEdt!.Value;
        var end = w.PlanEdt ?? start;

        var startDate = DateOnly.FromDateTime(start);
        var endDate = DateOnly.FromDateTime(end);

        return (startDate, endDate < startDate ? startDate : endDate);
    }

    // ── 끌어 옮기기 (월 격자) ────────────────────────────────────

    /// <summary>막대를 다른 날에 놓았다. 기간과 시간은 그대로 두고 통째로 옮기며, 놓은 날이 시작일이 된다(집은 자리는 브라우저가 알려 주지 않는다).</summary>
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
        var daysDiff = day.DayNumber - range.Start.DayNumber;
        if (next.PlanSdt.HasValue) next.PlanSdt = next.PlanSdt.Value.AddDays(daysDiff);
        if (next.PlanEdt.HasValue) next.PlanEdt = next.PlanEdt.Value.AddDays(daysDiff);

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

    /// <summary>좌우로 쓸면 앞뒤 구간으로 간다. 세로로 더 많이 움직였으면 아무 일도 하지 않는다 — 목록을 보려고 내릴 때마다 달이 바뀌지 않게.</summary>
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

    /// <summary>부품이 주는 편집 창을 취소하고 우리 창을 연다. 빈 칸 클릭은 무시하고, 일정 클릭 시 그 줄을 고치러 연다.</summary>
    private void OnFormShowing(SchedulerAppointmentFormEventArgs e)
    {
        e.Cancel = true;

        var appointment = e.Appointment;

        if (appointment.IsNew || appointment.Id is null)
        {
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
            PlanSdt = from.ToDateTime(TimeOnly.MinValue),
            PlanEdt = (to < from ? from : to).ToDateTime(TimeOnly.MinValue),
            DevUser = CurrentUserName,
        };

        _complete = false;
        _isNew = true;
        _peeking = false;
        _editing = true;
    }

    /// <summary>편집 창에 복사본을 띄운다. 목록 줄을 그대로 묶으면 취소해도 화면에 바뀐 값이 남는다.</summary>
    private void StartEdit(WbsItemDto w)
    {
        _form = Copy(w);
        _complete = w.DevEdt is not null;
        _isNew = false;
        _peeking = false;
        _editing = true;
    }

    /// <summary>새 줄에 붙일 구분. 하나만 고른 때만 따른다 — 여럿이면 어디에 넣을지 알 수 없고, 임의로 첫째를 집으면 고른 적 없는 구분에 조용히 들어간다.</summary>
    private string NewKind =>
        _scheduleTypes.Count == 1 ? _scheduleTypes[0] : DefaultKind;

    /// <summary>완료 표시를 값으로 옮긴다(원본 OnSubmit 의 규칙). 상태는 서버가 개발 시작·종료일로 정하므로 바꾸는 것은 그 두 날이다. 표시를 끄면 개발 종료일도 지운다 — 안 지우면 상태가 완료에 눌어붙는다.</summary>
    private void ApplyComplete()
    {
        var today = AppTime.TodayDate;

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
            var today = AppTime.TodayDate;
            var diff = _form.PlanEdt.HasValue && _form.PlanSdt.HasValue
                ? _form.PlanEdt.Value - _form.PlanSdt.Value
                : TimeSpan.Zero;
            var startTime = _form.PlanSdt?.TimeOfDay ?? TimeSpan.Zero;
            _form.PlanSdt = today.Add(startTime);
            _form.PlanEdt = _form.PlanSdt.Value.Add(diff);
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

    /// <summary>끌어 옮기거나 크기를 바꾼 것. 계획 날짜·시간이 바뀌고 나머지 칸은 읽어 둔 줄에서 그대로 가져간다.</summary>
    private async Task OnUpdatingAsync(SchedulerAppointmentOperationEventArgs e)
    {
        var row = Find(e.Appointment.Id);

        if (row is null)
        {
            return;
        }

        var next = Copy(row);
        if (e.Appointment.AllDay)
        {
            next.PlanSdt = Day(e.Appointment.Start).ToDateTime(TimeOnly.MinValue);
            next.PlanEdt = LastDay(e.Appointment).ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            next.PlanSdt = e.Appointment.Start;
            next.PlanEdt = e.Appointment.End;
        }

        if (next.PlanSdt == row.PlanSdt && next.PlanEdt == row.PlanEdt)
        {
            return;
        }

        // 부품이 자기 저장소를 먼저 고쳤다. 실패했으면 다시 읽어 되돌린다.
        await RunAsync(() => Api.UpdateAsync(next), "옮겼습니다.", "옮기지 못했습니다");
        await SearchAsync();
    }

    /// <summary>달력에서 지운 것. 부품이 지우기 전에 막고(Cancel) 먼저 묻는다 — 「아니오」인데 화면에서만 사라지면 안 된다.</summary>
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

    /// <summary>새 일정은 고른 프로젝트와 구분에 붙는다. 구분을 비우면 어느 화면에서도 안 보이는 줄이 된다.</summary>
    private void FillNew(WbsItemDto w)
    {
        w.PrjRid = ProjectRid;
        w.ScheduleType = NewKind;

        // 고른 기간의 첫날을 기본으로 둔다 — 아니면 만든 줄이 보고 있는 구간 밖으로 떨어져 저장 직후 사라진다.
        w.PlanSdt = _view == CalView.Table && _tableStart is not null
            ? _tableStart.Value.ToDateTime(TimeOnly.MinValue)
            : Range.Start;
        w.PlanEdt = _view == CalView.Table && _tableEnd is not null
            ? _tableEnd.Value.ToDateTime(TimeOnly.MinValue)
            : w.PlanSdt;
        w.DevUser = CurrentUserName;
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

    // ── 표 보기 인라인 즉시 수정 저장 ──────────────────────────────

    private async Task EnsureDropdownDataAsync()
    {
        if (_scheduleTypeCodes.Count == 0)
        {
            _scheduleTypeCodes = await Codes.GetAsync("schedule_type");
        }

        if (_userOptions.Count == 0)
        {
            _userOptions = await Biz.GetAsync("portal_account");
        }
    }

    private static DateTime? ToDateTime(DateOnly? d) =>
        d?.ToDateTime(TimeOnly.MinValue);

    private string GetScheduleTypeName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var item = _scheduleTypeCodes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        return item?.Name ?? code;
    }

    private string GetUserDisplayName(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return string.Empty;
        var opt = _userOptions.FirstOrDefault(o => string.Equals(o.Value, username, StringComparison.OrdinalIgnoreCase));
        return opt?.Label ?? username;
    }

    private static string GetInlineStateCode(WbsItemDto w)
    {
        if (w.DevEdt is not null || w.WbsState == "COMP") return "COMP";
        if (w.DevSdt is not null || w.WbsState == "RUNNING") return "RUNNING";
        return "READY";
    }

    /// <summary>표 보기에서 행 값을 변경했을 때 즉시 서버에 저장한다.</summary>
    private async Task SaveInlineAsync(WbsItemDto w)
    {
        if (w.WbsId == 0) return;

        try
        {
            var updated = await Api.UpdateAsync(w);
            if (updated is not null)
            {
                w.PlanGap = updated.PlanGap;
                w.WbsState = updated.WbsState;
                w.ModUser = updated.ModUser;
                w.ModDt = updated.ModDt;
            }

            BuildCalendar();
            BuildDays();
            BuildWeeks();
            StateHasChanged();
            Say("저장했습니다.", NoticeTone.Info);
        }
        catch (Exception ex)
        {
            Say($"저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    private async Task OnInlinePlanSdtChangedAsync(WbsItemDto w, DateTime? d)
    {
        if (w.PlanSdt == d) return;

        w.PlanSdt = d;
        if (w.PlanEdt is not null && w.PlanSdt is not null && w.PlanEdt < w.PlanSdt)
        {
            w.PlanEdt = w.PlanSdt;
        }

        if (w.PlanSdt is not null && w.PlanEdt is not null)
        {
            w.PlanGap = (DateOnly.FromDateTime(w.PlanEdt.Value).DayNumber - DateOnly.FromDateTime(w.PlanSdt.Value).DayNumber) + 1;
        }

        await SaveInlineAsync(w);
    }

    private async Task OnInlinePlanEdtChangedAsync(WbsItemDto w, DateTime? d)
    {
        if (w.PlanEdt == d) return;

        w.PlanEdt = d;
        if (w.PlanSdt is not null && w.PlanEdt is not null && w.PlanEdt < w.PlanSdt)
        {
            w.PlanSdt = w.PlanEdt;
        }

        if (w.PlanSdt is not null && w.PlanEdt is not null)
        {
            w.PlanGap = (DateOnly.FromDateTime(w.PlanEdt.Value).DayNumber - DateOnly.FromDateTime(w.PlanSdt.Value).DayNumber) + 1;
        }

        await SaveInlineAsync(w);
    }

    private async Task OnInlineProcNmChangedAsync(WbsItemDto w, string? val)
    {
        if (string.Equals(w.ProcNm, val, StringComparison.Ordinal)) return;
        if (string.IsNullOrWhiteSpace(val))
        {
            Say("일정 이름을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        w.ProcNm = val.Trim();
        await SaveInlineAsync(w);
    }

    private async Task OnInlineScheduleTypeChangedAsync(WbsItemDto w, string? val)
    {
        if (string.Equals(w.ScheduleType, val, StringComparison.Ordinal)) return;

        w.ScheduleType = string.IsNullOrWhiteSpace(val) ? DefaultKind : val;
        await SaveInlineAsync(w);
    }

    private async Task OnInlineProcTpChangedAsync(WbsItemDto w, string? val)
    {
        if (string.Equals(w.ProcTp, val, StringComparison.Ordinal)) return;

        w.ProcTp = val;
        await SaveInlineAsync(w);
    }

    private async Task OnInlineReqUserChangedAsync(WbsItemDto w, string? val)
    {
        if (string.Equals(w.ReqUser, val, StringComparison.Ordinal)) return;

        w.ReqUser = val;
        await SaveInlineAsync(w);
    }

    private async Task OnInlineDevUserChangedAsync(WbsItemDto w, string? val)
    {
        if (string.Equals(w.DevUser, val, StringComparison.Ordinal)) return;

        w.DevUser = val ?? string.Empty;
        await SaveInlineAsync(w);
    }

    private async Task OnInlineStateChangedAsync(WbsItemDto w, string? val)
    {
        var current = GetInlineStateCode(w);
        if (string.Equals(current, val, StringComparison.Ordinal)) return;

        var today = AppTime.TodayDate;
        switch (val)
        {
            case "COMP":
                w.DevSdt ??= today;
                w.DevEdt = today;
                if (w.PlanSdt > w.DevEdt) w.PlanSdt = w.DevEdt;
                if (w.PlanEdt > w.DevEdt) w.PlanEdt = w.DevEdt;
                w.WbsState = "COMP";
                break;
            case "RUNNING":
                w.DevSdt ??= today;
                w.DevEdt = null;
                w.WbsState = "RUNNING";
                break;
            default: // READY
                w.DevSdt = null;
                w.DevEdt = null;
                w.WbsState = "READY";
                break;
        }

        await SaveInlineAsync(w);
    }

    private async Task OnInlineCommChangedAsync(WbsItemDto w, string? val)
    {
        if (string.Equals(w.Comm, val, StringComparison.Ordinal)) return;

        w.Comm = val;
        await SaveInlineAsync(w);
    }

    private async Task OnInlineBodyTextChangedAsync(WbsItemDto w, string? val)
    {
        if (string.Equals(w.BodyText, val, StringComparison.Ordinal)) return;

        w.BodyText = val;
        await SaveInlineAsync(w);
    }

    // ── 도우미 ───────────────────────────────────────────────────

    private WbsItemDto? Find(object? id) =>
        int.TryParse(id?.ToString(), out var wbsId)
            ? _rows.FirstOrDefault(w => w.WbsId == wbsId)
            : null;

    private static DateOnly Day(DateTime value) => DateOnly.FromDateTime(value);

    /// <summary>약속의 마지막 날. 끝이 자정이면 그 앞날이다 — 자정을 그대로 받으면 하루가 늘어난다.</summary>
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
        ReqUser = w.ReqUser,
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
        Summary = w.Summary,
        BodyText = w.BodyText,
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

    /// <summary>격자의 한 칸.</summary>
    /// <param name="Date">그 날</param>
    /// <param name="InMonth">보고 있는 달의 날인가 — 아니면 흐리게 그린다</param>
    /// <param name="Items">그 날에 걸치는 일정. <b>걸치기만 해도 들어간다</b></param>
    private sealed record CalDay(DateOnly Date, bool InMonth, IReadOnlyList<WbsItemDto> Items);

    /// <summary>주 안에 놓인 막대 하나.</summary>
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
