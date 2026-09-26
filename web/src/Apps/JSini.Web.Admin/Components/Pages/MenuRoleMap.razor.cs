using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Admin.Api;
using JSini.Web.Admin.Components.Shared;

namespace JSini.Web.Admin.Components.Pages;

public partial class MenuRoleMap
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>
    /// 권한 항목 하나. 어느 칸을 읽고 쓰는지와, 메뉴가 그것을 쓰는지 보는 법을
    /// 함께 들고 있다. 열다섯 개를 손으로 늘어놓으면 반드시 하나를 빠뜨린다.
    /// </summary>
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

    /// <summary>
    /// 고른 메뉴. <b>부품이 받는다</b>(<c>SelectedItem</c>) — 셀 안에 단추를
    /// 그려 고르던 것을 걷어냈다.
    ///
    /// <para>
    /// 나무와 표가 <b>같은 값을 나눠 쓴다.</b> 검색어를 넣고 지울 때 부품이
    /// 갈리는데(나무 ↔ 표), 값이 따로면 그때 고른 것이 풀려 오른쪽이 빈다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 메뉴를 골랐다. <c>CommTree</c>·<c>CommGrd</c> 가 <b>고른 항목</b>을 준다.
    ///
    /// <para>
    /// 고른 것이 풀리면(<c>null</c>) 오른쪽을 비운다 — 옛 현황을 남겨 두면
    /// 어느 메뉴의 것인지 알 수 없는 표가 된다.
    /// </para>
    /// </summary>
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

            // 못 읽었으면 빈 표가 아니라 실패로 말한다. 권한 화면에서
            // 「아무도 못 쓴다」는 거짓말은 열려 있는 문을 닫힌 것으로 읽게 한다.
            if (_detail is null)
            {
                throw new ApiException("이 메뉴의 권한 현황을 받지 못했습니다.");
            }

            return _detail.Roles.Count;
        }, "이 메뉴에 걸린 역할이 없습니다.", "권한 현황을 읽지 못했습니다");
    }

    /// <summary>
    /// 역할 한 줄의 권한을 저장한다.
    ///
    /// <para>
    /// 저장 경로는 <b>메뉴별 upsert</b> 다 — 보낸 목록에 없는 메뉴는 건드리지
    /// 않는다(서버 <c>SaveRoleMenusAsync</c>). 그래도 그 역할의 현재 목록을
    /// 먼저 받아 이 메뉴 한 줄만 갈아 끼워 보낸다. 한 줄만 보내도 되지만,
    /// <b>읽은 것을 그대로 돌려보내는 모양이 화면 셋에서 같아야</b> 저장 규칙이
    /// 갈라지지 않는다(역할 관리는 전체를 보낸다).
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 대상에서 역할을 푼다.
    ///
    /// <b>그 역할이 이 메뉴를 주던 유일한 길이면 접근이 끊긴다.</b> 어느 역할
    /// 때문에 닿는지를 목록이 함께 보여 주는 이유가 그것이다.
    /// </summary>
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

    /// <summary>
    /// 오른쪽 제목에 쓸 메뉴 이름.
    ///
    /// <para>
    /// 서버가 주는 <c>MenuName</c> 은 DB 의 <c>title</c> 그대로라
    /// <b>번역 키일 수 있다</b>(<c>system.menu.title</c>). 왼쪽 나무와 같은
    /// 규칙을 태우지 않으면 <b>한 화면에서 같은 메뉴가 두 이름으로 보인다.</b>
    /// 골라 둔 메뉴는 이미 번역된 이름을 들고 있으니 그것을 먼저 쓴다.
    /// </para>
    /// </summary>
    private string MenuName(MenuRoleDto detail) =>
        _selected is not null && string.Equals(_selected.Id, detail.MenuId, StringComparison.Ordinal)
            ? Title(_selected)
            : detail.MenuName;

    /// <summary>
    /// 나무·표의 한 줄. <b>아이콘은 사이드바와 같은 규칙</b>으로 그린다 —
    /// 그 규칙은 <see cref="MenuGlyph"/> 가 갖고 있다(사람롤도 같은 것을 쓴다).
    /// </summary>
    /// <remarks>
    /// 화면이 달렸는지는 <see cref="SystemMenuDto.RouteKey"/> 로 본다.
    /// <c>Component</c>(옛 Vue 경로)도 함께 보는 이유는 <b>열쇠를 아직 안 채운
    /// 메뉴가 26건 남아 있어서</b>다 — 그것만 보면 그 26건이 전부 폴더로 보인다.
    /// </remarks>
    private static bool OpensScreen(SystemMenuDto m) =>
        !string.IsNullOrWhiteSpace(m.RouteKey) || !string.IsNullOrWhiteSpace(m.Component);

    /// <summary>
    /// 트리를 펴서 검색어에 걸리는 것을 모은다.
    ///
    /// 보이는 이름뿐 아니라 <b>저장된 이름과 경로</b>로도 찾히게 한다.
    /// 화면에는 「분석」이라고 떠도 사람은 <c>analytics</c> 로 기억하고 있을 수 있다.
    /// </summary>
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
