using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class DeptList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        SchSummary.NameOf(_companies, c => c.Id, c => c.Name, _companyFilter);

    private sealed record StatusOption(int Code, string Name);

    private static readonly StatusOption[] Statuses =
    [
        new(1, "사용"),
        new(0, "중지"),
    ];

    private IReadOnlyList<DeptDto> _depts = [];
    private IReadOnlyList<CompanyDto> _companies = [];

    private string? _companyFilter;

    private bool _editing;
    private string? _editingId;
    private SaveDeptDto _form = new();

    /// <summary>이 화면에서 부서를 끌어 옮길 수 있는가. 관리 칸의 「수정」과 같은 판정이다 — PermissionView 를 쓸 수 없어 직접 묻고, 끌기는 CommTree 의 파라미터로 켜고 끈다.</summary>
    private bool _canMove;

    /// <summary>나무를 평평하게 편 것. 부모 찾기와 형제 세기에 쓴다.</summary>
    private readonly List<DeptDto> _flat = [];

    /// <summary>부서 → 상위 부서. 최상위는 <c>null</c>.</summary>
    private readonly Dictionary<string, string?> _parent = new(StringComparer.Ordinal);

    /// <summary>회사로 거른 나무. 부모가 걸리면 자식도 함께 남는다.</summary>
    private IReadOnlyList<DeptDto> Shown =>
        string.IsNullOrEmpty(_companyFilter)
            ? _depts
            : [.. _depts.Where(d => string.Equals(d.CompanyId, _companyFilter, StringComparison.Ordinal))];

    /// <summary>상위로 고를 수 있는 부서. 자기 자신과 자기 아래 전부를 뺀다 — 자기 아래로 옮기면 나무가 고리가 되어 목록이 사라진다.</summary>
    private List<DeptDto> ParentChoices
    {
        get
        {
            if (_editingId is null)
            {
                return [.. _flat];
            }

            var banned = new HashSet<string>(StringComparer.Ordinal) { _editingId };
            CollectDescendants(_depts, _editingId, banned);

            return [.. _flat.Where(d => !banned.Contains(d.Id))];
        }
    }

    protected override Task OnInitializedAsync()
    {
        // PermissionView 와 같이 묻는다 — 프리렌더 때는 false 이고, 회로가 붙어 화면이 다시 만들어지면 여기도 다시 돈다.
        _canMove = Can(MenuAction.Update);

        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 회사 목록은 부서와 무관하다. 나란히 부른다.
        var depts = Api.GetDeptsAsync();
        var companies = Api.GetCompaniesAsync();

        await Task.WhenAll(depts, companies);

        _depts = depts.Result;
        _companies = companies.Result;

        Index();

        return _depts.Count;
    }, "등록된 부서가 없습니다.", "부서 목록을 읽지 못했습니다");

    /// <summary>평평한 목록과 부모 표를 다시 만든다. 조회할 때마다 부른다.</summary>
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

    /// <summary>끌어 옮긴 결과를 저장한다. 화면이 배치를 확정해서 보낸다 — 떠난 묶음과 도착한 묶음을 다시 번호 붙여 한 번에 보낸다. 자리 계산은 옮길 줄을 뺀 뒤의 형제 목록에서 한다.</summary>
    private async Task OnDroppedAsync(TreeListItemsDroppedEventArgs e)
    {
        if (e.DroppedItems.FirstOrDefault() is not DeptDto moved)
        {
            return;
        }

        var target = e.TargetItem as DeptDto;

        // 부서는 회사에 딸린 것이라 회사를 넘나들면 안 된다 — 서버도 막지만 왕복 전에 말해 준다.
        if (target is not null
            && !string.Equals(target.CompanyId, moved.CompanyId, StringComparison.Ordinal))
        {
            Say("다른 회사의 부서 자리로는 옮길 수 없습니다.", NoticeTone.Warning);
            return;
        }

        // 줄 **위**에 놓았으면 그 부서의 하위가 된다. 줄 **사이**면 형제다.
        var inside = target is not null
            && e.DropPosition is TreeListItemDropPosition.Inside or TreeListItemDropPosition.Append;

        var newParentId = target is null ? null
            : inside ? target.Id
            : _parent.GetValueOrDefault(target.Id);

        // 자기 자신이나 하위 아래로는 옮길 수 없다 — 서버도 막지만 왕복 전에 말해 준다.
        if (newParentId is not null && IsUnder(newParentId, moved.Id))
        {
            Say("자기 자신이나 하위 부서 아래로는 옮길 수 없습니다.", NoticeTone.Warning);
            return;
        }

        var oldParentId = _parent.GetValueOrDefault(moved.Id);
        var sameParent = string.Equals(oldParentId, newParentId, StringComparison.Ordinal);

        var arrived = Siblings(newParentId, moved.CompanyId).Where(d => d.Id != moved.Id).ToList();
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

        if (!sameParent)
        {
            Number(oldParentId,
                Siblings(oldParentId, moved.CompanyId).Where(d => d.Id != moved.Id).ToList());
        }

        if (await RunAsync(() => Api.ReorderDeptsAsync(items),
                $"「{moved.Name}」 을(를) 옮겼습니다.", "옮기지 못했습니다"))
        {
            await ReloadAsync();
        }

        // 1 부터 매긴다 — 0 은 정하지 않은 값의 기본값이라 새 부서가 맨 앞에 끼어들지 않도록 비워 둔다.
        void Number(string? parentId, List<DeptDto> rows)
            => items.AddRange(rows.Select((d, i) => new DeptOrderDto
            {
                Id = d.Id,
                Pid = parentId,
                SortOrder = i + 1,
            }));
    }

    /// <summary>그 부모의 자식들. 최상위는 같은 회사의 뿌리다 — 조건이 전체일 때 뿌리 목록에는 회사가 여럿 섞여, 번호를 다시 매기면 보지도 않은 회사의 차례가 바뀐다.</summary>
    private List<DeptDto> Siblings(string? parentId, string? companyId) =>
        parentId is null
            ? [.. _depts.Where(d => string.Equals(d.CompanyId, companyId, StringComparison.Ordinal))]
            : [.. _flat.FirstOrDefault(d => d.Id == parentId)?.Children ?? []];

    /// <summary>id 가 ancestorId 자신이거나 그 하위인가. 자료가 순환이면 돌지 않게 걸음 수를 센다.</summary>
    private bool IsUnder(string? id, string ancestorId)
    {
        var hops = 0;

        while (id is not null)
        {
            if (string.Equals(id, ancestorId, StringComparison.Ordinal))
            {
                return true;
            }

            if (++hops > _flat.Count)
            {
                return false;
            }

            id = _parent.GetValueOrDefault(id);
        }

        return false;
    }

    private void OpenNew()
    {
        _editingId = null;
        _form = new SaveDeptDto
        {
            CompanyId = _companyFilter,
            Status = 1,
        };
        _editing = true;
    }

    /// <summary>고른 부서 아래에 새 부서. 회사는 부모의 것을 이어받는다.</summary>
    private void OpenChild(DeptDto parent)
    {
        _editingId = null;
        _form = new SaveDeptDto
        {
            Pid = parent.Id,
            CompanyId = parent.CompanyId,
            Status = 1,
        };
        _editing = true;
    }

    private void OpenEdit(DeptDto dept)
    {
        _editingId = dept.Id;
        _form = new SaveDeptDto
        {
            Name = dept.Name,
            Pid = dept.Pid,
            CompanyId = dept.CompanyId,
            Remark = dept.Remark,
            Status = dept.Status,
            SortOrder = dept.SortOrder,
        };
        _editing = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.Name))
        {
            Say("부서명을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_form.CompanyId))
        {
            Say("회사를 고르십시오. 부서는 회사에 딸립니다.", NoticeTone.Warning);
            return;
        }

        var isNew = _editingId is null;

        var saved = await RunAsync(
            () => isNew ? Api.CreateDeptAsync(_form) : Api.UpdateDeptAsync(_editingId!, _form),
            isNew ? "등록했습니다." : "저장했습니다.",
            isNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (saved)
        {
            _editing = false;
            await ReloadAsync();
        }
    }

    /// <summary>삭제. 하위 부서나 소속 인원이 남아 있으면 서버가 막는다 — 화면이 세면 방금 읽은 값이라 그 사이에 바뀌었을 수 있으니 판정은 한 곳에서 한다.</summary>
    private async Task DeleteAsync(DeptDto dept)
    {
        if (await RunAsync(() => Api.DeleteDeptAsync(dept.Id),
                $"{dept.Name} 을 지웠습니다.", "지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>그 부서 아래에 있는 것들의 식별자를 모은다.</summary>
    private static void CollectDescendants(IEnumerable<DeptDto> nodes, string id, HashSet<string> into)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Id, id, StringComparison.Ordinal))
            {
                Collect(node.Children ?? []);
                return;
            }

            if (node.Children is { Count: > 0 })
            {
                CollectDescendants(node.Children, id, into);
            }
        }

        void Collect(IEnumerable<DeptDto> children)
        {
            foreach (var child in children)
            {
                into.Add(child.Id);

                if (child.Children is { Count: > 0 })
                {
                    Collect(child.Children);
                }
            }
        }
    }
}
