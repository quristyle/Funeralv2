using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsRowList
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;
    [Inject] private WbsBoardTaskClient TaskApi { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _filter.Basis),
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _filter.Scope),
        SchSummary.NameOf(_userOptions, o => o.Value, o => o.Text, _filter.User, string.Empty),
        SchSummary.NameOf(_moduleOptions, o => o.Value, o => o.Text, _filter.Module, string.Empty),
        SchSummary.NameOf(DoneOptions, o => o.Value, o => o.Text, _filter.Done, string.Empty),
        SchSummary.NameOf(LateOptions, o => o.Value, o => o.Text, _filter.Late, string.Empty),
        SchSummary.NameOf(StatusOptions, o => o.Value, o => o.Text, _filter.Status, string.Empty),
        _filter.Q);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;
    private readonly WbsBoardFilter _filter = new() { Basis = "edt", Scope = "dev" };

    private IReadOnlyList<WbsBoardRowDto> _rows = [];
    private SchOption[] _userOptions = [];
    private SchOption[] _moduleOptions = [];

    private bool _taskOpen;
    private WbsBoardRowDto? _taskRow;
    private IReadOnlyList<WbsBoardTaskDto> _tasks = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string Hint =>
        $"{_rows.Count(r => r.ComplateYn == "o")} / {_rows.Count}건 완료"
        + (_rows.Count(r => r.StartLate || r.FinishLate) is var late && late > 0 ? $" · 지연 {late}건" : "");

    private string TaskTitle => _taskRow is null ? "일감" : $"일감 · {_taskRow.MenuNm}";

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

    /// <summary>
    /// 다른 화면에서 실려 온 조건을 먼저 읽는다.
    /// </summary>
    /// <remarks>
    /// <b>화면이 스스로 정한 기본값보다 주소가 이긴다.</b> 안 그러면 차트에서
    /// 눌러 넘어왔는데 조건이 안 걸린 전체 목록이 뜬다.
    /// </remarks>
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

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _rows = [];
            return 0;
        }

        // 목록·일감 건수·고르개 셋을 나란히 읽는다. 일감 건수는 줄마다 묻지
        // 않으려고 한 번에 받는 값이다(머리말).
        var rows = Api.RowsAsync(rid, _filter);
        var counts = TaskApi.CountsAsync(rid);
        var users = Api.UsersAsync(rid, _filter.Scope, _filter.Basis, _filter.Month, _filter.Week);
        var modules = Api.ByModuleAsync(rid, _filter.Basis, _filter.Scope);

        await Task.WhenAll(rows, counts, users, modules);

        _rows = rows.Result;

        var byActivity = counts.Result
            .Where(c => c.ActivityId is not null)
            .ToDictionary(c => c.ActivityId!, c => c);

        foreach (var row in _rows)
        {
            if (row.ActivityId is not null && byActivity.TryGetValue(row.ActivityId, out var c))
            {
                row.TaskCnt = c.Cnt;
                row.TaskDone = c.Done;
            }
        }

        _userOptions =
        [
            new(null, "전체"),
            .. users.Result.Select(u => new SchOption(u.UserBpId, $"{u.UserNm} ({u.Cnt})"))
        ];

        _moduleOptions =
        [
            new(null, "전체"),
            .. modules.Result.Select(m => new SchOption(m.Systemcode, $"{m.Systemcode} {m.SystemNm}"))
        ];

        return _rows.Count;
    }, "조건에 맞는 화면이 없습니다.", "목록을 읽지 못했습니다");

    /// <summary>
    /// 고친 칸만 보낸다. <b>편집 창에 없는 칸은 담지 않는다</b> — 서버가
    /// 화이트리스트로 한 번 더 거르지만, 보내지 않는 편이 뜻이 분명하다.
    /// </summary>
    private async Task SaveAsync((WbsBoardRowDto Item, bool IsNew) e)
    {
        if (ProjectRid is not int rid || e.Item.ActivityId is null) return;

        var patch = new Dictionary<string, object?>
        {
            ["menu_nm"] = e.Item.MenuNm,
            ["systemcode"] = e.Item.Systemcode,
            ["system_nm"] = e.Item.SystemNm,
            ["user_bp_id"] = e.Item.UserBpId,
            ["user_real_id"] = e.Item.UserRealId,
            ["complate_yn"] = e.Item.ComplateYn,
            ["complate_real_yn"] = e.Item.ComplateRealYn,
            ["recheck_yn"] = e.Item.RecheckYn,
            ["complate_big_yn"] = e.Item.ComplateBigYn,
            ["complate_real_big_yn"] = e.Item.ComplateRealBigYn,
            ["db_ready_big_yn"] = e.Item.DbReadyBigYn,
            ["priority_order"] = e.Item.PriorityOrder,
            ["prog_type"] = e.Item.ProgType,
            ["prog_type_desc"] = e.Item.ProgTypeDesc,
        };

        await Api.PatchRowAsync(rid, e.Item.ActivityId, patch);
    }

    // ──────────────────────────────────────────── 일감

    private async Task OpenTasksAsync(WbsBoardRowDto row)
    {
        _taskRow = row;
        _taskOpen = true;

        await LoadTasksAsync();
    }

    private Task LoadTasksAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid || _taskRow?.ActivityId is null)
        {
            _tasks = [];
            return 0;
        }

        _tasks = await TaskApi.ListAsync(rid, _taskRow.ActivityId);
        return _tasks.Count;
    }, "일감이 없습니다.", "일감을 읽지 못했습니다");

    private void FillNewTask(WbsBoardTaskDto task) => task.ActivityId = _taskRow?.ActivityId;

    private async Task SaveTaskAsync((WbsBoardTaskDto Item, bool IsNew) e)
    {
        if (ProjectRid is not int rid) return;

        if (e.IsNew)
        {
            await TaskApi.CreateAsync(rid, e.Item);
        }
        else
        {
            await TaskApi.UpdateAsync(rid, e.Item.TaskId, new Dictionary<string, object?>
            {
                ["taskDiv"] = e.Item.TaskDiv,
                ["memo"] = e.Item.Memo,
                ["doneYn"] = e.Item.DoneYn,
            });
        }

        // 건수가 목록의 칸에 걸려 있어 함께 다시 읽는다.
        await SearchAsync();
    }

    private async Task DeleteTaskAsync(WbsBoardTaskDto task)
    {
        if (ProjectRid is not int rid) return;

        await TaskApi.DeleteAsync(rid, task.TaskId);
        await SearchAsync();
    }
}
