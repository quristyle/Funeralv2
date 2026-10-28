using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class WbsTaskPopup
{
    [Inject] private WbsBoardTaskClient TaskApi { get; set; } = default!;

    [Parameter] public int? ProjectRid { get; set; }

    /// <summary>일감을 더하거나 고치거나 지운 뒤 — 목록의 건수 칸을 다시 세라는 뜻이다.</summary>
    [Parameter] public EventCallback Changed { get; set; }

    private bool _open;
    private bool _busy;
    private WbsBoardRowDto? _row;
    private string? _reason;
    private IReadOnlyList<WbsBoardTaskDto> _tasks = [];

    private string _newDiv = "개발";
    private string? _newMemo;

    private int? _editId;
    private string? _editDiv;
    private string? _editMemo;

    private string? _doneFilter = "n";
    private string? _divFilter;
    private string? _textFilter;

    private ConfirmDialog? _confirm;

    private static readonly SchOption[] DoneFilters = [new(null, "완료 전체"), new("y", "완료"), new("n", "미완료")];

    private SchOption[] DivFilters =>
    [
        new(null, "구분 전체"),
        .. _tasks.Select(t => t.TaskDiv ?? "").Distinct().Order().Select(d => new SchOption(d, d.Length == 0 ? "(없음)" : d))
    ];

    private string Title => _row is null ? "일감" : $"일감 · {_row.Systemcode} · {_row.MenuNm} · {_row.ActivityId}";

    /// <summary>걸러서 보이는 일감. 미완료가 먼저, 그 안에서는 늦게 고친 것이 위다.</summary>
    private List<WbsBoardTaskDto> Shown =>
    [
        .. _tasks
            .Where(t => _doneFilter switch { "y" => t.Done, "n" => !t.Done, _ => true })
            .Where(t => _divFilter is null || (t.TaskDiv ?? "") == _divFilter)
            .Where(t => string.IsNullOrWhiteSpace(_textFilter)
                        || Plain(t.Memo).Contains(_textFilter.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Done)
            .ThenByDescending(t => t.UpdatedAt, StringComparer.Ordinal)
    ];

    /// <summary>메모는 HTML 이라 검색할 때는 태그를 벗긴 글자를 본다.</summary>
    private static string Plain(string? html)
        => System.Net.WebUtility.HtmlDecode(Regex.Replace(html ?? "", "<[^>]*>", " "));

    private static bool IsBlank(string? html)
        => !(html ?? "").Contains("<img", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Plain(html));

    /// <summary>창을 연다. 열기 전에 목록을 읽는다 — 부모가 여는 창은 VisibleChanged 가 안 울어, 열린 뒤에 읽으면 늘 빈 채로 선다.</summary>
    public async Task OpenAsync(WbsBoardRowDto row, string? div, string? reason)
    {
        _row = row;
        _reason = reason;
        _newDiv = div ?? "개발";
        _newMemo = null;
        _editId = null;
        _doneFilter = div is null ? "n" : null;
        _divFilter = null;
        _textFilter = null;

        await LoadAsync();
        _open = true;
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        if (ProjectRid is not int rid || _row?.ActivityId is null)
        {
            _tasks = [];
            return;
        }

        try
        {
            _tasks = await TaskApi.ListAsync(rid, _row.ActivityId);
        }
        catch (ApiException ex)
        {
            _tasks = [];
            Say($"일감을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
    }

    private async Task AfterChangeAsync()
    {
        await LoadAsync();
        await Changed.InvokeAsync();
    }

    private async Task AddAsync()
    {
        if (ProjectRid is not int rid || _row?.ActivityId is null) return;

        if (IsBlank(_newMemo))
        {
            Say("메모를 적으세요.", NoticeTone.Warning);
            return;
        }

        _busy = true;
        var ok = await RunAsync(
            () => TaskApi.CreateAsync(rid, new WbsBoardTaskDto { ActivityId = _row.ActivityId, TaskDiv = _newDiv, Memo = _newMemo }),
            "일감을 더했습니다.", "일감을 더하지 못했습니다");
        _busy = false;

        if (!ok) return;

        _newMemo = null;
        _reason = null;
        await AfterChangeAsync();
    }

    private async Task ToggleDoneAsync(WbsBoardTaskDto t)
    {
        if (ProjectRid is not int rid) return;

        _busy = true;
        var ok = await RunAsync(
            () => TaskApi.UpdateAsync(rid, t.TaskId, new Dictionary<string, object?> { ["doneYn"] = t.Done ? "" : "o" }),
            t.Done ? "미완료로 되돌렸습니다." : "완료로 표시했습니다.", "고치지 못했습니다");
        _busy = false;

        if (ok) await AfterChangeAsync();
    }

    private void StartEdit(WbsBoardTaskDto t)
    {
        _editId = t.TaskId;
        _editDiv = t.TaskDiv;
        _editMemo = t.Memo;
    }

    private async Task SaveEditAsync(WbsBoardTaskDto t)
    {
        if (ProjectRid is not int rid) return;

        if (IsBlank(_editMemo))
        {
            Say("메모를 적으세요.", NoticeTone.Warning);
            return;
        }

        _busy = true;
        var ok = await RunAsync(
            () => TaskApi.UpdateAsync(rid, t.TaskId, new Dictionary<string, object?> { ["taskDiv"] = _editDiv, ["memo"] = _editMemo }),
            "일감을 고쳤습니다.", "고치지 못했습니다");
        _busy = false;

        if (!ok) return;

        _editId = null;
        await AfterChangeAsync();
    }

    private async Task DeleteAsync(WbsBoardTaskDto t)
    {
        if (ProjectRid is not int rid || _confirm is null) return;

        var preview = Plain(t.Memo).Trim();
        if (preview.Length > 60) preview = preview[..60] + "…";

        if (!await _confirm.AskAsync($"「{t.TaskDiv} · {preview}」 을(를) 지웁니다.\n되돌릴 수 없습니다.")) return;

        if (await RunAsync(() => TaskApi.DeleteAsync(rid, t.TaskId), "일감을 지웠습니다.", "지우지 못했습니다"))
            await AfterChangeAsync();
    }
}
