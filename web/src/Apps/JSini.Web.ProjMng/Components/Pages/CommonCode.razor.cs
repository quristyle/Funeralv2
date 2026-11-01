using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class CommonCode
{
    [Inject] private DevCommonCodeClient Api { get; set; } = default!;
    [Inject] private ProjectDbClient Dbs { get; set; } = default!;

    /// <summary>DB 고르개 한 칸. <see cref="DbRid"/> 가 비면 공용이다.</summary>
    private sealed record DbOption(int? DbRid, string Text);

    /// <summary>프로젝트와 무관하게 늘 맨 앞에 선다 — 이 도구의 드롭다운이 읽는 코드가 여기 있다.</summary>
    private static readonly DbOption SharedDb = new(null, "공용 · 프로젝트관리");

    private string? _projectCode;
    private string? _projectName;

    private IReadOnlyList<DbOption> _dbs = [SharedDb];
    private DbOption _db = SharedDb;

    private IReadOnlyList<DevCommonCodeDto> _groups = [];
    private IReadOnlyList<DevCommonCodeDto> _codes = [];

    private DevCommonCodeDto? _group;

    private string ConditionSummary => SchSummary.Of(_projectName, _db.Text);

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    protected override Task OnInitializedAsync() => LoadGroupsAsync();

    /// <summary>프로젝트를 바꾸면 그 프로젝트의 DB 를 다시 세운다. 공용을 보고 있었으면 그대로 두고, 아니면 첫 DB 로 옮긴다.</summary>
    private async Task LoadDbsAsync()
    {
        IReadOnlyList<ProjectDbDto> rows = [];

        if (ProjectRid is { } prj)
        {
            await LoadAsync(async () =>
            {
                rows = await Dbs.ListAsync(prj);
                return rows.Count;
            }, "이 프로젝트에 등록된 DB 가 없습니다.", "DB 목록을 읽지 못했습니다");
        }

        _dbs = [SharedDb, .. rows.Select(r => new DbOption(r.DbRid, DbText(r)))];

        var next = _db.DbRid is null
            ? SharedDb
            : _dbs.FirstOrDefault(d => d.DbRid == _db.DbRid) ?? _dbs.ElementAtOrDefault(1) ?? SharedDb;

        if (next != _db)
        {
            await PickDbAsync(next);
        }
    }

    /// <summary>별칭만으로는 같은 DB 의 스키마가 안 갈린다(lsmnm_scom · lsmnm_smd …) — DB 이름과 스키마를 함께 적는다.</summary>
    private static string DbText(ProjectDbDto r)
    {
        var where = string.Join('.', new[] { r.DbDatabase, r.DbSchema }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrEmpty(where) ? r.DbNick ?? $"#{r.DbRid}" : $"{r.DbNick} · {where}";
    }

    private Task PickDbAsync(DbOption? db)
    {
        _db = db ?? SharedDb;
        _group = null;
        _codes = [];

        return LoadGroupsAsync();
    }

    private Task LoadGroupsAsync() => LoadAsync(async () =>
    {
        _groups = await Api.GroupsAsync(_db.DbRid);

        // 고른 묶음이 사라졌으면(지웠거나) 오른쪽을 비운다.
        if (_group is not null && _groups.All(g => g.CmRid != _group.CmRid))
        {
            _group = null;
            _codes = [];
        }

        return _groups.Count;
    }, "등록된 묶음이 없습니다.", "묶음을 읽지 못했습니다");

    private Task PickGroupAsync(DevCommonCodeDto? group)
    {
        _group = group;

        if (group is null)
        {
            _codes = [];
            return Task.CompletedTask;
        }

        return LoadCodesAsync();
    }

    private Task LoadCodesAsync() => _group is null ? Task.CompletedTask : LoadAsync(async () =>
    {
        _codes = await Api.ChildrenAsync(_group.CmCd ?? string.Empty, _db.DbRid);
        return _codes.Count;
    }, "이 묶음에 코드가 없습니다.", "코드를 읽지 못했습니다");

    /// <summary>새 묶음은 상위 코드가 없다 — 그것이 묶음의 정의다. 고른 DB 에 붙는다.</summary>
    private void FillNewGroup(DevCommonCodeDto c)
    {
        c.CmPcd = null;
        c.DbRid = _db.DbRid;
    }

    /// <summary>새 코드는 고른 묶음과 그 DB 에 붙는다. 사용자가 적게 두면 어느 묶음에도 안 붙어 어느 화면에도 안 나온다.</summary>
    private void FillNewCode(DevCommonCodeDto c)
    {
        c.CmPcd = _group?.CmCd;
        c.DbRid = _db.DbRid;
    }

    private async Task SaveAsync((DevCommonCodeDto Item, bool IsNew) e)
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

    /// <summary>묶음 삭제. 딸린 코드가 있으면 서버가 막는다.</summary>
    private async Task DeleteGroupAsync(DevCommonCodeDto g)
    {
        await Api.DeleteAsync(g.CmRid, g.CmCd);

        if (_group?.CmRid == g.CmRid)
        {
            _group = null;
            _codes = [];
        }
    }

    private Task DeleteCodeAsync(DevCommonCodeDto c) => Api.DeleteAsync(c.CmRid);
}
