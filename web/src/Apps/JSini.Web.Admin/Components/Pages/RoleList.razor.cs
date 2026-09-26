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

    // ── 지정 사용자 ────────────────────────────────────────
    private IReadOnlyList<RoleUserDto> _users = [];
    private bool _pickerOpen;
    private IReadOnlyList<RoleUserDto> _pickable = [];
    private readonly HashSet<string> _picked = new(StringComparer.Ordinal);

    // ── 메뉴 권한 ──────────────────────────────────────────
    private IReadOnlyList<RoleMenuDto> _menus = [];
    private IReadOnlyList<RoleMenuRow> _menuTree = [];

    /// <summary>이 화면에서 권한을 고칠 수 있는가. 체크박스를 열지 정한다.</summary>
    private bool _canEdit;

    /// <summary>
    /// 저장을 <b>한 줄로 세운다.</b> 빨리 여러 번 누르면 요청이 동시에 나가고,
    /// 먼저 보낸 것이 나중에 닿으면 옛 값이 마지막으로 남는다.
    /// </summary>
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>보내는 중이거나 차례를 기다리는 저장 수. 아래 띠의 「저장 중…」.</summary>
    private int _pendingSaves;

    /// <summary>
    /// 나무 한 줄. <b>원본(<see cref="RoleMenuDto"/>)을 그대로 들고 있는다</b> —
    /// 체크박스가 그 객체를 직접 고치고, 저장할 때는 <see cref="_menus"/> 를
    /// 평평한 채로 보내면 된다. 값을 복사하면 두 벌이 되어 어긋난다.
    /// </summary>
    private sealed class RoleMenuRow
    {
        public required RoleMenuDto Source { get; init; }

        public string MenuId => Source.MenuId;
        public string MenuName => Source.MenuName;

        public List<RoleMenuRow> Children { get; } = [];
    }

    /// <summary>
    /// 권한 항목 하나. 어느 칸을 읽고 쓰는지, 그 메뉴가 그것을 쓰는지,
    /// 칸 제목을 무엇으로 할지를 함께 들고 있다.
    /// </summary>
    /// <remarks>
    /// 열다섯 개를 마크업에 늘어놓으면 반드시 하나를 빠뜨리고, 빠뜨린 칸은
    /// <b>저장할 때 꺼져서 나간다</b> — 화면에 없으니 그것을 알 방법도 없다.
    /// </remarks>
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

    /// <summary>
    /// 실제로 그릴 칸.
    ///
    /// <para>
    /// 사용자 정의는 <b>쓰는 메뉴가 하나라도 있을 때만</b> 세운다. 칸 제목은
    /// 그 칸을 쓰는 메뉴들이 붙인 이름이고, 이름이 서로 다르면 하나로 고를 수
    /// 없으므로 <c>C1</c> 로 두고 각자의 이름은 셀 안내에 적는다.
    /// </para>
    /// </summary>
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

    protected override Task OnInitializedAsync()
    {
        // `PermissionView` 와 **똑같이** 묻는다. 체크박스는 그릴지 말지가 아니라
        // 켜고 끌 수 있는지를 정하는 것이라 직접 판정한다.
        _canEdit = Can(MenuAction.Update);

        return ReloadAsync();
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

    /// <summary>
    /// 역할을 골랐다. 사람과 메뉴를 <b>함께</b> 읽는다 — 탭을 옮길 때마다
    /// 읽으면 옮겨 다니는 것이 그대로 왕복이 된다.
    /// </summary>
    private Task SelectRoleAsync(RoleDto? role)
    {
        _role = role;

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

    /// <summary>
    /// 이 사람에게서 역할을 뗀다.
    ///
    /// <para>
    /// <b>다른 길로 온 역할은 떼어지지 않는다.</b> 회사·부서에 걸린 역할은
    /// 그 단계에서 풀어야 한다(사람롤 화면). 여기서 다루는 것은 사람에게
    /// 직접 건 것뿐이다.
    /// </para>
    /// </summary>
    private async Task RemoveUserAsync(RoleUserDto user)
    {
        if (_role is null)
        {
            return;
        }

        var ok = await RunAsync(
            () => Api.RemoveRoleUserAsync(_role.Id, user.Id),
            $"{user.UserName} 에게서 「{_role.Name}」 을(를) 뗐습니다.",
            "역할을 떼지 못했습니다");

        if (ok)
        {
            await LoadUsersAsync();
        }
    }

    // ── 메뉴 권한 ──────────────────────────────────────────

    /// <summary>
    /// 평평한 목록을 나무로 세운다. 서버가 <c>ParentId</c> 를 준다.
    /// </summary>
    /// <remarks>
    /// 부모가 목록에 없으면 뿌리로 올린다 — 빠뜨리면 그 메뉴가 <b>화면에서
    /// 통째로 사라지고</b>, 안 보이는 줄의 권한이 저장 때 함께 나간다.
    /// </remarks>
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

    /// <summary>
    /// 범위 안의 권한을 한 번에 켜거나 끈다.
    ///
    /// <para>
    /// 모두 켜져 있으면 끄고, 아니면 켠다 — 단추 하나로 둘을 다 하려는 것이다.
    /// 바꾼 줄을 <b>한 번에 모아</b> 곧바로 저장한다.
    /// </para>
    ///
    /// <para>
    /// <b>잠긴 칸은 건너뛴다.</b> 켜 두면 서버가 꺼서 저장하므로 화면과 결과가
    /// 어긋난다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 바뀐 메뉴만 저장한다.
    ///
    /// <para>
    /// <b>바뀐 줄만 보내도 된다.</b> 서버는 받은 메뉴만 upsert 하고 목록에
    /// 없는 메뉴는 건드리지 않는다. 한 줄을 보낼 때는 그 줄의 <b>열다섯 칸을
    /// 전부</b> 싣는다 — 사용자 정의 여덟 개를 <see cref="RoleMenuDto"/> 가 들고
    /// 있는 이유이고, 칸을 감출 때도 값은 그대로 실어 보내는 이유다.
    /// </para>
    ///
    /// <para>
    /// <b>보내는 값은 차례가 왔을 때의 값이다.</b> 줄 객체를 그대로 싣기 때문에
    /// 기다리는 동안 사람이 또 눌렀으면 그 값이 나간다. 빨리 켰다 끄면 요청이
    /// 둘 나가지만 마지막에 남는 것은 화면에 보이는 값이다.
    /// </para>
    ///
    /// <para>
    /// <b>실패하면 서버의 값으로 되돌린다.</b> 누르기 전 값을 기억해 두었다가
    /// 되돌리는 방식은 그 사이에 다른 칸이 저장된 경우를 못 맞춘다. 역할이
    /// 이미 바뀌었으면 되돌릴 화면이 없으므로 알리기만 한다.
    /// </para>
    ///
    /// <para>
    /// 성공은 토스트로 알리지 않는다 — 칸마다 뜨면 백 번 누르는 동안 토스트가
    /// 쌓인다. 아래 띠의 「저장 중…」 이 사라지는 것으로 끝났음을 안다.
    /// </para>
    /// </summary>
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

    private void FillNew(RoleDto r) => r.Status = 1;

    private void OnEditOpen(RoleDto r, bool isNew) => _isNew = isNew;

    /// <summary>
    /// 삭제 확인 문구. 고른 역할이면 지정 사용자 수를 함께 말한다 —
    /// 안 고른 역할은 그 수를 모르므로 말하지 않는다(0명이라고 잘못 적으면
    /// 안심하고 지운다).
    /// </summary>
    private string DeleteMessage(RoleDto r)
    {
        var scope = _role is not null && _role.Id == r.Id && _users.Count > 0
            ? $"\n이 역할이 걸린 사람 {_users.Count}명의 권한이 함께 사라집니다."
            : string.Empty;

        return $"역할 「{r.Name}」({r.Id}) 을(를) 지웁니다.{scope}\n되돌릴 수 없습니다.";
    }

    private async Task SaveAsync((RoleDto Item, bool IsNew) e)
    {
        var body = new SaveRoleDto
        {
            Id = e.Item.Id,
            Name = e.Item.Name,
            Remark = e.Item.Remark,
            Status = e.Item.Status,
            Permissions = e.Item.Permissions,
        };

        if (e.IsNew)
        {
            await Api.CreateRoleAsync(body);
            return;
        }

        await Api.UpdateRoleAsync(e.Item.Id, body);
    }

    private Task DeleteAsync(RoleDto r) => Api.DeleteRoleAsync(r.Id);
}
