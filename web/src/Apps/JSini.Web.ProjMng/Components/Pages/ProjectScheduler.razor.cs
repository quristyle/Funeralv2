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

public partial class ProjectScheduler : IAsyncDisposable
{
    [Inject] private WbsClient Api { get; set; } = default!;

    [Inject] private CommonCodes Codes { get; set; } = default!;

    [Inject] private BizOptions Biz { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Inject] private CurrentUser Me { get; set; } = default!;

    /// <summary>마지막에 고른 프로젝트를 브라우저에 적어 둘 때 쓰는 열쇠 앞부분. 뒤에 로그인 아이디가 붙는다.</summary>
    internal const string ProjectStorageKeyPrefix = "jsini-sched-prj:";

    /// <summary>「전체」를 골라 둔 것을 적어 두는 값. 빈 글자는 「기억한 것 없음」과 갈리지 않는다.</summary>
    internal const string AllProjectsMark = "*";

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
            : null,
        _view == CalView.Table && !string.IsNullOrWhiteSpace(_keyword) ? $"「{_keyword.Trim()}」" : null);

    /// <summary>고른 값의 이름들. 코드만 들고, 목록은 CodeSelect 가 쥐고 있어 부품이 @bind-Text 로 올려 준다.</summary>
    private string? _projectName;

    private string? _kindNames;

    private string? _completeStateName;

    /// <summary>표 형태일 때 조회할 시작일.</summary>
    private DateOnly? _tableStart;

    /// <summary>표 형태일 때 조회할 종료일.</summary>
    private DateOnly? _tableEnd;

    /// <summary>작은 달력에서 끌기 시작한 날. 누르고 있는 동안만 값이 있다.</summary>
    private DateOnly? _miniDragFrom;

    /// <summary>끄는 동안 손이 지금 올라가 있는 날. 놓으면 여기까지가 구간이다.</summary>
    private DateOnly? _miniDragTo;

    /// <summary>표 보기의 내용 검색어. 제목·메모·원문 어디에든 들어 있으면 남긴다.</summary>
    private string? _keyword;

    /// <summary>표에 까는 줄. 달력 보기와 작은 달력 건수는 거르지 않은 <see cref="_rows"/> 를 그대로 쓴다.
    /// 거른 결과를 들고 있는다 — 그릴 때마다 새 목록을 주면 표가 자료가 바뀐 것으로 읽고 고른 줄을 다시 잡는다.</summary>
    private IReadOnlyList<WbsItemDto> TableRows
    {
        get
        {
            var key = _keyword?.Trim();

            if (string.IsNullOrEmpty(key))
            {
                return _rows;
            }

            if (!ReferenceEquals(_filteredFrom, _rows) || _filteredKey != key)
            {
                _filteredFrom = _rows;
                _filteredKey = key;
                _filtered = [.. _rows.Where(w => Has(w.ProcNm, key) || Has(w.Comm, key) || Has(w.BodyText, key))];
            }

            return _filtered;

            static bool Has(string? text, string key) =>
                text is not null && text.Contains(key, StringComparison.OrdinalIgnoreCase);
        }
    }

    private IReadOnlyList<WbsItemDto>? _filteredFrom;
    private string? _filteredKey;
    private IReadOnlyList<WbsItemDto> _filtered = [];

    /// <summary>요청자·담당자 칸 필터의 고르개 한 줄. 값은 칸에 적힌 그대로, 글자는 사람이 읽는 이름이다.</summary>
    public sealed record UserFilterOption(string Value, string Text);

    private IReadOnlyList<WbsItemDto>? _userFilterFrom;
    private IReadOnlyList<UserFilterOption> _reqUserFilterOptions = [];
    private IReadOnlyList<UserFilterOption> _devUserFilterOptions = [];

    /// <summary>표에 깔린 줄에서 요청자 값을 모은다.</summary>
    private IReadOnlyList<UserFilterOption> ReqUserFilterOptions
    {
        get { RefreshUserFilterOptions(); return _reqUserFilterOptions; }
    }

    /// <summary>표에 깔린 줄에서 담당자 값을 모은다. 계정이면 이름으로 보인다.</summary>
    private IReadOnlyList<UserFilterOption> DevUserFilterOptions
    {
        get { RefreshUserFilterOptions(); return _devUserFilterOptions; }
    }

    /// <summary>줄이 바뀔 때만 다시 모은다 — 그릴 때마다 새 목록을 주면 펼쳐 둔 고르개가 다시 그려진다.</summary>
    private void RefreshUserFilterOptions()
    {
        var rows = TableRows;

        if (ReferenceEquals(_userFilterFrom, rows))
        {
            return;
        }

        _userFilterFrom = rows;
        _reqUserFilterOptions = Collect(rows.Select(w => w.ReqUser), v => v);
        _devUserFilterOptions = Collect(rows.Select(w => w.DevUser), GetUserDisplayName);

        static IReadOnlyList<UserFilterOption> Collect(IEnumerable<string?> values, Func<string, string> text) =>
        [
            .. values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct(StringComparer.Ordinal)
                .Select(v => new UserFilterOption(v, text(v)))
                .OrderBy(o => o.Text, StringComparer.CurrentCulture),
        ];
    }

    /// <summary>칸에 걸린 기준에서 고른 값을 되읽는다. 값을 따로 들고 있지 않는다 — 표가 기준을 지우면 고르개도 함께 비어야 한다.</summary>
    private static IEnumerable<string> PickedFilterValues(GridDataColumnFilterRowCellTemplateContext f) => f.FilterCriteria switch
    {
        DevExpress.Data.Filtering.InOperator op =>
            [.. op.Operands.OfType<DevExpress.Data.Filtering.OperandValue>().Select(o => o.Value as string).OfType<string>()],
        DevExpress.Data.Filtering.BinaryOperator { RightOperand: DevExpress.Data.Filtering.OperandValue { Value: string v } } => [v],
        _ => [],
    };

    /// <summary>고른 값들을 「그중 하나와 같다」로 건다. 고른 것이 없으면 기준을 지운다.</summary>
    private static void SetPickedFilter(GridDataColumnFilterRowCellTemplateContext f, IEnumerable<string>? values)
    {
        var list = values?.ToArray() ?? [];

        f.FilterCriteria = list.Length == 0
            ? null
            : new DevExpress.Data.Filtering.InOperator(f.DataColumn.FieldName, list);
    }

    private DateTime? TableStartDateTime => _tableStart?.ToDateTime(TimeOnly.MinValue);
    private DateTime? TableEndDateTime => _tableEnd?.ToDateTime(TimeOnly.MinValue);

    private Task OnTableStartChangedAsync(DateTime? val)
    {
        _tableStart = val is { } dt ? DateOnly.FromDateTime(dt) : null;
        return SearchAsync();
    }

    private Task OnTableEndChangedAsync(DateTime? val)
    {
        _tableEnd = val is { } dt ? DateOnly.FromDateTime(dt) : null;
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

    /// <summary>표 보기에서 쓰는 추가대상 공통코드 목록.</summary>
    private IReadOnlyList<CommonCodeItem> _addTargetCodes = [];

    /// <summary>표 보기에서 쓰는 담당자(포털 계정) 목록.</summary>
    private IReadOnlyList<BizOption> _userOptions = [];

    /// <summary>진행 상태 고르개. 표의 칸과 편집 창이 같은 목록을 쓴다.</summary>
    private static IReadOnlyList<ScheduleState> StateOptions => ScheduleEditPopup.StateOptions;

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

    /// <summary>작은 달력이 보고 있는 달(1일). 큰 달력의 기준일과 따로 든다 — 기준일에서 몇 달 떨어졌나로 셈하면
    /// 작은 달력을 넘겨 둔 채 날을 누를 때 기준일이 옮겨 가면서 그 거리만큼 달이 한 번 더 튄다.</summary>
    private DateOnly _miniMonth = FirstOfMonth(AppTime.Today);

    /// <summary>월 격자 끌어 옮기기(gc-month-drag.js)가 놓을 때 부르는 창구.</summary>
    private DotNetObjectReference<ProjectScheduler>? _dragRef;

    private IJSObjectReference? _dragModule;

    /// <summary>화면 유지(keep-alive)로 일정표가 둘 살 때 끌기 모듈이 제 화면을 가려내는 열쇠. 격자의 data-drag-owner 에 적는다.</summary>
    private readonly string _dragOwner = Guid.NewGuid().ToString("N");

    /// <summary>쓸기 판정에 쓰는 손가락 시작점. 화면에 그려지는 값이 아니다.</summary>
    private double _touchX;
    private double _touchY;

    /// <summary>쓸어 넘길 때 밀려 나갔다 들어오는 판. 날 목록(표·목록)과 월 격자가 따로다.</summary>
    private ElementReference _dayTurnRef;
    private ElementReference _monthTurnRef;

    private IJSObjectReference? _turnModule;

    /// <summary>표 보기에서 고른 줄을 Delete 글쇠로 지운다(gc-table-delete.js). 끌기와 같은 창구(_dragRef)로 부른다.</summary>
    private ElementReference _mainRef;
    private IJSObjectReference? _deleteKeyModule;

    /// <summary>새 자료가 그려진 뒤 들어오게 할 방향. 0 이면 할 일이 없다.</summary>
    private int _turnInDir;
    private bool _turnInMonth;

    private ConfirmDialog? _confirm;

    /// <summary>월 보기 오른쪽의 「고른 날」 목록이 펴져 있나. 휴대폰은 격자 아래 목록이 늘 있어 쓰지 않는다.</summary>
    private bool _dayPane = true;

    private bool ShowDayPane => _dayPane && _view == CalView.Month && !_isPhone;

    /// <summary>등록·수정 창. 달력·목록·표가 모두 이것을 연다.</summary>
    private ScheduleEditPopup? _editPopup;

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

    /// <summary>프로젝트를 「전체」로 보고 있나 — 내가 참여한 프로젝트 전부의 일정이 섞여 온다. 그때만 줄마다 프로젝트를 적는다.</summary>
    private bool AllProjects => ProjectRid is null;

    /// <summary>프로젝트 번호 → 이름·색. projlist 차례로 색을 매겨 같은 프로젝트는 어느 보기에서나 같은 색이다.</summary>
    private Dictionary<int, (string Name, string Color)> _projects = new();

    /// <summary>프로젝트 구별 색. 상태 색(노랑·파랑·빨강·회색)과 겹치지 않는 것으로 골랐다.</summary>
    private static readonly string[] ProjectColors =
    [
        "#188038", "#9334e6", "#e8710a", "#007b83", "#c5221f",
        "#3949ab", "#b06000", "#d01884", "#5f6368", "#0b8043",
    ];

    private string? ProjectName(WbsItemDto w) =>
        w.PrjRid is { } rid
            ? _projects.TryGetValue(rid, out var p) ? p.Name : $"프로젝트 {rid}"
            : null;

    private string ProjectColor(WbsItemDto w) =>
        w.PrjRid is { } rid && _projects.TryGetValue(rid, out var p) ? p.Color : "#5f6368";

    /// <summary>「전체」일 때만 붙는 프로젝트 이름. 하나를 골랐으면 모든 줄이 같은 말이라 적지 않는다.</summary>
    private string? ProjectTag(WbsItemDto w) => AllProjects ? ProjectName(w) : null;

    private async Task LoadProjectsAsync()
    {
        var list = await Codes.GetAsync("projlist");
        var map = new Dictionary<int, (string, string)>();

        foreach (var item in list)
        {
            if (int.TryParse(item.Code, out var rid) && !map.ContainsKey(rid))
            {
                map[rid] = (item.Name, ProjectColors[map.Count % ProjectColors.Length]);
            }
        }

        _projects = map;
    }

    private string Hint => $"{TableRows.Count}건";

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

    private string AgendaTitle =>
        $"{_selected.Month}월 {_selected.Day}일 ({DowName(_selected.DayOfWeek)})";

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

        if (firstRender)
        {
            await AttachDragAsync();
            await AttachDeleteKeyAsync();
        }

        if (_turnInDir != 0)
        {
            var dir = _turnInDir;
            _turnInDir = 0;
            await TurnAsync("turnIn", _turnInMonth ? _monthTurnRef : _dayTurnRef, dir);
        }

        if (!firstRender || _projectRestored) return;

        await EnsureDropdownDataAsync();

        var remembered = await ReadRememberedProjectAsync();
        var known = false;

        await LoadProjectsAsync();

        if (remembered == AllProjectsMark)
        {
            known = true;
            _projectCode = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(remembered))
        {
            var list = await Codes.GetAsync("projlist");
            known = list.Any(i => string.Equals(i.Code, remembered, StringComparison.Ordinal));

            if (known)
            {
                _projectCode = remembered;
            }
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
        // 지우기 단추로 비우면 「전체」로 돌린다 — 빈 칸으로 두면 무엇을 보고 있는지 칸이 말하지 않는다.
        if (string.IsNullOrEmpty(item?.Code))
        {
            _projectCode = string.Empty;
            _projectName = "전체";
        }

        _ = RememberProjectAsync(string.IsNullOrEmpty(item?.Code) ? AllProjectsMark : item.Code);
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

            // 작은 달력의 건수가 따로 읽어야 하는 구간이면 본 조회와 함께 띄운다 — 차례로 기다리면 두 번 왕복한 만큼 늦다.
            var mini = MiniReusesRows ? null : RefreshMiniAsync();

            // 표가 받은 차례 그대로 그리므로 날짜순으로 맞춰 둔다. 칸에 정렬을 걸면 고친 줄이 편집 중에 자리를 옮긴다.
            _rows = [.. (await Api.ListAsync(
                    ProjectRid, _completeState, _scheduleTypes,
                    from, to, mine: AllProjects))
                .OrderBy(w => w.PlanSdt ?? DateTime.MaxValue)
                .ThenBy(w => w.PlanEdt ?? DateTime.MaxValue)
                .ThenBy(w => w.WbsId)];

            SyncSelected();

            // 표 보기에서는 달력 재료를 짜지 않는다 — 그려지지 않는 것을 줄마다 날짜별로 가르느라 표가 늦게 뜬다.
            // 달력으로 돌아갈 때 SetViewAsync 가 다시 읽으며 짠다.
            if (_view == CalView.Table)
            {
                _editRow = _editRow is null ? null : _rows.FirstOrDefault(w => w.WbsId == _editRow.WbsId);
            }
            else
            {
                BuildCalendar();
                BuildDays();
                BuildWeeks();
            }

            await (mini ?? RefreshMiniAsync());

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
        SyncMiniMonth();

        if (_view == CalView.Table)
        {
            // 구간과 빈 목록을 첫 await 앞에서 정한다. 뒤로 밀면 그 사이 한 번 그려질 때 달 보기에서 읽어 둔
            // 줄(격자 6주치)이 표로 먼저 깔린다 — 고른 날이 아니라 「전체」를 읽은 것처럼 보이고 그동안 화면이 굳는다.
            _tableStart ??= _selected;
            _tableEnd ??= _selected;
            _rows = [];
            _editRow = null;

            await EnsureDropdownDataAsync();
        }

        await SearchAsync();
    }

    private Task GoTodayAsync()
    {
        _anchor = AppTime.TodayDate;
        _selected = Today;
        SyncMiniMonth();

        if (_view == CalView.Table)
        {
            _tableStart = Today;
            _tableEnd = Today;
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
            SyncMiniMonth();
            return SearchAsync();
        }

        _anchor = Preset switch
        {
            DateRangePreset.Day => _anchor.AddDays(direction),
            DateRangePreset.Week => _anchor.AddDays(direction * 7),
            _ => _anchor.AddMonths(direction),
        };

        SyncMiniMonth();

        return SearchAsync();
    }

    /// <summary>그 날로 간다. 작은 달력과 날짜 누르기가 함께 쓴다 — 보기는 그대로 두고 기준일만 옮긴다.</summary>
    private Task JumpAsync(DateOnly day)
    {
        _anchor = day.ToDateTime(TimeOnly.MinValue);
        _selected = day;
        SyncMiniMonth();

        if (_view == CalView.Table)
        {
            _tableStart = day;
            _tableEnd = day;
        }

        return SearchAsync();
    }

    // ── 작은 달력 누르기 ─────────────────────────────────────────
    // 한 번 누르면 그 하루, 끌거나 Shift 를 누른 채 누르면 구간이다. 「첫 클릭은 시작, 다음 클릭은 종료」로 두면
    // 다른 날 하루를 보려던 다음 클릭이 말없이 종료일이 되어, 누른 적 없는 구간이 선다.

    /// <summary>작은 달력의 날을 눌렀다(왼쪽 단추). Shift 면 그 자리에서 구간을 맺고, 아니면 끌기를 시작한다.</summary>
    private Task OnMiniDayDownAsync(DateOnly day, MouseEventArgs e)
    {
        if (e.Button != 0)
        {
            return Task.CompletedTask;
        }

        if (e.ShiftKey)
        {
            _miniDragFrom = null;
            _miniDragTo = null;
            return PickMiniRangeAsync(RangeOrigin, day);
        }

        _miniDragFrom = day;
        _miniDragTo = day;
        return Task.CompletedTask;
    }

    /// <summary>끄는 중에 다른 날로 들어왔다. 놓기 전까지 구간을 미리 비춘다.</summary>
    private void OnMiniDayEnter(DateOnly day)
    {
        if (_miniDragFrom is not null)
        {
            _miniDragTo = day;
        }
    }

    /// <summary>손을 뗐다. 격자에 건다 — 칸 사이 틈에서 떼어도 마지막으로 지난 날까지로 맺는다.</summary>
    private Task OnMiniGridUpAsync()
    {
        if (_miniDragFrom is not { } from)
        {
            return Task.CompletedTask;
        }

        var to = _miniDragTo ?? from;
        _miniDragFrom = null;
        _miniDragTo = null;

        return from == to ? PickMiniDayAsync(from) : PickMiniRangeAsync(from, to);
    }

    /// <summary>누른 채로 격자를 벗어났다. 끌기를 거둔다 — 밖에서 뗀 손은 여기로 안 온다.</summary>
    private void OnMiniGridLeave()
    {
        _miniDragFrom = null;
        _miniDragTo = null;
    }

    /// <summary>글쇠(Enter·Space)로 눌렀다. 마우스 누름은 내려감·올라옴이 이미 받았으므로 여기서는 건너뛴다(Detail 이 0 이면 글쇠다).</summary>
    private Task OnMiniDayKeyClickAsync(DateOnly day, MouseEventArgs e)
    {
        if (e.Detail != 0)
        {
            return Task.CompletedTask;
        }

        return e.ShiftKey ? PickMiniRangeAsync(RangeOrigin, day) : PickMiniDayAsync(day);
    }

    /// <summary>Shift 로 구간을 맺을 때 반대쪽 끝. 표 보기면 지금 시작일, 달력 보기면 고른 날이다.</summary>
    private DateOnly RangeOrigin =>
        _view == CalView.Table && _tableStart is { } start ? start : _selected;

    /// <summary>하루를 골랐다. 표 보기는 그 하루를 조회하고, 달력 보기는 그 날로 가서 오른쪽에 그 날의 목록을 편다.
    /// 흐린 칸(앞뒤 달)을 누른 것은 그 달로 가겠다는 뜻이라 작은 달력도 따라 넘긴다.</summary>
    private Task PickMiniDayAsync(DateOnly day)
    {
        var month = FirstOfMonth(day);

        if (_view != CalView.Table)
        {
            _dayPane = true;
            return JumpAsync(day);
        }

        _tableStart = day;
        _tableEnd = day;
        _selected = day;
        _anchor = day.ToDateTime(TimeOnly.MinValue);
        _miniMonth = month;

        return SearchAsync();
    }

    /// <summary>구간을 골랐다. 구간을 펼 수 있는 것은 표 보기뿐이라 달력 보기에서 고르면 표로 넘어간다. 작은 달력의 달은 그대로 둔다.</summary>
    private async Task PickMiniRangeAsync(DateOnly a, DateOnly b)
    {
        var (start, end) = a <= b ? (a, b) : (b, a);
        var month = _miniMonth;

        _tableStart = start;
        _tableEnd = end;
        _selected = start;
        _anchor = start.ToDateTime(TimeOnly.MinValue);

        if (_view != CalView.Table)
        {
            _view = CalView.Table;
            _rows = [];
            _editRow = null;
            await EnsureDropdownDataAsync();
        }

        _miniMonth = month;
        await SearchAsync();
    }

    /// <summary>작은 달력을 큰 달력이 보는 달로 맞춘다.</summary>
    private void SyncMiniMonth() => _miniMonth = FirstOfMonth(DateOnly.FromDateTime(_anchor));

    private static DateOnly FirstOfMonth(DateOnly day) => new(day.Year, day.Month, 1);

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

    private DateOnly MiniMonth => _miniMonth;

    private string MiniTitle => $"{MiniMonth.Year}년 {MiniMonth.Month}월";

    /// <summary>작은 달력의 마흔두 칸이 걸치는 시작일과 종료일.</summary>
    private (DateOnly Start, DateOnly End) MiniSpan
    {
        get
        {
            var first = MiniMonth;
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
        _miniMonth = _miniMonth.AddMonths(direction);
        await RefreshMiniAsync();
    }

    /// <summary>작은 달력의 날짜별 일정 갯수를 계산한다. 현재 큰 달력의 월과 일치하면 읽어 둔 줄을 재사용하고, 다르면 해당 구간을 읽어 온다.</summary>
    private async Task RefreshMiniAsync()
    {
        var (miniStart, miniEnd) = MiniSpan;

        if (MiniReusesRows)
        {
            BuildMiniCounts(_rows);
            return;
        }

        try
        {
            var rows = await Api.ListAsync(ProjectRid, _completeState, _scheduleTypes, miniStart, miniEnd, mine: AllProjects);
            BuildMiniCounts(rows);
        }
        catch
        {
            _miniCounts = new();
        }
    }

    /// <summary>작은 달력이 큰 달력이 읽은 줄을 그대로 쓸 수 있는가 — 같은 달을 보는 월 보기이고 격자가 마흔두 칸을 다 덮을 때다.</summary>
    private bool MiniReusesRows
    {
        get
        {
            if (_miniMonth != FirstOfMonth(DateOnly.FromDateTime(_anchor)) || _view != CalView.Month)
            {
                return false;
            }

            var (miniStart, miniEnd) = MiniSpan;

            return DateOnly.FromDateTime(GridSpan.Start) <= miniStart
                && DateOnly.FromDateTime(GridSpan.End) >= miniEnd;
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

    /// <summary>작은 달력에 칠할 구간. 끄는 중이면 미리 보이는 구간, 표 보기면 조회 구간이고, 달력 보기는 없다(고른 하루만 칠한다).</summary>
    private (DateOnly Start, DateOnly End)? MiniRange
    {
        get
        {
            if (_miniDragFrom is { } from)
            {
                var to = _miniDragTo ?? from;
                return from <= to ? (from, to) : (to, from);
            }

            if (_view == CalView.Table && _tableStart is { } s && _tableEnd is { } e)
            {
                return s <= e ? (s, e) : (e, s);
            }

            if (_view == CalView.Table && (_tableStart ?? _tableEnd) is { } one)
            {
                return (one, one);
            }

            return null;
        }
    }

    /// <summary>작은 달력 아래에 적는 고른 구간(「10월 3일 (금) ~ 10월 10일 (금) · 8일」). 칠한 칸만으로는 어디서 시작해 어디서 끝나는지,
    /// 달을 넘어간 끝이 어디인지 안 보인다.</summary>
    private string? MiniRangeText
    {
        get
        {
            if (MiniRange is not { } r)
            {
                return null;
            }

            if (r.Start == r.End)
            {
                return DayText(r.Start);
            }

            var days = r.End.DayNumber - r.Start.DayNumber + 1;
            return $"{DayText(r.Start)} ~ {DayText(r.End)} · {days}일";

            string DayText(DateOnly d) =>
                (d.Year == Today.Year ? "" : $"{d.Year}년 ") + $"{d.Month}월 {d.Day}일 ({DowName(d.DayOfWeek)})";
        }
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

        if (MiniRange is { } range)
        {
            if (day == range.Start && day == range.End)
            {
                css.Add("pm-gc-mini__day--selected");
            }
            else if (day == range.Start)
            {
                css.Add("pm-gc-mini__day--selected");
                css.Add("pm-gc-mini__day--range-start");
            }
            else if (day == range.End)
            {
                css.Add("pm-gc-mini__day--selected");
                css.Add("pm-gc-mini__day--range-end");
            }
            else if (day > range.Start && day < range.End)
            {
                css.Add("pm-gc-mini__day--range-mid");
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
                    Subject = ProjectTag(x.Row) is { } prj ? $"[{prj}] {Title(x.Row)}" : Title(x.Row),
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

    /// <summary>색. 서버 상태는 대기·진행·완료·폐기 넷이고, 지연은 여기서 센다 — 계획 종료일이 지났는데 안 끝난 것.</summary>
    private static string LabelOf(WbsItemDto w, DateOnly today) => w.WbsState switch
    {
        _ when IsDiscarded(w) => "DISCARD",
        "COMP" => "COMP",
        _ when w.PlanEdt is not null && DateOnly.FromDateTime(w.PlanEdt.Value) < today => "OVER",
        "RUNNING" => "RUNNING",
        _ => "READY",
    };

    private static string StateName(WbsItemDto w) => LabelOf(w, Today) switch
    {
        "DISCARD" => "폐기",
        "COMP" => "완료",
        "OVER" => "지연",
        "RUNNING" => "진행중",
        _ => "대기",
    };

    private static string StateClass(WbsItemDto w) => LabelOf(w, Today) switch
    {
        "DISCARD" => "jsini-badge--off pm-badge--discard",
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

    /// <summary>칸의 빈 자리를 눌렀다. 그 날을 고르고 오른쪽에 그 날의 목록을 편다(휴대폰은 격자 아래 목록이 받는다).</summary>
    private Task OnDayClickAsync(DateOnly day)
    {
        _selected = day;
        _dayPane = true;
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
        SyncMiniMonth();

        return SearchAsync();
    }

    /// <summary>막대를 눌렀다. 휴대폰은 수정창 대신 그 날을 골라 아래 목록을 편다 — 수정은 목록에서 한다.</summary>
    private void OnBarClick(WeekRow week, Bar bar)
    {
        if (_isPhone)
        {
            _selected = week.Days[bar.Col].Date;
            return;
        }

        StartEdit(bar.Item);
    }

    /// <summary>「+N」을 눌렀다. 휴대폰은 아래 목록이 이미 그 날 전부라 창을 띄우지 않는다.</summary>
    private void OnMoreClick(DateOnly day)
    {
        if (_isPhone)
        {
            _selected = day;
            return;
        }

        // 오른쪽 목록이 그 날 전부라 창을 따로 띄우지 않는다.
        _selected = day;
        _dayPane = true;
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

        css.Add(day.Date.DayOfWeek switch
        {
            DayOfWeek.Sunday => "pm-gc-day--sun",
            DayOfWeek.Saturday => "pm-gc-day--sat",
            _ => "pm-gc-day--wd",
        });

        return string.Join(' ', css);
    }

    /// <summary>막대를 칸 위에 얹는다. grid-column 으로 칸을 가로지른다.</summary>
    private string BarStyle(Bar bar) =>
        AllProjects
            ? $"grid-column: {bar.Col + 1} / span {bar.Span}; grid-row: {bar.Lane + 1}; --pm-prj: {ProjectColor(bar.Item)};"
            : $"grid-column: {bar.Col + 1} / span {bar.Span}; grid-row: {bar.Lane + 1};";

    private string MoreStyle(int col) =>
        $"grid-column: {col + 1}; grid-row: {MaxLanes + 1};";

    /// <summary>막대의 꼴. 주를 넘어가는 쪽은 모서리를 펴 둔다 — 잘린 끝이 둥글면 거기서 끝난 것으로 보인다.</summary>
    private string BarCss(Bar bar)
    {
        var css = new List<string>(4) { "pm-gc-bar", $"pm-gc-bar--{Label(bar.Item)}" };

        if (bar.ClipStart) css.Add("pm-gc-bar--clip-s");
        if (AllProjects) css.Add("pm-gc-bar--prj");
        if (bar.ClipEnd) css.Add("pm-gc-bar--clip-e");

        return string.Join(' ', css);
    }

    private string BarTip(WbsItemDto w)
    {
        var rest = Describe(w);
        var head = ProjectTag(w) is { } prj ? $"[{prj}] {Title(w)}" : Title(w);

        return rest.Length == 0
            ? $"{head} · {Period(w)}"
            : $"{head} · {Period(w)} · {rest}";
    }

    /// <summary>칸을 읽어 주는 말. 낭독기에는 날짜와 건수를 준다(막대의 글자는 잘려 있다).</summary>
    private static string DayLabel(CalDay day) =>
        day.Items.Count == 0
            ? $"{day.Date.Month}월 {day.Date.Day}일"
            : $"{day.Date.Month}월 {day.Date.Day}일, 일정 {day.Items.Count}건";

    /// <summary>제목. 비어 있는 줄이 실제로 있어서 빈칸으로 두지 않는다.</summary>
    private static string Title(WbsItemDto w) =>
        string.IsNullOrWhiteSpace(w.ProcNm) ? "(제목 없음)" : w.ProcNm;

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

    /// <summary>막대를 다른 날에 놓았다. 집은 날에서 놓은 날까지 벌어진 만큼 통째로 옮긴다 — 기간과 시각은 그대로다.</summary>
    [JSInvokable]
    public async Task MoveByDrag(int wbsId, string from, string to)
    {
        var row = _rows.FirstOrDefault(w => w.WbsId == wbsId);

        if (row is null || !_canUpdate
            || !DateOnly.TryParse(from, out var grabbed) || !DateOnly.TryParse(to, out var dropped)
            || grabbed == dropped)
        {
            return;
        }

        var shift = dropped.DayNumber - grabbed.DayNumber;
        var next = Copy(row);
        next.PlanSdt = next.PlanSdt?.AddDays(shift);
        next.PlanEdt = next.PlanEdt?.AddDays(shift);

        if (await RunAsync(() => Api.UpdateAsync(next), "옮겼습니다.", "옮기지 못했습니다"))
        {
            await SearchAsync();
        }

        StateHasChanged();
    }

    private async Task AttachDragAsync()
    {
        try
        {
            _dragRef ??= DotNetObjectReference.Create(this);
            _dragModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/gc-month-drag.js");
            await _dragModule.InvokeVoidAsync("attach", _dragRef, _dragOwner);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task AttachDeleteKeyAsync()
    {
        try
        {
            _dragRef ??= DotNetObjectReference.Create(this);
            _deleteKeyModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/gc-table-delete.js");
            await _deleteKeyModule.InvokeVoidAsync("attach", _mainRef, _dragRef);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_dragModule is not null)
            {
                await _dragModule.InvokeVoidAsync("detach", _dragOwner);
                await _dragModule.DisposeAsync();
            }

            if (_turnModule is not null)
            {
                await _turnModule.DisposeAsync();
            }

            if (_deleteKeyModule is not null)
            {
                await _deleteKeyModule.InvokeVoidAsync("detach", _mainRef);
                await _deleteKeyModule.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }

        _dragRef?.Dispose();
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

    /// <summary>좌우로 쓸면 앞뒤 구간으로 간다.</summary>
    private Task OnTouchEndAsync(TouchEventArgs e)
    {
        var direction = SwipeDirection(e);

        return direction == 0 ? Task.CompletedTask : TurnPageAsync(direction, month: true, () => ShiftAsync(direction));
    }

    /// <summary>휴대폰에서 일정 목록·표를 좌우로 쓸면 하루 앞뒤로 간다. 월 격자의 쓸기는 그대로 달을 넘긴다.</summary>
    private Task OnDaySwipeEndAsync(TouchEventArgs e)
    {
        var direction = _isPhone ? SwipeDirection(e) : 0;

        return direction == 0 ? Task.CompletedTask : TurnPageAsync(direction, month: false, () => StepDayAsync(direction));
    }

    /// <summary>
    /// 판을 쓴 쪽으로 밀어 내고, 새 자료가 그려진 뒤 반대쪽에서 들여온다 — 자료만 바뀌면 넘어간 줄 모른다.
    /// 들여오기는 그림이 끝난 뒤(OnAfterRenderAsync)라야 새 줄이 움직인다. 조회가 실패해도 들여와야 판이 흐린 채로 안 남는다.
    /// </summary>
    private async Task TurnPageAsync(int direction, bool month, Func<Task> step)
    {
        await TurnAsync("turnOut", month ? _monthTurnRef : _dayTurnRef, direction);

        try
        {
            await step();
        }
        finally
        {
            _turnInDir = direction;
            _turnInMonth = month;
            StateHasChanged();
        }
    }

    private async Task TurnAsync(string move, ElementReference target, int direction)
    {
        if (target.Context is null)
        {
            return;
        }

        try
        {
            _turnModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/gc-swipe-turn.js");
            await _turnModule.InvokeVoidAsync(move, target, direction);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            // 움직임은 곁다리다 — 못 그려도 날은 넘어가 있다.
        }
    }

    /// <summary>쓴 방향. 왼쪽으로 쓸면 다음(+1)이다 — 종이를 넘기는 쪽과 같다. 세로로 더 많이 움직였으면 0 이다 — 목록을 내릴 때마다 넘어가지 않게.</summary>
    private int SwipeDirection(TouchEventArgs e)
    {
        if (e.ChangedTouches.Length == 0)
        {
            return 0;
        }

        var dx = e.ChangedTouches[0].ClientX - _touchX;
        var dy = e.ChangedTouches[0].ClientY - _touchY;

        if (Math.Abs(dx) < SwipeMin || Math.Abs(dy) >= Math.Abs(dx))
        {
            return 0;
        }

        return dx < 0 ? 1 : -1;
    }

    /// <summary>하루 앞뒤로 간다. 월 보기에서 같은 달 안이면 고른 날만 옮긴다 — 이미 읽어 둔 달이라 다시 읽을 까닭이 없다.</summary>
    private Task StepDayAsync(int direction)
    {
        switch (_view)
        {
            case CalView.Table:
                return JumpAsync((_tableStart ?? _selected).AddDays(direction));

            case CalView.Month:
                var day = _selected.AddDays(direction);

                if (day.Year == _anchor.Year && day.Month == _anchor.Month)
                {
                    _selected = day;
                    return Task.CompletedTask;
                }

                return JumpAsync(day);

            case CalView.Day:
                return JumpAsync(DateOnly.FromDateTime(_anchor).AddDays(direction));

            // 주·목록은 하루씩 옮기면 같은 구간이 그대로 보인다 — 보기의 단위만큼 간다.
            default:
                return ShiftAsync(direction);
        }
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

    private void StartNew(DateOnly from, DateOnly to) =>
        _editPopup?.OpenNew(new WbsItemDto
        {
            PrjRid = ProjectRid,
            ScheduleType = NewKind,
            PlanSdt = from.ToDateTime(TimeOnly.MinValue),
            PlanEdt = (to < from ? from : to).ToDateTime(TimeOnly.MinValue),
            DevUser = CurrentUserName,
        });

    private void StartEdit(WbsItemDto w) => _editPopup?.OpenEdit(w);

    /// <summary>새 줄에 붙일 구분. 하나만 고른 때만 따른다 — 여럿이면 어디에 넣을지 알 수 없고, 임의로 첫째를 집으면 고른 적 없는 구분에 조용히 들어간다.</summary>
    private string NewKind =>
        _scheduleTypes.Count == 1 ? _scheduleTypes[0] : DefaultKind;

    /// <summary>저장 중인 줄. 응답 전에 한 번 더 누르면 같은 값으로 두 번 가서 되돌린 것이 안 남는다.</summary>
    private readonly HashSet<int> _toggling = [];

    /// <summary>오른쪽 일정 목록의 상태. 대기·진행·완료에 지연(계획 종료일이 지났는데 안 끝난 것)을 얹는다.</summary>
    private static string AgendaStateCode(WbsItemDto w) => GetInlineStateCode(w) switch
    {
        "DISCARD" => "DISCARD",
        "COMP" => "COMP",
        _ when w.PlanEdt is not null && DateOnly.FromDateTime(w.PlanEdt.Value) < Today => "OVER",
        var code => code,
    };

    private static string AgendaStateName(WbsItemDto w) => AgendaStateCode(w) switch
    {
        "DISCARD" => "폐기",
        "OVER" => "지연",
        "RUNNING" => "진행중",
        "COMP" => "완료",
        _ => "대기",
    };

    private static string AgendaStateClass(WbsItemDto w) => AgendaStateCode(w) switch
    {
        "DISCARD" => "jsini-badge--off pm-badge--discard",
        "OVER" => "jsini-badge--err",
        "RUNNING" => "jsini-badge--warn",
        "COMP" => "jsini-badge--on",
        _ => "jsini-badge--off",
    };

    private static string NextAgendaStateAction(WbsItemDto w) => NextAgendaStateCode(w) switch
    {
        "RUNNING" => "진행중으로 변경",
        "COMP" => "완료로 변경",
        _ => "대기로 변경",
    };

    /// <summary>지연은 대기든 진행이든 같은 「지연」으로 보이므로 둘 사이를 오가면 눌러도 안 바뀐 것처럼 보인다 — 바로 완료로 넘긴다.</summary>
    private static string NextAgendaStateCode(WbsItemDto w) => AgendaStateCode(w) switch
    {
        "READY" => "RUNNING",
        "RUNNING" or "OVER" => "COMP",
        _ => "READY",
    };

    /// <summary>오른쪽 일정 목록의 상태를 대기 → 진행 → 완료 → 대기로 순환한다. 지연은 완료로 간다.</summary>
    private async Task CycleAgendaStateAsync(WbsItemDto w)
    {
        if (!_canUpdate || !_toggling.Add(w.WbsId))
        {
            return;
        }

        try
        {
            var next = Copy(w);
            next.Discarded = false;
            var nextState = NextAgendaStateCode(w);
            var nextStateAction = NextAgendaStateAction(w);

            switch (nextState)
            {
                case "RUNNING":
                    next.DevSdt ??= AppTime.TodayDate;
                    next.DevEdt = null;
                    break;
                case "COMP":
                    next.DevSdt ??= AppTime.TodayDate;
                    next.DevEdt = AppTime.TodayDate;
                    break;
                default:
                    next.DevSdt = null;
                    next.DevEdt = null;
                    break;
            }
            if (await RunAsync(() => Api.UpdateAsync(next),
                    $"상태를 {nextStateAction}했습니다.",
                    "상태를 변경하지 못했습니다"))
            {
                await SearchAsync();
            }
        }
        finally
        {
            _toggling.Remove(w.WbsId);
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

    /// <summary>표의 ＋(아래 띠·오른쪽 클릭 창). FillNew 로 채운 새 줄을 공통 창으로 연다.</summary>
    private void OnGridEditOpen(WbsItemDto w, bool isNew)
    {
        if (isNew)
        {
            _editPopup?.OpenNew(w);
        }
        else
        {
            StartEdit(w);
        }
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

    /// <summary>표에서 고른 줄을 Delete 글쇠로 지운다. 「⋮」의 삭제와 같은 확인을 거친다. 편집 창이 떠 있으면 그 창의 일이라 받지 않는다.</summary>
    [JSInvokable]
    public async Task DeleteByKey()
    {
        var row = _editRow;

        if (_view != CalView.Table || !_canDelete || _editPopup?.IsOpen == true || row is null || _confirm is null)
        {
            return;
        }

        if (!await _confirm.AskAsync($"「{row.ProcNm}」 을(를) 지웁니다.\n되돌릴 수 없습니다."))
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteAsync(row.WbsId), "지웠습니다.", "지우지 못했습니다"))
        {
            _editRow = null;
            await SearchAsync();
        }

        StateHasChanged();
    }

    // ── 표 보기 인라인 즉시 수정 저장 ──────────────────────────────

    private async Task EnsureDropdownDataAsync()
    {
        if (_scheduleTypeCodes.Count == 0)
        {
            _scheduleTypeCodes = await Codes.GetAsync("schedule_type");
        }

        if (_addTargetCodes.Count == 0)
        {
            _addTargetCodes = await Codes.GetAsync(AddTargets.CodeId);
        }

        if (_userOptions.Count == 0)
        {
            _userOptions = await Biz.GetAsync("portal_account");
        }
    }

    /// <summary>표에서 고른 줄. 이 줄만 입력칸으로 그린다(나머지는 글자) — 줄마다 편집 부품을 세우면 표로 바꿀 때 화면이 굳는다.</summary>
    private WbsItemDto? _editRow;

    private bool IsEditing(WbsItemDto w) =>
        _canUpdate && _editRow is not null && _editRow.WbsId == w.WbsId;

    private void OnTableRowSelected(WbsItemDto? w) => _editRow = w;

    /// <summary>표 줄을 두 번 누르면 달력과 같은 편집 창을 연다. 권한이 없으면 달력의 줄 단추처럼 아무 일도 안 한다.</summary>
    private void OnTableRowDoubleClick(WbsItemDto w)
    {
        if (_canUpdate)
        {
            StartEdit(w);
        }
    }

    /// <summary>시작·종료·기간을 왼쪽에 붙박는다. 휴대폰은 표를 카드로 펴므로 붙박지 않는다.</summary>
    private GridColumnFixedPosition PinnedColumn =>
        _isPhone ? GridColumnFixedPosition.None : GridColumnFixedPosition.Left;

    /// <summary>기간 칸의 글자. 하루를 24시간으로 센 총 시간이다 — 「48시간」, 「1시간 30분」.</summary>
    private static string HoursText(double? hours)
    {
        if (hours is not { } h)
        {
            return string.Empty;
        }

        var minutes = (long)Math.Round(h * 60, MidpointRounding.AwayFromZero);
        if (minutes < 0)
        {
            minutes = 0;
        }

        var whole = minutes / 60;
        var rest = minutes % 60;

        return rest == 0 ? $"{whole}시간" : $"{whole}시간 {rest}분";
    }

    /// <summary>기간 칸 마우스 오버 시 한글 설명 툴팁.</summary>
    private static string HoursTip(double? hours)
    {
        if (hours is not { } h)
        {
            return string.Empty;
        }

        var minutes = (long)Math.Round(h * 60, MidpointRounding.AwayFromZero);
        if (minutes < 0)
        {
            minutes = 0;
        }

        var totalHours = minutes / 60;
        var restMinutes = minutes % 60;
        var days = totalHours / 24;
        var h24 = totalHours % 24;

        var parts = new List<string>();
        if (days > 0) parts.Add($"{days}일");
        if (h24 > 0 || (days == 0 && restMinutes == 0)) parts.Add($"{h24}시간");
        if (restMinutes > 0) parts.Add($"{restMinutes}분");

        return string.Join(" ", parts);
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

    private static string GetInlineStateCode(WbsItemDto w) => ScheduleEditPopup.StateCode(w);

    private static bool IsDiscarded(WbsItemDto w) => GetInlineStateCode(w) == "DISCARD";

    /// <summary>표의 줄 표시. 폐기한 일정은 줄 전체를 흐리고 글자에 줄을 긋는다(projmng.css).</summary>
    private static string? TableRowClass(WbsItemDto w) => IsDiscarded(w) ? "pm-row--discard" : null;

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
                w.Discarded = updated.Discarded;
                w.ModUser = updated.ModUser;
                w.ModDt = updated.ModDt;
            }

            // 달력 재료는 짜지 않는다 — 인라인 수정은 표 보기에서만 일어나고, 달력으로 돌아갈 때 다시 읽는다.
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

    /// <summary>공통코드를 못 읽었을 때 도는 차례. 공통코드가 있으면 그 차례를 따른다.</summary>
    private static readonly string[] FallbackAddTargets = ["ADD", "CHANGE", "MOVE"];

    /// <summary>연달아 누른 것은 마지막 값 하나만 저장한다 — 누를 때마다 보내면 응답이 엇갈려 앞의 값이 이길 수 있다.</summary>
    private readonly Dictionary<int, CancellationTokenSource> _addTargetSaves = [];

    private const int AddTargetSaveDelayMs = 600;

    /// <summary>비움 → 공통코드 차례 → 다시 비움으로 돈다.</summary>
    private void CycleAddTarget(WbsItemDto w)
    {
        var codes = _addTargetCodes.Count > 0
            ? _addTargetCodes.Select(c => c.Code).Where(c => !string.IsNullOrWhiteSpace(c)).ToList()
            : [.. FallbackAddTargets];

        var current = w.AddTarget?.Trim() ?? string.Empty;
        var at = codes.FindIndex(c => string.Equals(c, current, StringComparison.OrdinalIgnoreCase));

        // 비어 있거나 목록에 없는 값이면 첫 코드로, 마지막 코드 다음은 비움이다.
        // 지운 것은 빈 글자로 보낸다 — null 은 서버가 「그대로 둔다」로 읽는다.
        w.AddTarget = current.Length == 0 || at < 0
            ? codes.FirstOrDefault() ?? string.Empty
            : at + 1 < codes.Count ? codes[at + 1] : string.Empty;

        _ = SaveAddTargetLaterAsync(w);
    }

    private async Task SaveAddTargetLaterAsync(WbsItemDto w)
    {
        if (_addTargetSaves.Remove(w.WbsId, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        var cts = new CancellationTokenSource();
        _addTargetSaves[w.WbsId] = cts;

        try
        {
            await Task.Delay(AddTargetSaveDelayMs, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        _addTargetSaves.Remove(w.WbsId);
        cts.Dispose();

        try
        {
            await InvokeAsync(() => SaveInlineAsync(w));
        }
        catch (ObjectDisposedException)
        {
            // 기다리는 사이에 회로가 닫혔다 — 보낼 길이 없다.
        }
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
        val = val?.Trim() ?? string.Empty;
        if (string.Equals(w.DevUser ?? string.Empty, val, StringComparison.Ordinal)) return;

        w.DevUser = val;
        await SaveInlineAsync(w);
    }

    private async Task OnInlineStateChangedAsync(WbsItemDto w, string? val)
    {
        var current = GetInlineStateCode(w);
        if (string.Equals(current, val, StringComparison.Ordinal)) return;

        ScheduleEditPopup.ApplyState(w, val ?? "READY", AppTime.TodayDate);

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

    private static WbsItemDto Copy(WbsItemDto w) => ScheduleEditPopup.Copy(w);

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

    // ── 일정 메일 발송 ───────────────────────────────────────────

    [Inject] private ScreenState State { get; set; } = default!;

    /// <summary>메일 발송 화면을 탭으로 연다. 프로젝트·구분·진행 상태 조건은 지금 화면의 것을 맡겨 넘기고, 기간은 그 화면에서 고른다.</summary>
    private void OpenMail()
    {
        State.Set(ScheduleMailPage.ScopeKey, new ScheduleMailScope(
            ProjectRid, AllProjects ? null : _projectName, _completeState, [.. _scheduleTypes], Guid.NewGuid()));

        Navigation.NavigateTo(ScheduleMailPage.Href);
    }

    // ── 일정 찾기 ───────────────────────────────────────────────

    private bool _findOpen;

    private bool _finding;

    private string? _findKeyword;

    /// <summary>찾는 기간. 기본은 오늘 앞뒤 1년이고, 비우면 그쪽 끝을 묶지 않는다.</summary>
    private DateTime? _findFrom;

    private DateTime? _findTo;

    /// <summary>찾은 줄. null 은 아직 찾지 않았다는 뜻이다 — 「찾은 것 없음」과 갈린다.</summary>
    private List<WbsItemDto>? _findHits;

    /// <summary>찾는 범위. 프로젝트·구분·진행은 화면 조건을 그대로 쓴다 — 찾은 일정이 달력에서 걸러져 안 보이는 일이 없게.</summary>
    private string FindScopeText => "범위: " + SchSummary.Of(
        _projectName, SchSummary.Or(_kindNames), _completeStateName);

    /// <summary>창을 연다. 기간은 처음 열 때만 채운다 — 닫았다 다시 열면 전에 찾던 조건과 결과가 그대로다.</summary>
    private async Task OpenFind()
    {
        if (_findFrom is null && _findTo is null && _findHits is null)
        {
            _findFrom = AppTime.TodayDate.AddYears(-1);
            _findTo = AppTime.TodayDate.AddYears(1);
        }

        _findOpen = true;

        // 담당자 이름으로도 찾으므로 계정 목록을 미리 받아 둔다.
        await EnsureDropdownDataAsync();
    }

    private Task OnFindKeyAsync(KeyboardEventArgs e) =>
        e.Key == "Enter" && !_finding ? FindAsync() : Task.CompletedTask;

    private async Task FindAsync()
    {
        _finding = true;
        StateHasChanged();

        try
        {
            var (from, to) = (_findFrom, _findTo);
            if (from is not null && to is not null && from > to)
            {
                (from, to) = (to, from);
                (_findFrom, _findTo) = (from, to);
            }

            var rows = await Api.ListAsync(
                ProjectRid, _completeState, _scheduleTypes,
                from is null ? null : DateOnly.FromDateTime(from.Value),
                to is null ? null : DateOnly.FromDateTime(to.Value),
                mine: AllProjects);

            var word = _findKeyword?.Trim();

            _findHits = [.. rows
                .Where(w => string.IsNullOrEmpty(word) || FindMatches(w, word))
                .OrderBy(w => w.PlanSdt ?? w.PlanEdt ?? DateTime.MaxValue)
                .ThenBy(w => w.WbsId)];
        }
        catch (Exception ex)
        {
            _findHits = null;
            Say($"일정을 찾지 못했습니다: {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _finding = false;
        }
    }

    /// <summary>제목·메모·원문·종류·요청자·담당자(아이디와 이름) 어디에든 들어 있으면 맞다.</summary>
    private bool FindMatches(WbsItemDto w, string word)
    {
        bool Has(string? s) => s?.Contains(word, StringComparison.OrdinalIgnoreCase) == true;

        return Has(w.ProcNm) || Has(w.Comm) || Has(w.BodyText) || Has(w.ProcTp)
            || Has(w.ReqUser) || Has(w.DevUser) || Has(GetUserDisplayName(w.DevUser));
    }

    /// <summary>찾은 줄의 기간. 여러 해에 걸쳐 찾으므로 해까지 적는다.</summary>
    private static string FoundPeriod(WbsItemDto w)
    {
        var start = w.PlanSdt ?? w.PlanEdt;
        if (start is null)
        {
            return "기간 없음";
        }

        var end = w.PlanEdt ?? start.Value;
        return end.Date > start.Value.Date
            ? $"{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}"
            : $"{start:yyyy-MM-dd}";
    }

    /// <summary>찾은 일정으로 간다. 월 보기로 바꾸고 그 일정이 시작하는 달을 펴서 그 날을 고른다(오른쪽 목록도 그 날로 펴진다).</summary>
    private async Task GoToFoundAsync(WbsItemDto w)
    {
        if ((w.PlanSdt ?? w.PlanEdt) is not { } at)
        {
            return;
        }

        var day = DateOnly.FromDateTime(at);

        _findOpen = false;
        _view = CalView.Month;
        _editRow = null;
        _anchor = day.ToDateTime(TimeOnly.MinValue);
        _selected = day;
        _dayPane = true;
        SyncMiniMonth();

        await SearchAsync();
    }

    /// <summary>상태 색. 구글 캘린더의 색조로 옮긴 넷이다.</summary>
    private sealed record StateLabel(string Id, string Caption, System.Drawing.Color Color);

    private static readonly StateLabel[] StateLabels =
    [
        new("READY", "대기", System.Drawing.Color.FromArgb(0xF9, 0xAB, 0x00)),
        new("RUNNING", "진행중", System.Drawing.Color.FromArgb(0x1A, 0x73, 0xE8)),
        new("OVER", "지연", System.Drawing.Color.FromArgb(0xD9, 0x30, 0x25)),
        new("COMP", "완료", System.Drawing.Color.FromArgb(0x80, 0x86, 0x8B)),
        new("DISCARD", "폐기", System.Drawing.Color.FromArgb(0xBD, 0xC1, 0xC6)),
    ];
}
