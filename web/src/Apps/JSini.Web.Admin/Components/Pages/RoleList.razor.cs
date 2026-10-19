using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class RoleList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_keyword);

    private IReadOnlyList<RoleDto> _all = [];
    private string? _keyword;

    /// <summary>고른 역할. 오른쪽 판이 전부 이 값에 딸려 있다.</summary>
    private RoleDto? _role;

    /// <summary>편집 창이 등록으로 열렸는가. 식별자를 잠글지 정한다.</summary>
    private bool _isNew;

    private bool _roleEditOpen;
    private RoleDto _editRole = new();

    // ── 지정 사용자 ────────────────────────────────────────
    private IReadOnlyList<RoleUserDto> _users = [];

    private IReadOnlyList<OrgNode> _orgTree = Array.Empty<OrgNode>();

    private async Task LoadOrgTreeAsync()
    {
        if (_orgTree.Count > 0) return;
        var companies = await Api.GetCompaniesAsync();
        var nodes = new List<OrgNode>();
        foreach (var c in companies)
        {
            var companyNode = new OrgNode { Id = c.Id, Name = c.Name, IsUser = false };
            nodes.Add(companyNode);
            
            var depts = await Api.GetDeptsAsync(c.Id);
            var deptNodes = depts.ToDictionary(d => d.Id, d => new OrgNode { Id = d.Id, ParentId = d.Pid ?? c.Id, Name = d.Name, IsUser = false });
            
            foreach (var d in depts)
            {
                var node = deptNodes[d.Id];
                if (d.Pid != null && deptNodes.TryGetValue(d.Pid, out var parent))
                {
                    parent.Children.Add(node);
                }
                else
                {
                    companyNode.Children.Add(node);
                }
            }
            
            foreach (var d in depts)
            {
                var users = await Api.GetDeptUsersAsync(d.Id);
                foreach (var u in users)
                {
                    var userNode = new OrgNode 
                    { 
                        Id = u.Id,
                        ParentId = d.Id,
                        Name = u.UserName,
                        LoginId = u.LoginId,
                        CompanyName = c.Name,
                        DeptName = d.Name,
                        IsUser = true 
                    };
                    deptNodes[d.Id].Children.Add(userNode);
                }
            }
        }
        _orgTree = nodes;
    }

    private Task OnGridItemsDroppedAsync(DevExpress.Blazor.GridItemsDroppedEventArgs e) =>
        e.DroppedItems.FirstOrDefault() is OrgNode node ? AddNodeAsync(node) : Task.CompletedTask;

    /// <summary>위 표에서 고른 사람(빼기 단추).</summary>
    private RoleUserDto? _pickedUser;

    /// <summary>아래 조직도에서 고른 줄(넣기 단추). 부서 줄일 수도 있다.</summary>
    private OrgNode? _pickedNode;

    private bool CanAddPicked =>
        _pickedNode is { IsUser: true } n && _users.All(u => u.Id != n.Id);

    /// <summary>조직도의 사람 하나에게 이 역할을 건다 — 끌어 놓기 · 두 번 누르기 · 넣기 단추가 같이 쓴다. 부서 줄과 이미 지정된 사람은 조용히 건너뛴다.</summary>
    private async Task AddNodeAsync(OrgNode node)
    {
        if (_role is null || !_canEdit || !node.IsUser || _users.Any(u => u.Id == node.Id))
        {
            return;
        }

        var ok = await RunAsync(
            () => Api.AssignRoleUsersAsync(_role.Id, [node.Id]),
            $"{node.Name}에게 「{_role.Name}」 을(를) 걸었습니다.",
            "사용자를 지정하지 못했습니다");

        if (ok)
        {
            _pickedNode = null;
            await LoadUsersAsync();
        }
    }

    private async Task OnTreeItemsDroppedAsync(DevExpress.Blazor.TreeListItemsDroppedEventArgs e)
    {
        if (e.DroppedItems.FirstOrDefault() is RoleUserDto user)
        {
            if (_role is null) return;
            var ok = await RunAsync(
                () => Api.RemoveRoleUserAsync(_role.Id, user.Id),
                $"{user.UserName} 에게서 「{_role.Name}」 을(를) 뗐습니다.",
                "역할을 떼지 못했습니다");
            if (ok) await LoadUsersAsync();
        }
    }

    private bool _pickerOpen;
    private IReadOnlyList<RoleUserDto> _pickable = [];
    private readonly HashSet<string> _picked = new(StringComparer.Ordinal);

    // ── 메뉴 권한 ──────────────────────────────────────────
    private IReadOnlyList<RoleMenuDto> _menus = [];
    private IReadOnlyList<RoleMenuRow> _menuTree = [];

    /// <summary>이 화면에서 권한을 고칠 수 있는가. 체크박스를 열지 정한다.</summary>
    private bool _canEdit;

    /// <summary>저장을 한 줄로 세운다 — 빨리 여러 번 누르면 먼저 보낸 것이 나중에 닿아 옛 값이 마지막으로 남는다.</summary>
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>보내는 중이거나 차례를 기다리는 저장 수. 아래 띠의 「저장 중…」.</summary>
    private int _pendingSaves;

    /// <summary>나무 한 줄. 원본 RoleMenuDto 를 그대로 들고 체크박스가 직접 고친다 — 값을 복사하면 두 벌이 되어 어긋난다.</summary>
    private sealed class RoleMenuRow
    {
        public required RoleMenuDto Source { get; init; }

        public string MenuId => Source.MenuId;
        public string MenuName => Source.MenuName;

        /// <summary>이름 아래 작게 적을 꼬리표(개발용 이름과 주소). 이름과 코드가 같으면 코드를 뺀다.</summary>
        public string Stamp
        {
            get
            {
                var code = Source.MenuCode?.Trim() ?? string.Empty;
                var path = Source.MenuPath?.Trim() ?? string.Empty;

                if (string.Equals(code, MenuName, StringComparison.Ordinal))
                {
                    code = string.Empty;
                }

                return string.Join(" · ", new[] { code, path }.Where(s => s.Length > 0));
            }
        }

        public List<RoleMenuRow> Children { get; } = [];
    }

    /// <summary>권한 항목 하나. 어느 칸을 읽고 쓰는지, 그 메뉴가 쓰는지, 칸 제목을 함께 들고 있다. 마크업에 늘어놓으면 빠뜨린 칸이 저장 때 꺼져 나가므로 목록으로 둔다.</summary>
    private sealed record PermItem(
        string Caption,
        Func<RoleMenuDto, bool> Used,
        Func<RoleMenuDto, bool> Get,
        Action<RoleMenuDto, bool> Set,
        bool IsCustom = false,
        Func<RoleMenuDto, string?>? Name = null)
    {
        /// <summary>셀에 달아 줄 안내. 잠긴 이유나 이 칸의 진짜 이름을 알려 준다.</summary>
        public string Hint(object row, bool used)
        {
            var source = ((RoleMenuRow)row).Source;

            if (!used)
            {
                return $"「{source.MenuName}」 메뉴는 이 권한을 쓰지 않습니다. [메뉴 관리]에서 켤 수 있습니다.";
            }

            var custom = Name?.Invoke(source);
            return string.IsNullOrWhiteSpace(custom) ? Caption : custom!;
        }
    }

    private static readonly PermItem[] BaseItems =
    [
        new("열람", m => m.UseView, m => m.CanView, (m, v) => m.CanView = v),
        new("조회", m => m.UseSearch, m => m.CanSearch, (m, v) => m.CanSearch = v),
        new("등록", m => m.UseCreate, m => m.CanCreate, (m, v) => m.CanCreate = v),
        new("수정", m => m.UseUpdate, m => m.CanUpdate, (m, v) => m.CanUpdate = v),
        new("삭제", m => m.UseDelete, m => m.CanDelete, (m, v) => m.CanDelete = v),
        new("출력", m => m.UsePrint, m => m.CanPrint, (m, v) => m.CanPrint = v),
        new("엑셀", m => m.UseExcel, m => m.CanExcel, (m, v) => m.CanExcel = v),
    ];

    /// <summary>사용자 정의 여덟 개. 쓰는 메뉴가 있을 때만 칸이 선다.</summary>
    private static readonly PermItem[] CustomItems =
    [
        new("C1", m => m.UseCust1, m => m.CanCust1, (m, v) => m.CanCust1 = v, true, m => m.Cust1Name),
        new("C2", m => m.UseCust2, m => m.CanCust2, (m, v) => m.CanCust2 = v, true, m => m.Cust2Name),
        new("C3", m => m.UseCust3, m => m.CanCust3, (m, v) => m.CanCust3 = v, true, m => m.Cust3Name),
        new("C4", m => m.UseCust4, m => m.CanCust4, (m, v) => m.CanCust4 = v, true, m => m.Cust4Name),
        new("C5", m => m.UseCust5, m => m.CanCust5, (m, v) => m.CanCust5 = v, true, m => m.Cust5Name),
        new("C6", m => m.UseCust6, m => m.CanCust6, (m, v) => m.CanCust6 = v, true, m => m.Cust6Name),
        new("C7", m => m.UseCust7, m => m.CanCust7, (m, v) => m.CanCust7 = v, true, m => m.Cust7Name),
        new("C8", m => m.UseCust8, m => m.CanCust8, (m, v) => m.CanCust8 = v, true, m => m.Cust8Name),
    ];

    /// <summary>실제로 그릴 칸. 사용자 정의는 쓰는 메뉴가 있을 때만 서고, 제목이 서로 다르면 C1 로 두고 각자의 이름은 셀 안내에 적는다.</summary>
    private List<PermItem> ShownItems
    {
        get
        {
            var items = new List<PermItem>(BaseItems);

            foreach (var item in CustomItems)
            {
                var users = _menus.Where(m => item.Used(m)).ToList();

                if (users.Count == 0)
                {
                    continue;
                }

                var names = users
                    .Select(m => item.Name?.Invoke(m)?.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                items.Add(names.Count == 1 ? item with { Caption = names[0]! } : item);
            }

            return items;
        }
    }

    /// <summary>사용자 정의를 쓰는 메뉴가 하나도 없는가. 칸이 없는 이유를 말해 준다.</summary>
    private bool NoCustomConfigured =>
        _menus.Count > 0 && !CustomItems.Any(i => _menus.Any(m => i.Used(m)));

    /// <summary>권한이 한 칸이라도 걸린 메뉴 수. 탭 이름에 붙는다.</summary>
    private int GrantedMenuCount =>
        _menus.Count(m => BaseItems.Concat(CustomItems).Any(i => i.Get(m)));

    private IReadOnlyList<RoleDto> Shown
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return _all;
            }

            var k = _keyword.Trim();
            return [.. _all.Where(r =>
                r.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                || r.Id.Contains(k, StringComparison.OrdinalIgnoreCase))];
        }
    }

    protected override async Task OnInitializedAsync()
    {
        // PermissionView 와 같이 묻는다 — 체크박스는 그릴지가 아니라 켜고 끌 수 있는지를 직접 판정한다.
        _canEdit = Can(MenuAction.Update);

        await ReloadAsync();

        // 조직도는 역할 목록 뒤에 읽는다 — 회사마다 부서를 따로 묻느라 호출이 여러 번 나가서, 먼저 읽으면 왼쪽 목록이 늦게 뜬다.
        await LoadOrgTreeAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetRolesAsync();

        // 고른 역할이 사라졌으면(지웠거나) 오른쪽을 비운다.
        if (_role is not null && _all.All(r => r.Id != _role.Id))
        {
            ClearRole();
        }

        return _all.Count;
    }, "등록된 역할이 없습니다.", "역할 목록을 읽지 못했습니다");

    /// <summary>역할을 골랐다. 사람과 메뉴를 함께 읽는다 — 탭을 옮길 때마다 읽으면 그대로 왕복이 된다.</summary>
    private Task SelectRoleAsync(RoleDto? role)
    {
        _role = role;
        _pickedUser = null;

        if (role is null)
        {
            ClearRole();
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            _users = await Api.GetRoleUsersAsync(role.Id);
            _menus = await Api.GetRoleMenusAsync(role.Id);
            _menuTree = BuildTree(_menus);

            return _menus.Count;
        }, "이 역할에 걸 수 있는 메뉴가 없습니다.", "역할 권한을 읽지 못했습니다");
    }

    private void ClearRole()
    {
        _role = null;
        _users = [];
        _menus = [];
        _menuTree = [];
    }

    private Task LoadUsersAsync() => _role is null ? Task.CompletedTask : LoadAsync(async () =>
    {
        _users = await Api.GetRoleUsersAsync(_role.Id);
        return _users.Count;
    }, "지정된 사용자가 없습니다.", "지정 사용자를 읽지 못했습니다");

    private Task LoadMenusAsync() => _role is null ? Task.CompletedTask : LoadAsync(async () =>
    {
        _menus = await Api.GetRoleMenusAsync(_role.Id);
        _menuTree = BuildTree(_menus);
        return _menus.Count;
    }, "메뉴가 없습니다.", "메뉴 권한을 읽지 못했습니다");

    // ── 지정 사용자 ────────────────────────────────────────

    private async Task OpenPickerAsync()
    {
        _picked.Clear();
        _pickerOpen = true;

        await LoadPickableAsync();
    }

    private Task LoadPickableAsync() => _role is null ? Task.CompletedTask : LoadAsync(async () =>
    {
        _pickable = await Api.GetRoleEligibleUsersAsync(_role.Id);
        return _pickable.Count;
    }, "추가할 수 있는 사용자가 없습니다.", "사용자 목록을 읽지 못했습니다");

    private void Pick(string id, bool on)
    {
        if (on)
        {
            _picked.Add(id);
            return;
        }

        _picked.Remove(id);
    }

    private async Task AssignAsync()
    {
        if (_role is null || _picked.Count == 0)
        {
            return;
        }

        var ok = await RunAsync(
            () => Api.AssignRoleUsersAsync(_role.Id, [.. _picked]),
            $"{_picked.Count}명에게 「{_role.Name}」 을(를) 걸었습니다.",
            "사용자를 지정하지 못했습니다");

        if (!ok)
        {
            return;
        }

        _pickerOpen = false;
        await LoadUsersAsync();
    }

    /// <summary>이 사람에게서 역할을 뗀다. 회사·부서에 걸린 역할은 그 단계(사람롤 화면)에서 풀어야 해서 여기서는 사람에게 직접 건 것만 다룬다.</summary>
    private async Task RemoveUserAsync(RoleUserDto user)
    {
        if (_role is null || !_canEdit)
        {
            return;
        }

        var ok = await RunAsync(
            () => Api.RemoveRoleUserAsync(_role.Id, user.Id),
            $"{user.UserName} 에게서 「{_role.Name}」 을(를) 뗐습니다.",
            "역할을 떼지 못했습니다");

        if (ok)
        {
            _pickedUser = null;
            await LoadUsersAsync();
        }
    }

    // ── 메뉴 권한 ──────────────────────────────────────────

    /// <summary>평평한 목록을 나무로 세운다. 부모가 목록에 없으면 뿌리로 올린다 — 빠뜨리면 그 메뉴가 화면에서 사라지고 권한이 저장 때 함께 나간다.</summary>
    private static IReadOnlyList<RoleMenuRow> BuildTree(IReadOnlyList<RoleMenuDto> menus)
    {
        var rows = menus.ToDictionary(
            m => m.MenuId,
            m => new RoleMenuRow { Source = m },
            StringComparer.Ordinal);

        var roots = new List<RoleMenuRow>();

        foreach (var menu in menus)
        {
            var row = rows[menu.MenuId];

            if (!string.IsNullOrWhiteSpace(menu.ParentId)
                && rows.TryGetValue(menu.ParentId!, out var parent))
            {
                parent.Children.Add(row);
                continue;
            }

            roots.Add(row);
        }

        return roots;
    }

    /// <summary>이 줄과 그 하위 전부. 하위 일괄 단추의 범위다.</summary>
    private static List<RoleMenuRow> Subtree(RoleMenuRow row)
    {
        var all = new List<RoleMenuRow>();

        void Walk(RoleMenuRow node)
        {
            all.Add(node);

            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        Walk(row);
        return all;
    }

    /// <summary>범위 안의 권한을 한 번에 켜거나 끈다. 모두 켜져 있으면 끄고 아니면 켠다. 잠긴 칸은 건너뛴다 — 켜 두면 서버가 꺼서 저장해 화면과 어긋난다.</summary>
    private Task ToggleAsync(IReadOnlyList<RoleMenuRow> rows)
    {
        var items = ShownItems;

        var usable = 0;
        var on = 0;

        foreach (var row in rows)
        {
            foreach (var item in items.Where(i => i.Used(row.Source)))
            {
                usable++;

                if (item.Get(row.Source))
                {
                    on++;
                }
            }
        }

        if (!_canEdit || usable == 0)
        {
            return Task.CompletedTask;
        }

        var next = on != usable;
        var changed = new List<RoleMenuDto>();

        foreach (var row in rows)
        {
            var touched = false;

            foreach (var item in items.Where(i => i.Used(row.Source)))
            {
                if (item.Get(row.Source) != next)
                {
                    item.Set(row.Source, next);
                    touched = true;
                }
            }

            if (touched)
            {
                changed.Add(row.Source);
            }
        }

        return SaveRowsAsync(changed);
    }

    /// <summary>체크박스 하나를 바꾸고 그 메뉴를 곧바로 저장한다.</summary>
    private Task SetAsync(RoleMenuRow row, PermItem item, bool value)
    {
        if (!_canEdit || item.Get(row.Source) == value)
        {
            return Task.CompletedTask;
        }

        item.Set(row.Source, value);
        return SaveRowsAsync([row.Source]);
    }

    /// <summary>바뀐 메뉴만 저장한다. 서버는 받은 메뉴만 upsert 하므로 한 줄에는 열다섯 칸을 전부 싣는다. 보내는 값은 차례가 왔을 때의 값이고, 실패하면 서버 값으로 되돌린다. 성공은 토스트로 알리지 않는다.</summary>
    private async Task SaveRowsAsync(IReadOnlyList<RoleMenuDto> rows)
    {
        if (_role is null || rows.Count == 0)
        {
            return;
        }

        var role = _role;
        _pendingSaves++;
        StateHasChanged();

        await _saveGate.WaitAsync();

        try
        {
            await Api.SaveRoleMenusAsync(role.Id, rows);
        }
        catch (ApiException ex)
        {
            Say($"「{role.Name}」 의 메뉴 권한을 저장하지 못했습니다 — {ex.Message}", NoticeTone.Error);

            if (_role?.Id == role.Id)
            {
                await LoadMenusAsync();
            }
        }
        finally
        {
            _saveGate.Release();
            _pendingSaves--;
        }
    }

    // ── 역할 CRUD ──────────────────────────────────────────

    private void OpenNew()
    {
        _isNew = true;
        _editRole = new RoleDto
        {
            Status = 1,
            Permissions = [],
        };
        _roleEditOpen = true;
    }

    private void OpenEdit(RoleDto r)
    {
        _isNew = false;
        _editRole = new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Remark = r.Remark,
            Status = r.Status,
            Permissions = [.. r.Permissions],
        };
        _roleEditOpen = true;
    }

    /// <summary>삭제 확인 문구. 고른 역할이면 지정 사용자 수를 함께 말한다 — 안 고른 역할은 그 수를 몰라서 말하지 않는다.</summary>
    private string DeleteMessage(RoleDto r)
    {
        var scope = _role is not null && _role.Id == r.Id && _users.Count > 0
            ? $"\n이 역할이 걸린 사람 {_users.Count}명의 권한이 함께 사라집니다."
            : string.Empty;

        return $"역할 「{r.Name}」({r.Id}) 을(를) 지웁니다.{scope}\n되돌릴 수 없습니다.";
    }

    private async Task SaveRoleAsync()
    {
        if (string.IsNullOrWhiteSpace(_editRole.Name))
        {
            Say("역할 이름을 적어 주십시오.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_editRole.Id))
        {
            Say("식별자를 적어 주십시오.", NoticeTone.Warning);
            return;
        }

        var id = _editRole.Id.Trim();
        var body = new SaveRoleDto
        {
            Id = id,
            Name = _editRole.Name.Trim(),
            Remark = _editRole.Remark?.Trim(),
            Status = _editRole.Status,
            Permissions = _editRole.Permissions,
        };

        var ok = await RunAsync(async () =>
        {
            if (_isNew)
            {
                await Api.CreateRoleAsync(body);
            }
            else
            {
                await Api.UpdateRoleAsync(id, body);
            }
        },
        _isNew ? $"역할 「{body.Name}」 을(를) 등록했습니다." : $"역할 「{body.Name}」 을(를) 저장했습니다.",
        "역할을 저장하지 못했습니다");

        if (!ok)
        {
            return;
        }

        _roleEditOpen = false;
        await ReloadAsync();

        if (_role is not null && _role.Id == body.Id)
        {
            var updated = _all.FirstOrDefault(r => r.Id == body.Id);
            if (updated is not null)
            {
                _role = updated;
            }
        }
    }

    private Task DeleteAsync(RoleDto r) => Api.DeleteRoleAsync(r.Id);
}
