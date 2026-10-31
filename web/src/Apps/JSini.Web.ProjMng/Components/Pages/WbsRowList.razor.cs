using System.Text.Json;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsRowList
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;
    [Inject] private WbsBoardTaskClient TaskApi { get; set; } = default!;
    [Inject] private WbsDevUserClient Prefs { get; set; } = default!;
    [Inject] private BizOptions Biz { get; set; } = default!;
    [Inject] private FileGroupClient Files { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>칸 순서·표시 상태가 바뀔 때마다 그리드를 다시 세우는 키.</summary>
    private int _colsRevision;
    private string GridKey => $"wbs-grid-{_colsRevision}";

    /// <summary>추가/수정/삭제 직후 사용자에게 안내 띠를 띄운다.</summary>
    private string? _addedName;
    private string? _addedId;
    private string? _savedName;
    private string? _goneName;
    private long? _goneDelId;

    private void FilterToAdded()
    {
        if (_addedId is not null)
        {
            _filter.Q = _addedId;
            _ = SearchAsync();
        }
    }

    /// <summary>보이는 칸을 담아 두는 설정 이름. 참고 소스와 같은 이름이라 옮겨 온 설정도 읽힌다.</summary>
    private const string ColsPref = "rows.cols";

    /// <summary>첨부를 담는 FileServer 업무 구분(저장 폴더 이름).</summary>
    private const string FileBizType = "wbs-row";

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _filter.Basis),
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _filter.Scope),
        _filter.Month, _filter.Week,
        SchSummary.NameOf(_userOptions, o => o.Value, o => o.Text, _filter.User, string.Empty),
        SchSummary.NameOf(_realOptions, o => o.Value, o => o.Text, _filter.RealUser, string.Empty),
        SchSummary.NameOf(_moduleOptions, o => o.Value, o => o.Text, _filter.Module, string.Empty),
        SchSummary.NameOf(DoneOptions, o => o.Value, o => o.Text, _filter.Done, string.Empty),
        SchSummary.NameOf(LateOptions, o => o.Value, o => o.Text, _filter.Late, string.Empty),
        SchSummary.NameOf(StatusOptions, o => o.Value, o => o.Text, _filter.Status, string.Empty),
        _filter.Q);

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    private string? _projectCode;
    private readonly WbsBoardFilter _filter = new() { Basis = "edt", Scope = "dev", Limit = 5000 };

    private IReadOnlyList<WbsBoardRowDto> _rows = [];

    /// <summary>고른 프로젝트의 원장이 범위·조건과 상관없이 0건이다.</summary>
    private bool _ledgerEmpty;
    private int _noIdCount;

    private SchOption[] _userOptions = [];
    private SchOption[] _realOptions = [];
    private SchOption[] _moduleOptions = [];
    private SchOption[] _monthOptions = [];
    private SchOption[] _weekOptions = [];

    /// <summary>담당자·개발자 고르개의 사람들(포털 계정). 원장에 적힌 사번이 계정 아이디다.</summary>
    private SchOption[] _people = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string Hint =>
        $"{_rows.Count(r => r.ComplateYn == "o")} / {_rows.Count}건 완료"
        + (_rows.Count(r => r.StartLate || r.FinishLate) is var late && late > 0 ? $" · 지연 {late}건" : "")
        + (_rows.Count(r => r.RecheckYn == "o" || r.RecheckBigYn == "o") is var re && re > 0 ? $" · 재확인 {re}건" : "");

    private static readonly SchOption[] DoneOptions =
    [
        new(null, "전체"),
        new("y", "완료"),
        new("n", "미완료"),
    ];

    private static readonly SchOption[] StatusOptions =
    [
        new(null, "전체"),
        new("open", "착수도래 미완료"),
        new("notyet", "미도래"),
        new("done", "완료"),
    ];

    private static readonly SchOption[] LateOptions =
    [
        new(null, "전체"),
        new("any", "하나라도"),
        new("start", "착수지연"),
        new("finish", "종료지연"),
        new("both", "둘 다"),
        new("none", "지연 없음"),
    ];

    private static readonly SchOption[] PriorityOptions = [new("1", "1 · 선행"), new("2", "2 · 후행")];

    /// <summary>우선순위 고르개 — 기본값(1·2)과 목록에 이미 있는 값.</summary>
    private IEnumerable<string> Priorities =>
        new[] { "1", "2" }
            .Concat(_rows.Select(r => r.PriorityOrder).OfType<string>().Where(p => p.Length > 0))
            .Distinct()
            .Order(StringComparer.Ordinal);

    /// <summary>다른 화면에서 실려 온 조건을 먼저 읽는다. 화면이 정한 기본값보다 주소가 이긴다 — 안 그러면 차트에서 눌러 넘어왔는데 조건 없는 전체 목록이 뜬다.</summary>
    protected override void OnInitialized()
    {
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(Navigation.Uri).Query);

        string? Get(string key) =>
            string.IsNullOrWhiteSpace(query[key]) ? null : query[key];

        _filter.Basis = Get("basis") ?? _filter.Basis;
        _filter.Scope = Get("scope") ?? _filter.Scope;
        _filter.Month = Get("month");
        _filter.Week = Get("week");
        _filter.User = Get("user");
        _filter.RealUser = Get("realUser");
        _filter.Module = Get("module");
        _filter.Q = Get("q");
        _filter.Done = Get("done");
        _filter.Late = Get("late");
        _filter.Status = Get("status");
    }

    /// <summary>월과 주는 둘 다 날짜 기준이라 하나를 고르면 다른 하나를 푼다.</summary>
    private void PickMonth(string? month)
    {
        _filter.Month = month;
        if (month is not null) _filter.Week = null;
    }

    /// <inheritdoc cref="PickMonth"/>
    private void PickWeek(string? week)
    {
        _filter.Week = week;
        if (week is not null) _filter.Month = null;
    }

    private int? _colsLoadedFor;

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _rows = [];
            _ledgerEmpty = false;
            _noIdCount = 0;
            return 0;
        }

        // 목록·일감 건수·고르개를 나란히 읽는다. 일감 건수는 줄마다 묻지 않으려고 한 번에 받는다.
        var rows = Api.RowsAsync(rid, _filter);
        var counts = TaskApi.CountsAsync(rid);
        var users = Api.UsersAsync(rid, _filter.Scope, _filter.Basis, _filter.Month, _filter.Week);
        var modules = Api.ByModuleAsync(rid, _filter.Basis, _filter.Scope);
        var monthly = Api.MonthlyAsync(rid, _filter.Basis, _filter.Scope);
        var weekly = Api.WeeklyAsync(rid, _filter.Basis, _filter.Scope);
        var ledger = Api.SummaryAsync(rid, _filter.Basis, "all");
        var people = _people.Length == 0 ? Biz.GetAsync("portal_account") : Task.FromResult<IReadOnlyList<BizOption>>([]);
        var cols = _colsLoadedFor == rid ? Task.CompletedTask : LoadColsAsync(rid);

        await Task.WhenAll(rows, counts, users, modules, monthly, weekly, ledger, people, cols);

        _rows = rows.Result;
        _ledgerEmpty = ledger.Result is { Total: 0 };
        _noIdCount = _rows.Count(r => r.NoId);

        ApplyTaskCounts(counts.Result);

        if (people.Result.Count > 0)
        {
            _people = [.. people.Result
                .OrderBy(p => p.Label, StringComparer.CurrentCulture)
                .Select(p => new SchOption(p.Value, $"{p.Label} ({p.Value})"))];
        }

        _userOptions =
        [
            new(null, "전체"),
            .. users.Result.Select(u => new SchOption(u.UserBpId, $"{u.UserNm} ({u.Cnt})"))
        ];

        // 개발자 고르개는 따로 묻지 않는다 — 지금 목록에 있는 사람이면 충분하다.
        _realOptions =
        [
            new(null, "전체"),
            .. _rows
                .GroupBy(r => r.UserRealId ?? "(미지정)")
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new SchOption(g.Key, $"{g.First().UserRealNm ?? g.Key} ({g.Count()})"))
        ];

        _moduleOptions =
        [
            new(null, "전체"),
            .. modules.Result.Select(m => new SchOption(m.Systemcode, $"{m.Systemcode} {m.SystemNm}"))
        ];

        _monthOptions = [.. monthly.Result.Select(m => new SchOption(m.Bucket, $"{m.Bucket} · {m.Cnt}건"))];

        _weekOptions = [.. weekly.Result.Select(w => new SchOption(
            w.Bucket, $"{w.Bucket} ({w.WeekStart?[5..]}~{w.WeekEnd?[5..]}) · {w.Cnt}건"))];

        return _rows.Count;
    }, "조건에 맞는 화면이 없습니다.", "목록을 읽지 못했습니다");

    private void ApplyTaskCounts(IReadOnlyList<WbsBoardTaskCountDto> counts)
    {
        var byActivity = counts
            .Where(c => c.ActivityId is not null)
            .ToDictionary(c => c.ActivityId!, c => c);

        foreach (var row in _rows)
        {
            var hit = row.ActivityId is not null && byActivity.TryGetValue(row.ActivityId, out var c) ? c : null;
            row.TaskCnt = hit?.Cnt ?? 0;
            row.TaskDone = hit?.Done ?? 0;
        }
    }

    /// <summary>일감 창이 바꾼 뒤 — 목록 전체가 아니라 건수만 다시 받는다.</summary>
    private async Task ReloadTaskCountsAsync()
    {
        if (ProjectRid is not int rid) return;

        try
        {
            ApplyTaskCounts(await TaskApi.CountsAsync(rid));
            StateHasChanged();
        }
        catch (ApiException ex)
        {
            Say($"일감 건수를 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    // ──────────────────────────────────────────── 보이는 칸

    /// <summary>보이는 칸과 그 차례. 목록의 차례가 곧 표의 칸 차례다(엑셀도 그대로 따른다).</summary>
    private List<string> _cols = [.. WbsRowSchema.Cols.Select(c => c.Key)];
    private bool _colOpen;
    private string? _colWho;

    private bool On(string key) => _cols.Contains(key);

    /// <summary>칸의 자리. 꺼 둔 칸은 뒤로 몰아 켠 칸끼리의 차례를 흐트러뜨리지 않는다.</summary>
    private int Ix(string key)
    {
        var at = _cols.IndexOf(key);
        return at >= 0 ? at : _cols.Count + SchemaIndex(key);
    }

    private static int SchemaIndex(string key)
    {
        for (var i = 0; i < WbsRowSchema.Cols.Count; i++)
        {
            if (WbsRowSchema.Cols[i].Key == key) return i;
        }

        return WbsRowSchema.Cols.Count;
    }

    private int VisibleCount => _cols.Count;

    /// <summary>담긴 값({cols, known})을 지금 칸 목록에 맞춘다. cols 의 차례를 지키고, known 에 없던 칸(담은 뒤에 생긴 칸)은 켠 채로 뒤에 붙인다 — 안 그러면 새 칸이 영영 안 보인다.</summary>
    private async Task LoadColsAsync(int rid)
    {
        _colsLoadedFor = rid;

        try
        {
            var pref = await Prefs.GetPrefAsync(rid, ColsPref);
            _colWho = pref?.Who;

            if (string.IsNullOrWhiteSpace(pref?.Value)) return;

            using var doc = JsonDocument.Parse(pref.Value);
            var root = doc.RootElement;

            static List<string>? Keys(JsonElement el) =>
                el.ValueKind == JsonValueKind.Array
                    ? [.. el.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)]
                    : null;

            var picked = root.ValueKind == JsonValueKind.Array
                ? Keys(root)
                : root.TryGetProperty("cols", out var c) ? Keys(c) : null;
            var known = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("known", out var k) ? Keys(k) : picked;

            if (picked is null) return;

            var all = WbsRowSchema.Cols.Select(x => x.Key).ToList();
            var keep = picked.Where(all.Contains).Distinct().ToList();
            keep.AddRange(all.Where(a => known?.Contains(a) != true && !keep.Contains(a)));

            if (keep.Count > 0)
            {
                _cols = keep;
                _colsRevision++;
                StateHasChanged();
            }
        }
        catch (Exception ex) when (ex is ApiException or JsonException)
        {
            // 설정을 못 읽어도 화면은 전부 보이는 칸으로 선다.
            _colWho = null;
        }
    }

    private async Task SaveColsAsync()
    {
        if (ProjectRid is not int rid) return;

        try
        {
            await Prefs.SetPrefAsync(rid, ColsPref, new
            {
                cols = _cols.ToArray(),
                known = WbsRowSchema.Cols.Select(c => c.Key).ToArray(),
            });
        }
        catch (ApiException ex)
        {
            _colWho = null;
            Say($"칸 설정을 담지 못했습니다 — {ex.Message}", NoticeTone.Warning);
        }
    }

    // 창 안에서는 사본(_draft)만 만진다. 「확인」을 눌러야 표가 바뀌므로 이것저것 켜 보다 「취소」로 되돌릴 수 있다.
    private List<string> _draft = [];
    private string? _colQ;
    private int _dragFrom = -1;
    private int _dragOver = -1;

    private void OpenColumns()
    {
        _draft = [.. _cols];
        _colQ = null;
        _dragFrom = _dragOver = -1;
        _colOpen = true;
    }

    private bool DraftOn(string key) => _draft.Contains(key);

    private static string LabelOf(string key) => WbsRowSchema.Cols.FirstOrDefault(c => c.Key == key)?.Label ?? key;

    private static string GroupOf(string key) => WbsRowSchema.Cols.FirstOrDefault(c => c.Key == key)?.Group ?? "";

    private bool Matches(WbsRowCol c)
    {
        var q = _colQ?.Trim();
        return string.IsNullOrEmpty(q)
            || c.Label.Contains(q, StringComparison.OrdinalIgnoreCase)
            || c.Key.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<IGrouping<string, WbsRowCol>> PickGroups =>
        WbsRowSchema.Cols.Where(Matches).GroupBy(c => c.Group);

    /// <summary>켜는 칸은 맨 뒤에 붙는다 — 오른쪽 차례 목록에서 제자리로 옮긴다.</summary>
    private void ToggleDraft(string key)
    {
        if (!_draft.Remove(key)) _draft.Add(key);
    }

    /// <summary>묶음이 (검색에 걸린 것 중) 전부 켜져 있으면 끄고, 아니면 빠진 것을 뒤에 붙인다.</summary>
    private void ToggleDraftGroup(string group)
    {
        var keys = WbsRowSchema.Cols.Where(c => c.Group == group && Matches(c)).Select(c => c.Key).ToList();

        if (keys.All(_draft.Contains)) _draft.RemoveAll(keys.Contains);
        else _draft.AddRange(keys.Where(k => !_draft.Contains(k)));
    }

    private (int On, int All) GroupCount(string group)
    {
        var keys = WbsRowSchema.Cols.Where(c => c.Group == group).Select(c => c.Key).ToList();
        return (keys.Count(_draft.Contains), keys.Count);
    }

    private void DraftAll() => _draft = [.. WbsRowSchema.Cols.Select(c => c.Key)];

    private void DraftBase() => _draft = [.. WbsRowSchema.BaseCols];

    private void DraftNone() => _draft = [];

    private void DraftRevert() => _draft = [.. _cols];

    /// <summary>고른 것은 그대로 두고 차례만 처음 정의의 차례로 되돌린다.</summary>
    private void DraftSortDefault() =>
        _draft = [.. WbsRowSchema.Cols.Select(c => c.Key).Where(_draft.Contains)];

    private void MoveDraft(int from, int to)
    {
        if (from < 0 || from >= _draft.Count || to < 0 || to >= _draft.Count || from == to) return;

        var key = _draft[from];
        _draft.RemoveAt(from);
        _draft.Insert(to, key);
    }

    private void DragStart(int i) => _dragFrom = i;

    private void DragOver(int i) => _dragOver = i;

    private void Drop(int i)
    {
        MoveDraft(_dragFrom, i);
        _dragFrom = _dragOver = -1;
    }

    private void DragEnd() => _dragFrom = _dragOver = -1;

    private async Task ApplyColumnsAsync()
    {
        if (_draft.Count == 0) return;   // 하나는 남긴다

        _cols = [.. _draft];
        _colsRevision++;
        _colOpen = false;
        await SaveColsAsync();
        StateHasChanged();
    }

    // ──────────────────────────────────────────── 화면 열기 메뉴 (운영/개발)
    private bool _screenMenuOpen;
    private WbsBoardRowDto? _screenMenuRow;

    private void OpenScreenMenu(WbsBoardRowDto r)
    {
        _screenMenuRow = r;
        _screenMenuOpen = true;
    }

    // ──────────────────────────────────────────── ProjectView 메뉴 (작업/일감/단위테스트)
    private bool _pvMenuOpen;
    private WbsBoardRowDto? _pvMenuRow;

    private void OpenPvMenu(WbsBoardRowDto r)
    {
        _pvMenuRow = r;
        _pvMenuOpen = true;
    }

    // ──────────────────────────────────────────── ProjectView 데이터 수집 창
    private bool _pvCollectOpen;
    private void OpenPvCollect() => _pvCollectOpen = true;

    private string GetPvCollectScript()
        => WbsPvScripts.BuildPvCacheScript(_rows.Select(r => r.ActivityId).OfType<string>());

    private async Task CopyPvCollectScriptAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("jsiniClipboard.copy", GetPvCollectScript());
            Say("ProjectView 데이터 수집 스크립트를 클립보드에 복사했습니다.");
        }
        catch
        {
            Say("클립보드 복사에 실패했습니다.", NoticeTone.Warning);
        }
    }

    // ──────────────────────────────────────────── 단위테스트 상태 일괄 변경 창
    private bool _testStatusOpen;
    private string _testStatusValue = "SUCCESS";
    private string _testTarget = "view";

    private static readonly SchOption[] TestStatusChoices =
    [
        new("SUCCESS", "SUCCESS (성공)"),
        new("UNPERFORMED", "UNPERFORMED (미수행)"),
        new("IN_PROGRESS", "IN_PROGRESS (진행중)"),
        new("FAIL", "FAIL (실패)"),
        new("NA", "NA (해당없음)"),
        new("NE", "NE"),
    ];

    private static readonly SchOption[] TestTargetChoices =
    [
        new("view", "목록에 보이는 화면"),
        new("all", "전체 대상"),
    ];

    private void OpenTestStatus() => _testStatusOpen = true;

    private string GetTestStatusScript()
    {
        var map = _rows.Where(r => !string.IsNullOrEmpty(r.ActivityId))
            .ToDictionary(r => r.ActivityId!, r => r.MenuNm ?? "");
        return WbsPvScripts.BuildPvTestScript(_testStatusValue, map, null, _testTarget == "view" ? "목록에 보이는 화면" : "전체");
    }

    private async Task CopyTestStatusScriptAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("jsiniClipboard.copy", GetTestStatusScript());
            Say("단위테스트 상태 변경 스크립트를 클립보드에 복사했습니다.");
        }
        catch
        {
            Say("클립보드 복사에 실패했습니다.", NoticeTone.Warning);
        }
    }

    // ──────────────────────────────────────────── 조건 초기화
    private async Task ResetFiltersAsync()
    {
        _filter.Basis = "edt";
        _filter.Scope = "dev";
        _filter.Month = null;
        _filter.Week = null;
        _filter.User = null;
        _filter.RealUser = null;
        _filter.Module = null;
        _filter.Done = null;
        _filter.Late = null;
        _filter.Status = null;
        _filter.Q = null;
        await SearchAsync();
    }

    // ──────────────────────────────────────────── 칸별 검색

    /// <summary>PV 칸을 켰는데 그 값이 한 줄에도 없다 — 빈 칸을 보고 헤매지 않게 까닭을 적는다.</summary>
    private bool PvShownButEmpty =>
        _rows.Count > 0
        && _cols.Any(k => k.StartsWith("pv_", StringComparison.Ordinal))
        && !_rows.Any(r => !string.IsNullOrEmpty(r.PvSnapshotAt));

    private static (string On, string Off) CheckWords(string col) => col switch
    {
        "recheck_yn" or "recheck_big_yn" => ("✓ 요청됨", "요청 없음"),
        "dwg_view_use" or "report_use" => ("✓ 사용", "미사용"),
        _ => ("✓ 완료", "미완료"),
    };

    private static string CheckFilterValue(GridDataColumnFilterRowCellTemplateContext f) =>
        f.FilterCriteria is BinaryOperator { RightOperand: OperandValue { Value: bool b } } ? (b ? "y" : "n") : "";

    private static void SetCheckFilter(GridDataColumnFilterRowCellTemplateContext f, string? value) =>
        f.FilterCriteria = value is "y" or "n"
            ? new BinaryOperator(f.DataColumn.FieldName, value == "y", BinaryOperatorType.Equal)
            : null;

    // 일감 칸의 값(TaskState)은 「없음」·「미완료 n/m」·「완료 n/m」 셋 중 하나로 시작한다.
    private static readonly (string Value, string Prefix)[] TaskFilterPrefixes =
        [("open", "미완료"), ("done", "완료"), ("none", "없음")];

    private static string TaskFilterValue(GridDataColumnFilterRowCellTemplateContext f) =>
        f.FilterCriteria is FunctionOperator { OperatorType: FunctionOperatorType.StartsWith } fn
        && fn.Operands.Count == 2 && fn.Operands[1] is OperandValue { Value: string prefix }
            ? TaskFilterPrefixes.FirstOrDefault(p => p.Prefix == prefix).Value ?? ""
            : "";

    private static void SetTaskFilter(GridDataColumnFilterRowCellTemplateContext f, string? value)
    {
        var prefix = TaskFilterPrefixes.FirstOrDefault(p => p.Value == value).Prefix;
        f.FilterCriteria = prefix is null
            ? null
            : new FunctionOperator(FunctionOperatorType.StartsWith,
                new OperandProperty(f.DataColumn.FieldName), new OperandValue(prefix));
    }

    // ──────────────────────────────────────────── 칸에서 바로 고치기

    private (string Id, string Col)? _editing;
    private readonly HashSet<string> _busy = [];

    private static string BusyKey(WbsBoardRowDto r, string col) => $"{r.ActivityId}|{col}";

    private bool Busy(WbsBoardRowDto r, string col) => _busy.Contains(BusyKey(r, col));

    private bool IsEditing(WbsBoardRowDto r, string col) => _editing == (r.ActivityId ?? "", col);

    private void StartEdit(WbsBoardRowDto r, string col)
    {
        if (!Can(MenuAction.Update) || r.ActivityId is null) return;
        _editing = (r.ActivityId, col);
    }

    private void StopEdit() => _editing = null;

    /// <summary>고르개 목록. 원장에 적힌 값이 계정 목록에 없으면(퇴사자·오타) 그 값을 맨 위에 그대로 둔다 — 안 두면 고르개가 빈칸으로 보여 지운 줄로 읽힌다.</summary>
    private IEnumerable<SchOption> PeopleFor(string? current)
        => string.IsNullOrEmpty(current) || _people.Any(p => string.Equals(p.Value, current, StringComparison.OrdinalIgnoreCase))
            ? _people
            : [new SchOption(current, $"{current} (계정 없음)"), .. _people];

    private Task OnPickAsync(WbsBoardRowDto r, string col, string? value)
    {
        _editing = null;
        return SaveCellAsync(r, col, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>재확인 요청을 켜면 사유를 남기도록 일감 창을 「확인」 구분으로 띄운다.</summary>
    private async Task OnCheckAsync(WbsBoardRowDto r, string col, bool on)
    {
        var saved = await SaveCellAsync(r, col, on ? "o" : null);

        if (saved && on && col is "recheck_yn" or "recheck_big_yn")
        {
            var big = col == "recheck_big_yn" ? "대형 " : "";
            await OpenTasksAsync(r, "확인",
                $"{big}재확인요청을 켰습니다. 무엇을 다시 확인해야 하는지 일감으로 남겨 주세요.");
        }
    }

    private async Task<bool> SaveCellAsync(WbsBoardRowDto r, string col, string? value)
    {
        if (ProjectRid is not int rid || r.ActivityId is null) return false;
        if ((r.ValueOf(col) ?? "") == (value ?? "")) return false;

        var key = BusyKey(r, col);
        var before = r.ValueOf(col);

        _busy.Add(key);
        r.SetValue(col, value);
        StateHasChanged();

        try
        {
            await Api.PatchRowAsync(rid, r.ActivityId, new Dictionary<string, object?> { [col] = value ?? "" });

            if (col is "user_bp_id" or "user_real_id")
            {
                var name = value is null ? null : _people.FirstOrDefault(p => string.Equals(p.Value, value, StringComparison.OrdinalIgnoreCase))?.Text;
                name = name is null ? value : name[..name.LastIndexOf(" (", StringComparison.Ordinal)];
                if (col == "user_bp_id") r.UserNm = name; else r.UserRealNm = name;
            }

            return true;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            r.SetValue(col, before);
            Say($"고치지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return false;
        }
        finally
        {
            _busy.Remove(key);
            StateHasChanged();
        }
    }

    // ──────────────────────────────────────────── 대상 추가 · 수정

    private bool _formOpen;
    private bool _formBusy;

    /// <summary>고치는 줄의 Activity ID. null 이면 추가다.</summary>
    private string? _formKey;
    private Dictionary<string, string?> _form = new(StringComparer.OrdinalIgnoreCase);

    private string FormTitle => _formKey is null
        ? "대상 추가"
        : $"대상 수정 · {_formKey} · {Get("menu_nm")}";

    /// <summary>창을 열 때의 값. 저장할 때 고친 칸만 고르는 잣대다.</summary>
    private Dictionary<string, string?> _formOriginal = new(StringComparer.OrdinalIgnoreCase);

    private string? Get(string key) => _form.TryGetValue(key, out var v) ? v : null;

    private void Set(string key, string? value) => _form[key] = Norm(value);

    private static string? Norm(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private DateTime? DateOf(string key)
        => DateTime.TryParse(Get(key), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;

    private void StartNew()
    {
        if (ProjectRid is null) return;

        _formKey = null;
        _form = new(StringComparer.OrdinalIgnoreCase)
        {
            ["module"] = "MM",
            ["module_name"] = "자재관리",
            ["new_dev2"] = "o",
        };
        _formOpen = true;
    }

    /// <summary>창을 열기 전에 모든 칸을 읽어 둔다 — 열린 뒤에 채우면 빈 창이 먼저 보인다.</summary>
    private async Task StartEditAsync(WbsBoardRowDto row)
    {
        if (ProjectRid is not int rid || row.ActivityId is null) return;

        try
        {
            _formOriginal = await Api.RowFullAsync(rid, row.ActivityId);
            _form = _formOriginal.ToDictionary(kv => kv.Key, kv => Norm(kv.Value), StringComparer.OrdinalIgnoreCase);
            _formKey = row.ActivityId;
            _formOpen = true;
        }
        catch (ApiException ex)
        {
            Say($"대상을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    private async Task SaveFormAsync()
    {
        if (ProjectRid is not int rid) return;

        var missing = WbsRowSchema.Fields.Where(f => f.Required && string.IsNullOrWhiteSpace(Get(f.Key))).ToList();
        if (missing.Count > 0)
        {
            Say($"필수 항목이 비어 있습니다 — {string.Join(" · ", missing.Select(f => f.Label))}", NoticeTone.Warning);
            return;
        }

        // 고친 칸만 보낸다(비운 것도 고친 것이다). 안 건드린 칸까지 보내면 저장 한 번에 원장의 공백·빈 글자가 정리되어 엑셀 WBS 와 어긋난다.
        var values = WbsRowSchema.Fields
            .Where(f => _formKey is null
                ? !string.IsNullOrWhiteSpace(Get(f.Key))
                : f.Key != "activity_id" && Get(f.Key) != Norm(_formOriginal.GetValueOrDefault(f.Key)))
            .ToDictionary(f => f.Key, f => (string?)(Get(f.Key) ?? ""));

        if (_formKey is not null && values.Count == 0)
        {
            _formOpen = false;
            return;
        }

        _formBusy = true;

        try
        {
            if (_formKey is null)
            {
                var id = await Api.CreateRowAsync(rid, values);
                _formOpen = false;
                _addedName = Get("menu_nm");
                _addedId = id;
                _savedName = null;
                _goneName = null;
                Say($"「{id} · {Get("menu_nm")}」 을(를) 추가했습니다.");

                // 방금 세운 줄을 찾아갈 길 — 211건 사이에 끼면 눈으로 못 찾는다.
                _filter.Q = id;
            }
            else
            {
                await Api.UpdateRowAsync(rid, _formKey, values);
                _formOpen = false;
                _savedName = Get("menu_nm");
                _addedName = null;
                _goneName = null;
                Say($"「{Get("menu_nm")}」 을(를) 고쳤습니다.");
            }
        }
        catch (ApiException ex)
        {
            Say($"저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return;
        }
        finally
        {
            _formBusy = false;
        }

        await SearchAsync();
    }

    private async Task DeleteFromFormAsync()
    {
        var row = _rows.FirstOrDefault(r => r.ActivityId == _formKey);
        if (row is null) return;

        _formOpen = false;
        await DeleteAsync(row);
    }

    // ──────────────────────────────────────────── 삭제

    private ConfirmDialog? _confirm;

    /// <summary>함께 사라질 일감·첨부를 세어 묻는다. 줄과 일감은 서버가 보관 표에 먼저 담는다.</summary>
    private async Task DeleteAsync(WbsBoardRowDto row)
    {
        if (ProjectRid is not int rid || row.ActivityId is null || _confirm is null) return;

        WbsBoardRowDepsDto? deps;
        try
        {
            deps = await Api.RowDepsAsync(rid, row.ActivityId);
        }
        catch (ApiException ex)
        {
            Say($"지울 대상을 확인하지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return;
        }

        var with = new List<string>();
        if (deps?.Tasks > 0) with.Add($"일감 {deps.Tasks}건");
        if (deps?.Files > 0) with.Add($"첨부 {deps.Files}건");

        var ok = await _confirm.AskAsync(
            $"「{row.ActivityId} · {row.MenuNm}」 을(를) 지웁니다."
            + (with.Count > 0 ? $"\n{string.Join(" · ", with)}도 함께 사라집니다." : "")
            + "\n줄과 일감은 보관 표(projmng.wbs_work_deleted)에 남습니다.",
            "대상 삭제");

        if (!ok) return;

        try
        {
            var gone = await Api.DeleteRowAsync(rid, row.ActivityId);
            _goneName = row.MenuNm;
            _goneDelId = gone?.DelId;
            _addedName = null;
            _savedName = null;

            if (row.FileGroupId is { Length: > 0 })
            {
                // 첨부는 FileServer 에 있어 원장과 함께 안 지워진다. 남은 것은 보관 번호로 찾는다.
                Say($"지웠습니다(보관 번호 {gone?.DelId}). 첨부 그룹 {row.FileGroupId} 은 파일 서버에 남아 있습니다.");
            }
            else
            {
                Say($"「{row.MenuNm}」 을(를) 지웠습니다(보관 번호 {gone?.DelId}"
                    + (gone?.Tasks > 0 ? $" · 일감 {gone.Tasks}건 함께" : "") + ").");
            }
        }
        catch (ApiException ex)
        {
            Say($"지우지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return;
        }

        await SearchAsync();
    }

    // ──────────────────────────────────────────── ID 지정

    private bool _idOpen;
    private WbsBoardRowDto? _idRow;
    private string? _idValue;

    /// <summary>보내기 전에 겹치는 ID 를 알려 준다. 서버도 같은 것을 막는다.</summary>
    private WbsBoardRowDto? IdClash => string.IsNullOrWhiteSpace(_idValue)
        ? null
        : _rows.FirstOrDefault(r => r != _idRow && string.Equals(r.ActivityId, _idValue.Trim(), StringComparison.OrdinalIgnoreCase));

    private void StartSetId(WbsBoardRowDto row)
    {
        _idRow = row;
        _idValue = null;
        _idOpen = true;
    }

    private async Task SaveIdAsync()
    {
        if (ProjectRid is not int rid || _idRow?.ActivityId is null || string.IsNullOrWhiteSpace(_idValue)) return;

        var ok = await RunAsync(
            () => Api.SetRowIdAsync(rid, _idRow.ActivityId, _idValue.Trim()),
            $"Activity ID 를 {_idValue.Trim()} 로 채웠습니다.", "ID 를 채우지 못했습니다");

        if (!ok) return;

        _idOpen = false;
        await SearchAsync();
    }

    // ──────────────────────────────────────────── 일감

    private WbsTaskPopup? _taskPopup;

    private Task OpenTasksAsync(WbsBoardRowDto row) => OpenTasksAsync(row, null, null);

    private Task OpenTasksAsync(WbsBoardRowDto row, string? div, string? reason)
        => _taskPopup?.OpenAsync(row, div, reason) ?? Task.CompletedTask;

    // ──────────────────────────────────────────── 첨부

    private bool _fileOpen;
    private WbsBoardRowDto? _fileRow;
    private IReadOnlyList<GroupFile> _files = [];
    private IReadOnlyList<PickedFile> _picked = [];
    private FilePicker? _picker;

    private async Task OpenFilesAsync(WbsBoardRowDto row)
    {
        _fileRow = row;
        _picked = [];

        try
        {
            _files = await Files.ListAsync(row.FileGroupId);
        }
        catch (ApiException ex)
        {
            _files = [];
            Say($"첨부를 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }

        _fileOpen = true;
    }

    private async Task UploadAsync()
    {
        if (ProjectRid is not int rid || _fileRow?.ActivityId is null || _picked.Count == 0) return;

        var streams = new List<Stream>();

        try
        {
            var payload = _picked.Select(p =>
            {
                var s = p.OpenRead();
                streams.Add(s);
                return (s, p.Name, (string?)p.ContentType);
            }).ToList();

            var result = await Files.UploadAsync(_fileRow.FileGroupId, payload, FileBizType);
            _files = await Files.ListAsync(result.GroupId);

            await Api.SetRowFilesAsync(rid, _fileRow.ActivityId, result.GroupId, _files.Count);
            _fileRow.FileGroupId = result.GroupId;
            _fileRow.FileCnt = _files.Count;

            Say($"{payload.Count}개를 올렸습니다.");
        }
        catch (ApiException ex)
        {
            Say($"올리지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return;
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
        }

        _picked = [];
        if (_picker is not null) await _picker.ClearAsync();
    }

    private async Task DeleteFileAsync(GroupFile file)
    {
        if (ProjectRid is not int rid || _fileRow?.ActivityId is null || _confirm is null) return;

        if (!await _confirm.AskAsync($"「{file.OriginalName}」 을(를) 지웁니다.\n되돌릴 수 없습니다.")) return;

        try
        {
            await Files.DeleteAsync(file.Id);
            _files = await Files.ListAsync(_fileRow.FileGroupId);

            await Api.SetRowFilesAsync(rid, _fileRow.ActivityId, _fileRow.FileGroupId, _files.Count);
            _fileRow.FileCnt = _files.Count;
        }
        catch (ApiException ex)
        {
            Say($"지우지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    private static string SizeText(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB",
    };

    // ──────────────────────────────────────────── ProjectView

    private static string StageTip(WbsBoardRowDto r)
    {
        if (string.IsNullOrEmpty(r.PvStatus))
            return r.PvTaskCode is null ? "ProjectView 캐시가 없습니다" : "현재단계는 아직 수집되지 않았습니다";

        return $"현재단계 {r.PvStatus}"
            + (r.PvStatusAt is null ? "" : $" · {r.PvStatusAt} 부터")
            + (r.PvStatusCnt > 1 ? $" · 일감이 {r.PvStatusCnt}종 단계로 갈려 있어 가장 나중 것을 보여 줍니다" : "")
            + (r.PvSnapshotAt is null ? "" : $" · {r.PvSnapshotAt} 수집");
    }
}
