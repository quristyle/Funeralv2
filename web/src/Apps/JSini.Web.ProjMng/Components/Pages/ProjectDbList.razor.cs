using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ProjectDbList
{
    [Inject] private ProjectDbClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private IReadOnlyList<ProjectDbDto> _dbs = [];
    private IReadOnlyList<ProjectDbPropDto> _props = [];

    private string? _projectCode;
    private ProjectDbDto? _db;
    private ProjectDbPropDto? _prop;

    /// <summary>
    /// 편집기에 열려 있는 값. <b>속성 줄의 값을 그대로 쓰지 않는다</b> —
    /// 타이핑 중에 줄이 다시 읽히면 방금 친 것이 사라진다. 저장할 때 옮긴다.
    /// </summary>
    private string? _value;

    /// <summary>
    /// 끌어 옮길 수 있는가. <b>「수정」 단추를 감추는 것과 같은 판정</b>이다 —
    /// 갈라지면 「단추는 없는데 끌어 옮기기는 된다」가 된다.
    /// </summary>
    private bool _canReorder;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string ValueHint =>
        _prop is null ? string.Empty : $"{(_value ?? string.Empty).Length:#,##0}자";

    /// <summary>표에 보여 줄 값의 앞머리. 여러 줄이면 첫 줄만.</summary>
    private static string Head(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var line = value.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? string.Empty;
        return line.Length <= 80 ? line : line[..80] + "…";
    }

    protected override Task OnInitializedAsync()
    {
        // `PermissionView` 와 **똑같이** 묻는다. 끌어 옮기기는 그릴지 말지가
        // 아니라 표의 파라미터로 켜고 끄는 것이라 직접 판정한다.
        _canReorder = Can(MenuAction.Update);

        return SearchAsync();
    }

    /// <summary>
    /// 줄을 끌어다 놓았다. <b>놓는 즉시 저장한다</b>(머리말).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>표는 스스로 줄을 옮기지 않는다.</b> 우리가 손에 든 목록에서 옮겨
    /// 새 차례를 만들고, 그것을 통째로 서버에 보낸 뒤 다시 읽는다 —
    /// 저장이 실패하면 화면이 서버의 차례로 되돌아가야 하기 때문이다.
    /// </para>
    ///
    /// <para>
    /// 옮긴 줄 하나만 보내지 않는다. 서버는 「이 줄들이 지금 차지한 자리」를
    /// 모아 다시 나눠 주므로 <b>보이는 줄 전부</b>를 알아야 한다
    /// (<c>ProjectDbService.ReorderAsync</c>).
    /// </para>
    /// </remarks>
    private async Task OnRowsDroppedAsync(GridItemsDroppedEventArgs e)
    {
        if (e.DroppedItems.FirstOrDefault() is not ProjectDbDto moved
            || e.TargetItem is not ProjectDbDto target
            || ReferenceEquals(moved, target))
        {
            return;
        }

        var order = _dbs.ToList();
        var from = order.IndexOf(moved);

        if (from < 0)
        {
            return;
        }

        order.RemoveAt(from);

        // **뽑아낸 뒤에 자리를 찾는다.** 먼저 찾아 두면 위에서 아래로 옮길 때
        // 목표의 자리가 하나씩 밀려 한 칸 어긋난다.
        var at = order.IndexOf(target);

        if (at < 0)
        {
            return;
        }

        order.Insert(e.DropPosition == GridItemDropPosition.Before ? at : at + 1, moved);

        await RunAsync(
            () => Api.ReorderAsync([.. order.Select(d => d.DbRid)]),
            "차례를 바꿨습니다.", "차례를 바꾸지 못했습니다");

        // **성공이든 실패든 되읽는다.** 표는 놓는 순간 이미 줄을 옮겨 그렸다 —
        // 실패했으면 서버의 차례로 되돌려야 하고, 성공했으면 새 숫자를 받아야
        // 「순서」 칸이 화면과 맞는다.
        await SearchAsync();
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _dbs = await Api.ListAsync(ProjectRid);

        if (_db is not null && _dbs.All(d => d.DbRid != _db.DbRid))
        {
            ClearDb();
        }

        return _dbs.Count;
    }, "등록된 접속이 없습니다.", "DB 목록을 읽지 못했습니다");

    private void ClearDb()
    {
        _db = null;
        _props = [];
        ClearProp();
    }

    private void ClearProp()
    {
        _prop = null;
        _value = null;
    }

    private Task PickDbAsync(ProjectDbDto? db)
    {
        _db = db;
        ClearProp();

        if (db is null)
        {
            _props = [];
            return Task.CompletedTask;
        }

        return LoadPropsAsync();
    }

    private Task LoadPropsAsync() => _db is null ? Task.CompletedTask : LoadAsync(async () =>
    {
        _props = await Api.PropsAsync(_db.DbRid);

        // 고른 속성이 목록에서 빠졌으면 편집기를 비운다.
        if (_prop is not null && _props.All(p => p.DbPrid != _prop.DbPrid))
        {
            ClearProp();
        }

        return _props.Count;
    }, "이 접속에 속성이 없습니다.", "속성을 읽지 못했습니다");

    private Task PickPropAsync(ProjectDbPropDto? prop)
    {
        _prop = prop;
        _value = prop?.DbPvalue ?? string.Empty;

        return Task.CompletedTask;
    }

    private void FillNewDb(ProjectDbDto d)
    {
        d.PrjRid = ProjectRid;
        d.DbType = "POSTGRESQL";
    }

    private async Task SaveDbAsync((ProjectDbDto Item, bool IsNew) e)
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

    private async Task DeleteDbAsync(ProjectDbDto d)
    {
        await Api.DeleteAsync(d.DbRid);

        if (_db?.DbRid == d.DbRid)
        {
            ClearDb();
        }
    }

    /// <summary>
    /// 새 속성은 <b>고른 접속에 붙는다.</b> 값은 비워 둔다 — 편집기에서 채운다.
    /// </summary>
    private void FillNewProp(ProjectDbPropDto p) => p.DbRid = _db?.DbRid ?? 0;

    private async Task SavePropAsync((ProjectDbPropDto Item, bool IsNew) e)
    {
        if (_db is null)
        {
            return;
        }

        if (e.IsNew)
        {
            await Api.CreatePropAsync(_db.DbRid, e.Item);
        }
        else
        {
            // 편집 창은 값을 다루지 않는다. 표의 줄에 남아 있는 값을 그대로
            // 실어 보낸다 — 안 실으면 이름만 고쳐도 값이 지워진다.
            await Api.UpdatePropAsync(_db.DbRid, e.Item);
        }
    }

    private async Task DeletePropAsync(ProjectDbPropDto p)
    {
        if (_db is null)
        {
            return;
        }

        await Api.DeletePropAsync(_db.DbRid, p.DbPrid);

        if (_prop?.DbPrid == p.DbPrid)
        {
            ClearProp();
        }
    }

    /// <summary>편집기 내용을 그 속성 한 건에 저장한다(옛 <c>OnSaveWrk</c>).</summary>
    private async Task SaveValueAsync()
    {
        if (_db is null || _prop is null)
        {
            return;
        }

        _prop.DbPvalue = _value ?? string.Empty;

        var saved = await RunAsync(
            () => Api.UpdatePropAsync(_db.DbRid, _prop),
            "값을 저장했습니다.", "값을 저장하지 못했습니다");

        if (saved)
        {
            // 표의 앞머리와 수정 시각이 방금 바뀌었다.
            await LoadPropsAsync();
        }
    }
}
