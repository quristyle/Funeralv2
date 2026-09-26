using DevExpress.Blazor;

using JSini.Web.Abstractions;
using JSini.Web.Admin.Api;
using JSini.Web.Components.Layout;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Company;

/// <summary>고른 회사의 부서 관리 탭.</summary>
public partial class CompanyDeptTab
{
    [Inject] private AdminClient Api { get; set; } = default!;

    [Parameter, EditorRequired] public string CompanyId { get; set; } = string.Empty;

    [Parameter] public string? CompanyName { get; set; }

    /// <summary>부서가 늘거나 줄었다. 화면이 회사 목록의 부서 수를 다시 읽는다.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    private IReadOnlyList<DeptDto> _depts = [];
    private string? _loadedFor;
    private bool _canMove;

    private bool _editing;
    private string? _editingId;
    private SaveDeptDto _form = new();

    private ConfirmDialog? _confirm;

    /// <summary>나무를 평평하게 편 것과 부서 → 상위 부서 표. 조회할 때마다 다시 만든다.</summary>
    private readonly List<DeptDto> _flat = [];
    private readonly Dictionary<string, string?> _parent = new(StringComparer.Ordinal);

    /// <summary>상위로 고를 수 있는 부서. 자기 자신과 자기 아래는 뺀다 — 나무가 고리가 된다.</summary>
    private List<DeptDto> ParentChoices =>
        _editingId is null
            ? [.. _flat]
            : [.. _flat.Where(d => !IsUnder(d.Id, _editingId))];

    private RenderFragment<DeptDto> RowActions => dept => builder =>
    {
        builder.OpenComponent<DeptRowActions>(0);
        builder.AddAttribute(1, nameof(DeptRowActions.Dept), dept);
        builder.AddAttribute(2, nameof(DeptRowActions.OnAddChild), EventCallback.Factory.Create<DeptDto>(this, OpenChild));
        builder.AddAttribute(3, nameof(DeptRowActions.OnEdit), EventCallback.Factory.Create<DeptDto>(this, OpenEdit));
        builder.AddAttribute(4, nameof(DeptRowActions.OnDelete), EventCallback.Factory.Create<DeptDto>(this, DeleteAsync));
        builder.CloseComponent();
    };

    protected override void OnInitialized() =>
        _canMove = Can(MenuAction.Update);

    protected override Task OnParametersSetAsync()
    {
        if (_loadedFor == CompanyId)
        {
            return Task.CompletedTask;
        }

        _loadedFor = CompanyId;
        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _depts = await Api.GetDeptsAsync(CompanyId);
        Index();

        return _flat.Count;
    }, "등록된 부서가 없습니다.", "부서 목록을 읽지 못했습니다");

    private void Index()
    {
        _flat.Clear();
        _parent.Clear();

        Walk(_depts, null);

        void Walk(IEnumerable<DeptDto> nodes, string? parentId)
        {
            foreach (var node in nodes)
            {
                _flat.Add(node);
                _parent[node.Id] = parentId;

                if (node.Children is { Count: > 0 })
                {
                    Walk(node.Children, node.Id);
                }
            }
        }
    }

    /// <summary><paramref name="id"/> 가 <paramref name="ancestorId"/> 자신이거나 그 하위인가.</summary>
    private bool IsUnder(string? id, string ancestorId)
    {
        var hops = 0;

        while (id is not null)
        {
            if (string.Equals(id, ancestorId, StringComparison.Ordinal))
            {
                return true;
            }

            // 자료가 이미 순환이면 돌지 않는다.
            if (++hops > _flat.Count)
            {
                return false;
            }

            id = _parent.GetValueOrDefault(id);
        }

        return false;
    }

    private List<DeptDto> Siblings(string? parentId) =>
        parentId is null
            ? [.. _depts]
            : [.. _flat.FirstOrDefault(d => d.Id == parentId)?.Children ?? []];

    /// <summary>
    /// 끌어 옮긴 결과를 저장한다. 떠난 묶음과 도착한 묶음의 순번을 다시 매겨
    /// 한 번에 보낸다 — 옮긴 한 건만 보내면 서버가 나머지를 짐작해야 한다.
    /// </summary>
    private async Task OnDroppedAsync(TreeListItemsDroppedEventArgs e)
    {
        if (e.DroppedItems.FirstOrDefault() is not DeptDto moved)
        {
            return;
        }

        var target = e.TargetItem as DeptDto;

        var inside = target is not null
            && e.DropPosition is TreeListItemDropPosition.Inside or TreeListItemDropPosition.Append;

        var newParentId = target is null ? null
            : inside ? target.Id
            : _parent.GetValueOrDefault(target.Id);

        if (newParentId is not null && IsUnder(newParentId, moved.Id))
        {
            Say("자기 자신이나 하위 부서 아래로는 옮길 수 없습니다.", NoticeTone.Warning);
            return;
        }

        var oldParentId = _parent.GetValueOrDefault(moved.Id);

        // 자리는 옮길 줄을 빼낸 뒤에 센다. 빼기 전에 세면 같은 묶음 안에서
        // 아래로 옮길 때 한 칸 어긋난다.
        var arrived = Siblings(newParentId).Where(d => d.Id != moved.Id).ToList();
        var at = arrived.Count;

        if (target is not null && !inside)
        {
            at = arrived.FindIndex(d => d.Id == target.Id);

            if (at < 0)
            {
                at = arrived.Count;
            }
            else if (e.DropPosition == TreeListItemDropPosition.After)
            {
                at++;
            }
        }

        arrived.Insert(at, moved);

        var items = new List<DeptOrderDto>();
        Number(newParentId, arrived);

        if (!string.Equals(oldParentId, newParentId, StringComparison.Ordinal))
        {
            Number(oldParentId, Siblings(oldParentId).Where(d => d.Id != moved.Id).ToList());
        }

        if (await RunAsync(() => Api.ReorderDeptsAsync(items),
                $"「{moved.Name}」 을(를) 옮겼습니다.", "옮기지 못했습니다"))
        {
            await ReloadAsync();
        }

        // 1 부터 매긴다. 0 은 새로 만든 부서가 맨 앞에 끼어들 자리로 비워 둔다.
        void Number(string? parentId, List<DeptDto> rows)
            => items.AddRange(rows.Select((d, i) => new DeptOrderDto
            {
                Id = d.Id,
                Pid = parentId,
                SortOrder = i + 1,
            }));
    }

    private void OpenNew() => Open(null, new SaveDeptDto { CompanyId = CompanyId, Status = 1 });

    private void OpenChild(DeptDto parent) =>
        Open(null, new SaveDeptDto { Pid = parent.Id, CompanyId = CompanyId, Status = 1 });

    private void OpenEdit(DeptDto dept) => Open(dept.Id, new SaveDeptDto
    {
        Name = dept.Name,
        Pid = dept.Pid,
        CompanyId = CompanyId,
        Remark = dept.Remark,
        Status = dept.Status,
        SortOrder = dept.SortOrder,
    });

    private void Open(string? id, SaveDeptDto form)
    {
        _editingId = id;
        _form = form;
        _editing = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.Name))
        {
            Say("부서명을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var isNew = _editingId is null;

        var saved = await RunAsync(
            () => isNew ? Api.CreateDeptAsync(_form) : Api.UpdateDeptAsync(_editingId!, _form),
            isNew ? "등록했습니다." : "저장했습니다.",
            isNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (!saved)
        {
            return;
        }

        _editing = false;
        await ReloadAsync();

        if (isNew)
        {
            await OnChanged.InvokeAsync();
        }
    }

    /// <summary>하위 부서나 소속 인원이 남아 있으면 서버가 막는다.</summary>
    private async Task DeleteAsync(DeptDto dept)
    {
        var ok = await _confirm!.AskAsync($"부서 「{dept.Name}」 을(를) 지웁니다.\n되돌릴 수 없습니다.");

        if (!ok)
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteDeptAsync(dept.Id),
                $"{dept.Name} 을(를) 지웠습니다.", "지우지 못했습니다"))
        {
            await ReloadAsync();
            await OnChanged.InvokeAsync();
        }
    }
}
