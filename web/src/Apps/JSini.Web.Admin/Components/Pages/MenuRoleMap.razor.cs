using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Admin.Api;
using JSini.Web.Admin.Components.Shared;

namespace JSini.Web.Admin.Components.Pages;

public partial class MenuRoleMap
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>권한 항목 하나. 어느 칸을 읽고 쓰는지와 메뉴가 그것을 쓰는지를 함께 들고 있다. 손으로 늘어놓으면 하나를 빠뜨리므로 목록으로 둔다.</summary>
    private sealed record PermissionItem(
        string Label,
        Func<MenuUsedPermissionDto, bool> Used,
        Func<MenuRoleGrantDto, bool> Get,
        Action<MenuRoleGrantDto, bool> Set,
        Func<MenuUsedPermissionDto, string?>? Name = null);

    private static readonly PermissionItem[] AllItems =
    [
        new("열람", u => u.View, r => r.CanView, (r, v) => r.CanView = v),
        new("조회", u => u.Search, r => r.CanSearch, (r, v) => r.CanSearch = v),
        new("등록", u => u.Create, r => r.CanCreate, (r, v) => r.CanCreate = v),
        new("수정", u => u.Update, r => r.CanUpdate, (r, v) => r.CanUpdate = v),
        new("삭제", u => u.Delete, r => r.CanDelete, (r, v) => r.CanDelete = v),
        new("출력", u => u.Print, r => r.CanPrint, (r, v) => r.CanPrint = v),
        new("엑셀", u => u.Excel, r => r.CanExcel, (r, v) => r.CanExcel = v),
        new("사용자1", u => u.Cust1, r => r.CanCust1, (r, v) => r.CanCust1 = v, u => u.Cust1Name),
        new("사용자2", u => u.Cust2, r => r.CanCust2, (r, v) => r.CanCust2 = v, u => u.Cust2Name),
        new("사용자3", u => u.Cust3, r => r.CanCust3, (r, v) => r.CanCust3 = v, u => u.Cust3Name),
        new("사용자4", u => u.Cust4, r => r.CanCust4, (r, v) => r.CanCust4 = v, u => u.Cust4Name),
        new("사용자5", u => u.Cust5, r => r.CanCust5, (r, v) => r.CanCust5 = v, u => u.Cust5Name),
        new("사용자6", u => u.Cust6, r => r.CanCust6, (r, v) => r.CanCust6 = v, u => u.Cust6Name),
        new("사용자7", u => u.Cust7, r => r.CanCust7, (r, v) => r.CanCust7 = v, u => u.Cust7Name),
        new("사용자8", u => u.Cust8, r => r.CanCust8, (r, v) => r.CanCust8 = v, u => u.Cust8Name),
    ];

    private IReadOnlyList<SystemMenuDto> _menus = [];
    private string? _keyword;

    /// <summary>고른 메뉴. 부품이 받는다(SelectedItem) — 나무와 표가 같은 값을 나눠 써야 검색어를 넣고 지울 때 고른 것이 풀리지 않는다.</summary>
    private SystemMenuDto? _selected;

    private MenuRoleDto? _detail;
    private string? _savingRole;

    /// <summary>검색 중인가. 나무를 접고 평평한 표로 줄지 가르는 값이다.</summary>
    private bool Filtering => !string.IsNullOrWhiteSpace(_keyword);

    /// <summary>왼쪽 판 머리의 곁줄. 몇 개 중 몇 개를 보고 있는지.</summary>
    private string MenuHint => Filtering
        ? $"검색 {Found.Count}건"
        : $"{_menus.Count}개 묶음";

    /// <summary>닿는 대상이 하나도 없는가. 셋을 각각 물으면 한 곳은 빠뜨린다.</summary>
    private bool NoTargets =>
        _detail is not null
        && _detail.Companies.Count == 0
        && _detail.Departments.Count == 0
        && _detail.Accounts.Count == 0;

    /// <summary>이 메뉴가 실제로 쓰는 권한 항목만. 이름은 메뉴가 붙인 것을 우선한다.</summary>
    private List<(string Label, Func<MenuRoleGrantDto, bool> Get, Action<MenuRoleGrantDto, bool> Set)> Items
    {
        get
        {
            if (_detail is null)
            {
                return [];
            }

            var used = _detail.Used;

            return [.. AllItems
                .Where(i => i.Used(used))
                .Select(i =>
                {
                    var custom = i.Name?.Invoke(used);
                    var label = string.IsNullOrWhiteSpace(custom) ? i.Label : custom!;
                    return (label, i.Get, i.Set);
                })];
        }
    }

    /// <summary>검색에 걸린 메뉴. 트리를 펴서 훑는다.</summary>
    private List<SystemMenuDto> Found
    {
        get
        {
            var keyword = _keyword?.Trim();

            if (string.IsNullOrEmpty(keyword))
            {
                return [];
            }

            var hits = new List<SystemMenuDto>();

            Walk(_menus, hits, keyword);
            return hits;
        }
    }

    protected override Task OnInitializedAsync() => LoadMenusAsync();

    private Task LoadMenusAsync() => LoadAsync(async () =>
    {
        _menus = await Api.GetSystemMenusAsync();
        return _menus.Count;
    }, "등록된 메뉴가 없습니다.", "메뉴 목록을 읽지 못했습니다");

    /// <summary>메뉴를 골랐다. 고른 것이 풀리면(null) 오른쪽을 비운다 — 옛 현황을 남기면 어느 메뉴의 것인지 알 수 없다.</summary>
    private Task SelectAsync(SystemMenuDto? menu)
    {
        _selected = menu;

        if (menu is null)
        {
            _detail = null;
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            _detail = await Api.GetMenuRoleAsync(menu.Id);

            // 못 읽었으면 빈 표가 아니라 실패로 말한다 — 「아무도 못 쓴다」는 거짓이 열린 문을 닫힌 것으로 읽게 한다.
            if (_detail is null)
            {
                throw new ApiException("이 메뉴의 권한 현황을 받지 못했습니다.");
            }

            return _detail.Roles.Count;
        }, "이 메뉴에 걸린 역할이 없습니다.", "권한 현황을 읽지 못했습니다");
    }

    /// <summary>역할 한 줄의 권한을 저장한다. 저장은 메뉴별 upsert 라 보낸 목록에 없는 메뉴는 건드리지 않는다 — 역할 관리와 같은 모양으로 보내야 저장 규칙이 갈라지지 않는다.</summary>
    private async Task SaveRoleAsync(MenuRoleGrantDto role)
    {
        if (_detail is null)
        {
            return;
        }

        _savingRole = role.RoleId;

        try
        {
            var saved = await RunAsync(async () =>
            {
                var current = await Api.GetRoleMenusAsync(role.RoleId);

                var next = current
                    .Where(m => !string.Equals(m.MenuId, _detail.MenuId, StringComparison.Ordinal))
                    .ToList();

                next.Add(new RoleMenuDto
                {
                    MenuId = _detail.MenuId,
                    CanView = role.CanView,
                    CanSearch = role.CanSearch,
                    CanCreate = role.CanCreate,
                    CanUpdate = role.CanUpdate,
                    CanDelete = role.CanDelete,
                    CanPrint = role.CanPrint,
                    CanExcel = role.CanExcel,
                    CanCust1 = role.CanCust1,
                    CanCust2 = role.CanCust2,
                    CanCust3 = role.CanCust3,
                    CanCust4 = role.CanCust4,
                    CanCust5 = role.CanCust5,
                    CanCust6 = role.CanCust6,
                    CanCust7 = role.CanCust7,
                    CanCust8 = role.CanCust8,
                });

                await Api.SaveRoleMenusAsync(role.RoleId, next);
            }, $"{role.RoleName} 권한을 저장했습니다.", "권한을 저장하지 못했습니다");

            if (saved)
            {
                await SelectAsync(_selected);
            }
        }
        finally
        {
            _savingRole = null;
        }
    }

    /// <summary>대상에서 역할을 푼다. 그 역할이 이 메뉴를 주던 유일한 길이면 접근이 끊기므로, 어느 역할 때문에 닿는지를 목록이 함께 보여 준다.</summary>
    private async Task DetachAsync((string Kind, string TargetId, string RoleId, string Name) e)
    {
        var ok = await RunAsync(
            () => Api.RemoveRoleScopeAsync(e.Kind, e.TargetId, e.RoleId),
            $"{e.Name} 에서 역할을 해제했습니다.", "역할을 해제하지 못했습니다");

        if (ok)
        {
            await SelectAsync(_selected);
        }
    }

    private static string Title(SystemMenuDto m) =>
        !string.IsNullOrWhiteSpace(m.Meta.TitleText) ? m.Meta.TitleText!
        : !string.IsNullOrWhiteSpace(m.Meta.Title) ? m.Meta.Title!
        : m.Name;

    /// <summary>오른쪽 제목에 쓸 메뉴 이름. 번역 키일 수 있어 왼쪽 나무와 같은 규칙을 태우고, 골라 둔 메뉴가 이미 번역된 이름을 들고 있으면 그것을 먼저 쓴다.</summary>
    private string MenuName(MenuRoleDto detail) =>
        _selected is not null && string.Equals(_selected.Id, detail.MenuId, StringComparison.Ordinal)
            ? Title(_selected)
            : detail.MenuName;

    /// <summary>나무·표의 한 줄. 아이콘은 사이드바와 같은 규칙으로 MenuGlyph 가 그린다. 화면이 달렸는지는 RouteKey 로 보되, 열쇠를 아직 안 채운 메뉴가 있어 Component 도 함께 본다.</summary>
    private static bool OpensScreen(SystemMenuDto m) =>
        !string.IsNullOrWhiteSpace(m.RouteKey) || !string.IsNullOrWhiteSpace(m.Component);

    /// <summary>트리를 펴서 검색어에 걸리는 것을 모은다. 보이는 이름뿐 아니라 저장된 이름과 경로로도 찾게 한다 — 화면에는 「분석」이라 떠도 사람은 analytics 로 기억한다.</summary>
    private static void Walk(IEnumerable<SystemMenuDto> nodes, List<SystemMenuDto> into, string keyword)
    {
        foreach (var node in nodes)
        {
            var hit =
                Title(node).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || node.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (node.Meta.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || node.Path.Contains(keyword, StringComparison.OrdinalIgnoreCase);

            if (hit)
            {
                into.Add(node);
            }

            if (node.Children is { Count: > 0 })
            {
                Walk(node.Children, into, keyword);
            }
        }
    }
}
