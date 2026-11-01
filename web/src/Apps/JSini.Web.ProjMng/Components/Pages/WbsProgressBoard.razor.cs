using System.Globalization;
using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsProgressBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;
    [Inject] private WbsBoardTaskClient TaskApi { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope),
        SchSummary.NameOf(StatusOptions, o => o.Value, o => o.Text, _status));

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    private string? _projectCode;
    private string _scope = "dev";

    /// <summary>항목 목록의 진행 상태. 서버에 묻지 않고 받은 목록을 거른다 — 원본과 같이 상단 숫자는 그대로 두고 목록만 좁힌다.</summary>
    private string _status = "open";

    private WbsBoardProgressDto? _summary;
    private IReadOnlyList<WbsBoardProgressUserDto> _byPlan = [];
    private IReadOnlyList<WbsBoardProgressUserDto> _byReal = [];
    private IReadOnlyList<WbsBoardProgressModuleDto> _byModule = [];
    private List<WbsProgressItem> _rows = [];
    private Dictionary<string, WbsBoardTaskCountDto> _taskCounts = [];

    // 위 세 표에서 누른 줄. 하나만 걸린다 — 담당자와 모듈을 함께 걸면 어느 표가 목록을 좁혔는지 안 보인다.
    private string? _selUser;
    private string? _selReal;
    private string? _selModule;

    private CommGrd<WbsProgressItem>? _itemGrid;
    private WbsTaskPopup? _taskPopup;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private static readonly SchOption[] StatusOptions =
    [
        new("open", "착수(도래) & 미완료"),
        new("notyet", "미도래 (0%)"),
        new("done", "완료"),
        new("all", "전체"),
    ];

    private static readonly SchOption[] PvValueOptions =
    [
        new("plan", "계획진척률"),
        new("done", "완료율(0/100)"),
    ];

    private static string FullAtText => WbsPvProgressScript.FullAt.ToString("0", CultureInfo.InvariantCulture);
    private static string FullRateText => WbsPvProgressScript.FullRate.ToString("0", CultureInfo.InvariantCulture);

    private static string Rate(decimal? v) => v is null ? "-" : $"{v.Value:0.#}%";

    private static string Width(decimal? v) =>
        $"{Math.Clamp(v ?? 0, 0, 100).ToString("0.#", CultureInfo.InvariantCulture)}%";

    /// <summary>계획 대비 실적 격차. 클수록 계획보다 뒤처졌다.</summary>
    private static decimal? Gap(WbsBoardProgressUserDto u) =>
        u.PlanRate is null ? null : Math.Round(u.PlanRate.Value - (u.DoneRate ?? 0), 1);

    // ──────────────────────────────────────────── 조회

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _summary = null;
            _byPlan = [];
            _byReal = [];
            _byModule = [];
            _rows = [];
            _pvCache = [];
            return 0;
        }

        var summary = Api.ProgressAsync(rid, _scope);
        var byPlan = Api.ProgressByUserAsync(rid, _scope, "plan");
        var byReal = Api.ProgressByUserAsync(rid, _scope, "real");
        var byModule = Api.ProgressByModuleAsync(rid, _scope);
        var counts = TaskApi.CountsAsync(rid);
        var pv = LoadPvCacheAsync(rid);

        await Task.WhenAll(summary, byPlan, byReal, byModule, counts, pv);

        _summary = summary.Result;
        _byPlan = byPlan.Result;
        _byReal = byReal.Result;
        _byModule = byModule.Result;
        _taskCounts = ToCounts(counts.Result);

        return await LoadRowsAsync(rid);
    }, "진척률 자료가 없습니다.", "진척률을 읽지 못했습니다");

    private Task ReloadRowsAsync() => LoadAsync(
        () => ProjectRid is int rid ? LoadRowsAsync(rid) : Task.FromResult(0),
        "해당 조건의 항목이 없습니다", "항목을 읽지 못했습니다");

    private async Task<int> LoadRowsAsync(int rid)
    {
        var rows = await Api.ProgressRowsAsync(rid, _scope, _selUser, _selReal, _selModule);
        _rows = [.. rows.Select(r => new WbsProgressItem(r))];
        ApplyTaskCounts();
        return _rows.Count;
    }

    private static Dictionary<string, WbsBoardTaskCountDto> ToCounts(IReadOnlyList<WbsBoardTaskCountDto> counts) =>
        counts.Where(c => c.ActivityId is not null).GroupBy(c => c.ActivityId!).ToDictionary(g => g.Key, g => g.First());

    private void ApplyTaskCounts()
    {
        foreach (var r in _rows)
        {
            var hit = r.ActivityId is not null && _taskCounts.TryGetValue(r.ActivityId, out var c) ? c : null;
            r.TaskCnt = hit?.Cnt ?? 0;
            r.TaskDone = hit?.Done ?? 0;
        }
    }

    /// <summary>일감 창이 바꾼 뒤 — 목록 전체가 아니라 건수만 다시 받는다.</summary>
    private async Task ReloadTaskCountsAsync()
    {
        if (ProjectRid is not int rid) return;

        try
        {
            _taskCounts = ToCounts(await TaskApi.CountsAsync(rid));
            ApplyTaskCounts();
            StateHasChanged();
        }
        catch (ApiException ex)
        {
            Say($"일감 건수를 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    private Task OpenTasksAsync(WbsProgressItem r) =>
        _taskPopup?.OpenAsync(new WbsBoardRowDto
        {
            ActivityId = r.ActivityId,
            Systemcode = r.Systemcode,
            MenuNm = r.MenuNm,
        }, null, null) ?? Task.CompletedTask;

    // ──────────────────────────────────────────── 위 표에서 좁히기

    private bool HasSel => _selUser is not null || _selReal is not null || _selModule is not null;

    private bool IsPicked(string who, string? id) => id is not null && (who == "real" ? _selReal : _selUser) == id;

    private Task PickUserAsync(string who, string? id)
    {
        var again = IsPicked(who, id);
        _selUser = who == "plan" && !again ? id : null;
        _selReal = who == "real" && !again ? id : null;
        _selModule = null;
        return ReloadRowsAsync();
    }

    private Task PickModuleAsync(string? module)
    {
        var again = module is not null && _selModule == module;
        _selModule = again ? null : module;
        _selUser = null;
        _selReal = null;
        return ReloadRowsAsync();
    }

    private Task ClearSelAsync()
    {
        _selUser = _selReal = _selModule = null;
        return ReloadRowsAsync();
    }

    private string SelText =>
        _selUser is not null ? $"담당자 {_byPlan.FirstOrDefault(u => u.UserBpId == _selUser)?.UserNm ?? _selUser}"
        : _selReal is not null ? $"개발자 {_byReal.FirstOrDefault(u => u.UserBpId == _selReal)?.UserNm ?? _selReal}"
        : _selModule is not null ? $"모듈 {_selModule}"
        : "";

    /// <summary>진행 상태로 거른 목록. 착수는 「계획진척률이 0 보다 크다」 — 상세 목록의 status 필터와 같은 잣대다.</summary>
    private List<WbsProgressItem> StatusRows =>
    [
        .. _rows.Where(r => _status switch
        {
            "open" => (r.PlanRate ?? 0) > 0 && !r.Complate,
            "notyet" => (r.PlanRate ?? 0) <= 0,
            "done" => r.Complate,
            _ => true,
        })
    ];

    private string ItemsHint
    {
        get
        {
            var shown = StatusRows.Count;
            var text = shown == _rows.Count ? $"{shown}건" : $"{shown}건 / 전체 {_rows.Count}";
            return Loading ? text + " · 조회 중…" : text;
        }
    }

    private IReadOnlyList<GridCheckFilterOption> UserFilterOptions =>
        GridCheckFilter.Collect(_rows.Select(r => r.UserText), t => $"{t} · {_rows.Count(r => r.UserText == t)}");

    private IReadOnlyList<GridCheckFilterOption> RealFilterOptions =>
        GridCheckFilter.Collect(_rows.Select(r => r.UserRealText), t => $"{t} · {_rows.Count(r => r.UserRealText == t)}");

    /// <summary>지금 조건 그대로 [상세 목록]을 연다. 칸별 검색은 넘어가지 않는다 — 그 화면의 칸 구성이 다르다.</summary>
    private void Drill()
    {
        var q = new List<string> { $"scope={_scope}" };

        if (_status != "all") q.Add($"status={_status}");
        if (_selUser is not null) q.Add($"user={Uri.EscapeDataString(_selUser)}");
        if (_selReal is not null) q.Add($"realUser={Uri.EscapeDataString(_selReal)}");
        if (_selModule is not null) q.Add($"module={Uri.EscapeDataString(_selModule)}");

        Navigation.NavigateTo($"/projmng/wbs/rows?{string.Join('&', q)}");
    }

    // ──────────────────────────────────────────── 칸별 검색

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

    // ──────────────────────────────────────────── ProjectView 전송

    private string _pvValue = "plan";
    private bool _pvDiffOnly = true;
    private bool _pvOpen;
    private string _pvScript = "";
    private string _pvMeta = "";

    private IReadOnlyList<WbsBoardPvCacheDto> _pvCache = [];
    private string? _pvCacheAt;

    /// <summary>캐시는 없어도 되는 것이라 못 읽어도 조용히 넘어간다 — 스크립트가 목록 조회로 돌아간다.</summary>
    private async Task LoadPvCacheAsync(int rid)
    {
        try
        {
            _pvCache = [.. (await Api.ProgressPvCacheAsync(rid, _scope)).Where(c => c.ActivityId is not null && c.PvWorkId is not null)];
        }
        catch (ApiException)
        {
            _pvCache = [];
        }

        _pvCacheAt = _pvCache.Select(c => c.PvSnapshotAt).Where(a => a is not null).Max(StringComparer.Ordinal);
    }

    private decimal? PvRate(WbsProgressItem r) =>
        _pvValue == "done" ? (r.Complate ? 100 : 0) : r.PlanRate;

    /// <summary>지금 표에 보이는 줄 — 진행 상태와 칸별 검색을 모두 거친 것. 표가 아직 없으면 진행 상태만 거친 목록이다.</summary>
    private List<WbsProgressItem> VisibleRows()
    {
        if (_itemGrid?.Grid is not { } grid) return StatusRows;

        var list = new List<WbsProgressItem>();
        var count = grid.GetVisibleRowCount();

        for (var i = 0; i < count; i++)
        {
            if (grid.GetDataItem(i) is WbsProgressItem r) list.Add(r);
        }

        return list;
    }

    private void OpenPvScript()
    {
        var all = VisibleRows()
            .Where(r => r.ActivityId is not null && PvRate(r) is not null)
            .Select(r => (Code: r.ActivityId!, Rate: PvRate(r)!.Value))
            .ToList();

        // 캐시가 아는 지금 값과 같은 건은 보낼 까닭이 없다. 90% 이상은 100 으로 올라가 나가므로 비교도 올린 값으로 한다.
        var cur = _pvCache.Where(c => c.PvFinishRate is not null)
            .ToDictionary(c => c.ActivityId!, c => c.PvFinishRate!.Value);
        var changed = all.Where(i => !cur.TryGetValue(i.Code, out var v) || v != WbsPvProgressScript.ToSend(i.Rate)).ToList();

        var items = _pvDiffOnly && _pvCache.Count > 0 ? changed : all;
        var same = all.Count - changed.Count;
        var full = items.Count(i => i.Rate >= WbsPvProgressScript.FullAt);
        var label = _pvValue == "done" ? "완료율(0/100)" : "계획진척률";

        var ids = _pvCache.ToDictionary(c => c.ActivityId!, c => c.PvWorkId!);
        var projectId = _pvCache.Select(c => c.PvProjectId).FirstOrDefault(p => !string.IsNullOrEmpty(p));

        _pvScript = WbsPvProgressScript.Build(items, label, ids, projectId);
        _pvMeta = $"{items.Count}건 · {label} · {FullAtText}% 이상은 {FullRateText}%"
                  + (full > 0 ? $" ({FullRateText}% 전송 {full}건)" : "")
                  + (_pvDiffOnly && _pvCache.Count > 0 && same > 0 ? $" · 값이 같아 제외 {same}건" : "");
        _pvOpen = true;
    }

    private async Task CopyPvScriptAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("jsiniClipboard.copy", _pvScript);
            Say("복사했습니다. ProjectView 탭의 F12 → Console 에 붙여넣으세요.");
        }
        catch (JSException)
        {
            Say("자동 복사가 막혔습니다. 스크립트 칸을 눌러 전체 선택(Ctrl+A) 후 Ctrl+C 로 복사하세요.", NoticeTone.Warning);
        }
    }
}

/// <summary>항목별 진척률 표의 한 줄. 표가 거르고 고를 칸(참·거짓 · 미할당 묶음 · 일감 상태)을 원장 값 위에 얹는다.</summary>
public sealed class WbsProgressItem(WbsBoardProgressRowDto row)
{
    public string? ActivityId => row.ActivityId;
    public string? Systemcode => row.Systemcode;
    public string? MenuNm => row.MenuNm;
    public string? PlanSdt => row.PlanSdt;
    public string? PlanEdt => row.PlanEdt;
    public int? SpanDays => row.SpanDays;
    public int? PassedDays => row.PassedDays;
    public decimal? PlanRate => row.PlanRate;
    public bool Complate => row.ComplateYn == "o";
    public bool ComplateReal => row.ComplateRealYn == "o";
    public string? UserNm => NameOrNull(row.UserNm, row.UserBpId);
    public string? UserRealNm => NameOrNull(row.UserRealNm, row.UserRealId);

    /// <summary>담당자 칸의 거르는 글자. 빈 사람은 「(미할당)」으로 묶는다.</summary>
    public string UserText => UserNm ?? "(미할당)";

    /// <inheritdoc cref="UserText"/>
    public string UserRealText => UserRealNm ?? "(미할당)";

    public int TaskCnt { get; set; }
    public int TaskDone { get; set; }

    /// <summary>일감 칸의 상태 글자. 칸별 검색이 「미완료」·「완료」·「없음」으로 거른다.</summary>
    public string TaskState => TaskCnt == 0 ? "없음" : TaskDone < TaskCnt ? $"미완료 {TaskDone}/{TaskCnt}" : $"완료 {TaskDone}/{TaskCnt}";

    /// <summary>이름 칸. 아이디가 비면 사람이 없는 것이다 — 이름 채우기가 빈 아이디에 무엇을 돌려주든 미할당으로 본다.</summary>
    private static string? NameOrNull(string? name, string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : string.IsNullOrWhiteSpace(name) ? id : name;
}
