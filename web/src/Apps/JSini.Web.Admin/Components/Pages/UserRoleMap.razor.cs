using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class UserRoleMap
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _mode == Mode.Account ? "사람" : "회사·부서",
        _keyword);

    private enum Mode
    {
        /// <summary>사람을 고른다. 합쳐진 결과와 메뉴까지 본다.</summary>
        Account,

        /// <summary>회사·부서를 고른다. 그 단계에 직접 걸린 역할만 다룬다.</summary>
        Scope,
    }

    /// <summary>왼쪽 목록의 한 칸. 사람과 회사·부서를 같은 모양으로 다룬다.</summary>
    private sealed record Target(string Id, string Kind, string Name, string Meta);

    private Mode _mode = Mode.Account;
    private string? _keyword;

    private IReadOnlyList<RoleDto> _roles = [];
    private IReadOnlyList<Target> _accounts = [];
    private IReadOnlyList<Target> _scopes = [];

    private Target? _selected;

    /// <summary>고른 대상에 <b>직접</b> 걸린 역할.</summary>
    private IReadOnlyList<string> _direct = [];

    /// <summary>사람 모드에서의 합산 결과.</summary>
    private EffectiveRolesDto? _effective;

    private IReadOnlyList<AccountMenuItemDto> _assigned = [];
    private IReadOnlyList<AccountMenuItemDto> _unassigned = [];

    /// <summary>
    /// 전체 메뉴 나무. <b>구조와 아이콘이 여기에만 있다.</b>
    ///
    /// <para>
    /// 「볼 수 있는 메뉴」 응답(<see cref="AccountMenuItemDto"/>)에는 상위
    /// 메뉴도 아이콘도 없다 — 길은 <c>Breadcrumb</c> 에 <b>글자로</b> 이어
    /// 붙여 온다. 그것만으로는 나무를 세울 수 없어서, 사이드바가 쓰는 것과
    /// 같은 목록을 한 번 더 읽어 맞물린다.
    /// </para>
    ///
    /// <para>
    /// 대상마다 다시 읽지 않는다 — 사람이 바뀌어도 메뉴 나무는 그대로다.
    /// </para>
    /// </summary>
    private IReadOnlyList<SystemMenuDto> _menus = [];

    /// <summary>고른 사람이 볼 수 있는 메뉴만 남긴 나무.</summary>
    private IReadOnlyList<MenuRow> _tree = [];

    /// <summary>
    /// 나무 한 줄. 메뉴의 생김새(<see cref="SystemMenuDto"/>)와 그 사람의
    /// 권한(<see cref="AccountMenuItemDto"/>)을 합쳐 놓은 것이다.
    /// </summary>
    private sealed class MenuRow
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string? Icon { get; init; }
        public string Type { get; init; } = string.Empty;
        public bool OpensScreen { get; init; }

        /// <summary>
        /// 이 줄 <b>자신</b>이 열린 메뉴인가.
        ///
        /// <para>
        /// 거짓이면 <b>자식이 열려서 남은 묶음</b>이다 — 빼면 나무가 끊겨
        /// 어느 업무의 메뉴인지 알 수 없게 된다. 화면에서 딱지로 가른다.
        /// </para>
        /// </summary>
        public bool Granted { get; init; }

        /// <summary>이 메뉴를 열어 준 역할 이름들. 묶음 줄은 비어 있다.</summary>
        public string GrantedBy { get; init; } = string.Empty;

        public List<MenuRow> Children { get; init; } = [];
    }

    private IReadOnlyList<Target> Source => _mode == Mode.Account ? _accounts : _scopes;

    /// <summary>이 대상에 <b>직접</b> 걸린 역할 수. 권한 탭의 이름에 붙는다.</summary>
    private int DirectCount => _direct.Count;

    /// <summary>
    /// 메뉴 칸의 제목.
    ///
    /// <para>
    /// 회사·부서에서는 셀 수가 없어 숫자를 붙이지 않는다 — 붙이면 0 이 되고,
    /// 0 은 <b>「아무것도 못 본다」로 읽힌다.</b> 그 단계의 역할은 속한 사람에게
    /// 합쳐져 닿으므로 결과가 사람마다 다르다.
    /// </para>
    /// </summary>
    private string MenuTitleText => _mode == Mode.Account
        ? $"볼 수 있는 메뉴 ({_assigned.Count} / {_assigned.Count + _unassigned.Count})"
        : "볼 수 있는 메뉴";

    private IReadOnlyList<Target> Targets
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return Source;
            }

            var k = _keyword.Trim();
            return [.. Source.Where(t =>
                t.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                || t.Meta.Contains(k, StringComparison.OrdinalIgnoreCase))];
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _roles = await Api.GetRolesAsync();
        _accounts = await LoadAccountsAsync();
        _scopes = await LoadScopesAsync();

        // 메뉴 나무는 **사람과 무관**하다. 대상마다 다시 읽지 않으려고
        // 여기서 한 번 읽는다(`_menus` 머리말).
        _menus = await Api.GetSystemMenusAsync();

        // 고른 대상이 사라졌으면(회사를 바꿨거나) 오른쪽을 비운다.
        if (_selected is not null && Source.All(t => t.Id != _selected.Id))
        {
            _selected = null;
        }

        return Source.Count;
    }, "대상이 없습니다.", "권한 정보를 읽지 못했습니다");

    private async Task<IReadOnlyList<Target>> LoadAccountsAsync()
    {
        var rows = await Api.GetRoleScopeAccountsAsync();

        return
        [
            .. rows.Select(a => new Target(
                a.Id,
                "account",
                a.Name,
                string.Join(" · ", new[] { a.LoginId, a.DepartmentName, a.CompanyName }
                    .Where(x => !string.IsNullOrWhiteSpace(x))!)))
        ];
    }

    /// <summary>
    /// 회사·부서 목록.
    ///
    /// 회사마다 나무를 따로 받아야 한다(<c>tree?companyId=</c>). 회사가 몇 개
    /// 안 되므로 그만큼 왕복해도 무겁지 않다.
    /// </summary>
    private async Task<IReadOnlyList<Target>> LoadScopesAsync()
    {
        var companies = await Api.GetCompaniesAsync();
        var targets = new List<Target>();

        foreach (var company in companies)
        {
            var tree = await Api.GetRoleScopeTreeAsync(company.Id);

            if (tree is null)
            {
                continue;
            }

            targets.Add(new Target(tree.Company.Id, "company", tree.Company.Name, "회사"));
            Walk(tree.Company, company.Name, targets);
        }

        return targets;
    }

    /// <summary>
    /// 「볼 수 있는 메뉴」를 <b>사이드바와 같은 나무</b>로 세운다.
    ///
    /// <para>
    /// 전체 메뉴 나무를 훑으면서 <b>그 사람에게 열린 것과 그 조상만</b>
    /// 남긴다. 사이드바가 보이는 메뉴를 고를 때 하는 일과 같다
    /// (<c>MenuFilter</c>) — 거기서는 <c>MenuNode</c> 를 다루고 여기서는
    /// 포털관리의 <c>SystemMenuDto</c> 라 타입이 달라 그 코드를 못 쓴다.
    /// </para>
    ///
    /// <para>
    /// <b>조상을 남기지 않으면 나무가 끊긴다.</b> 열린 것만 남기면 자식이
    /// 뿌리로 올라와 어느 업무의 메뉴인지 알 수 없게 되는데, 이 화면에서
    /// 사람이 확인하려는 것이 바로 그 「어느 업무가 몇 개 열렸나」다.
    /// </para>
    /// </summary>
    private IReadOnlyList<MenuRow> BuildTree()
    {
        // 열린 메뉴를 아이디로 찾을 수 있게 해 둔다. 109건 × 179줄을
        // 목록으로 훑으면 그 곱이 그대로 비용이 된다.
        var granted = _assigned
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return Prune(_menus, granted);
    }

    private List<MenuRow> Prune(
        IEnumerable<SystemMenuDto> nodes,
        Dictionary<string, AccountMenuItemDto> granted)
    {
        var kept = new List<MenuRow>();

        foreach (var node in nodes)
        {
            var children = node.Children is { Count: > 0 }
                ? Prune(node.Children, granted)
                : [];

            var hit = granted.TryGetValue(node.Id, out var item);

            // 자기도 안 열렸고 남은 자식도 없으면 뺀다.
            if (!hit && children.Count == 0)
            {
                continue;
            }

            kept.Add(new MenuRow
            {
                Id = node.Id,
                Title = MenuTitle(node),
                Path = node.Path,
                Icon = node.Meta.Icon,
                Type = node.Type,

                // 열쇠가 정본이지만 아직 안 채운 메뉴가 26건 남아 있어
                // 옛 `Component` 도 함께 본다(`MenuGlyph` 머리말).
                OpensScreen = !string.IsNullOrWhiteSpace(node.RouteKey)
                    || !string.IsNullOrWhiteSpace(node.Component),

                Granted = hit,
                GrantedBy = hit
                    ? string.Join(", ", item!.GrantedBy.Select(RoleName))
                    : string.Empty,

                Children = children,
            });
        }

        return kept;
    }

    /// <summary>
    /// 메뉴 이름. <b>사이드바와 같은 규칙</b>이다 — 서버가 번역해 준 것이
    /// 있으면 그것을, 없으면 저장된 글자를 쓴다.
    ///
    /// <para>
    /// 「볼 수 있는 메뉴」 응답의 <c>Title</c> 을 쓰지 않는 이유가 그것이다 —
    /// 그쪽은 DB 값 그대로라 <c>system.menu.title</c> 같은 <b>번역 키일 수
    /// 있다.</b> 그러면 한 화면에서 같은 메뉴가 두 이름으로 보인다.
    /// </para>
    /// </summary>
    private static string MenuTitle(SystemMenuDto m) =>
        !string.IsNullOrWhiteSpace(m.Meta.TitleText) ? m.Meta.TitleText!
        : !string.IsNullOrWhiteSpace(m.Meta.Title) ? m.Meta.Title!
        : m.Name;

    /// <summary>부서 나무를 평평하게 편다. 이름 앞에 상위 부서를 이어 붙인다.</summary>
    private static void Walk(RoleScopeNodeDto node, string trail, List<Target> into)
    {
        foreach (var child in node.Children)
        {
            into.Add(new Target(child.Id, "department", child.Name, trail));
            Walk(child, $"{trail} › {child.Name}", into);
        }
    }

    private async Task SwitchAsync(Mode mode)
    {
        _mode = mode;
        _selected = null;
        _direct = [];
        _effective = null;
        _assigned = [];
        _unassigned = [];
        _tree = [];

        await Task.CompletedTask;
    }

    private Task SelectAsync(Target target)
    {
        _selected = target;

        return LoadAsync(async () =>
        {
            if (target.Kind == "account")
            {
                _effective = await Api.GetEffectiveRolesAsync(target.Id);

                // 사람 단계에서 직접 걸린 것만 뺄 수 있다. 부서·회사에서 온
                // 역할을 여기서 빼려 하면 서버가 거절한다.
                _direct = _effective is null
                    ? []
                    : [.. _effective.Sources
                        .Where(kv => kv.Value.Contains("account"))
                        .Select(kv => kv.Key)];

                var access = await Api.GetAccountMenuAccessAsync(target.Id);
                _assigned = access?.Assigned ?? [];
                _unassigned = access?.Unassigned ?? [];
                _tree = BuildTree();

                return _effective?.RoleIds.Count ?? 0;
            }

            // 회사·부서는 합산이 없다. 그 단계에 직접 걸린 것뿐이다.
            _effective = null;
            _assigned = [];
            _unassigned = [];
            _tree = [];
            _direct = await DirectRolesAsync(target);

            return _direct.Count;
        }, "걸린 역할이 없습니다.", "역할을 읽지 못했습니다");
    }

    /// <summary>회사·부서에 직접 걸린 역할. 나무에서 그 노드를 찾아 꺼낸다.</summary>
    private async Task<IReadOnlyList<string>> DirectRolesAsync(Target target)
    {
        var companies = await Api.GetCompaniesAsync();

        foreach (var company in companies)
        {
            var tree = await Api.GetRoleScopeTreeAsync(company.Id);

            if (tree is null)
            {
                continue;
            }

            if (Find(tree.Company, target.Id) is { } node)
            {
                return node.RoleIds;
            }
        }

        return [];
    }

    private static RoleScopeNodeDto? Find(RoleScopeNodeDto node, string id)
    {
        if (node.Id == id)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (Find(child, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private bool IsDirect(string roleId) => _direct.Contains(roleId);

    /// <summary>이 역할이 어느 단계에서 왔는가. 회사·부서 모드에서는 직접뿐이다.</summary>
    private IReadOnlyList<string> SourcesOf(string roleId)
    {
        if (_effective is not null)
        {
            return _effective.Sources.GetValueOrDefault(roleId) ?? [];
        }

        return IsDirect(roleId) ? [KindOf(_selected?.Kind)] : [];
    }

    private static string KindOf(string? kind) => kind ?? "account";

    private async Task AssignAsync(string roleId)
    {
        if (_selected is null)
        {
            return;
        }

        if (await RunAsync(
                () => Api.AssignRoleScopeAsync(_selected.Kind, _selected.Id, roleId),
                "역할을 걸었습니다.", "걸지 못했습니다"))
        {
            await SelectAsync(_selected);
        }
    }

    private async Task RemoveAsync(string roleId)
    {
        if (_selected is null)
        {
            return;
        }

        if (await RunAsync(
                () => Api.RemoveRoleScopeAsync(_selected.Kind, _selected.Id, roleId),
                "역할을 뺐습니다.", "빼지 못했습니다"))
        {
            await SelectAsync(_selected);
        }
    }

    private string RoleName(string roleId) =>
        _roles.FirstOrDefault(r => r.Id == roleId)?.Name ?? roleId;

    private static string KindLabel(string kind) => kind switch
    {
        "company" => "회사",
        "department" => "부서",
        _ => "사람",
    };

    private static string SourceLabel(string source) => source switch
    {
        "company" => "회사에서",
        "department" => "부서에서",
        _ => "직접",
    };

    /// <summary>사진이 없을 때 쓰는 이름 첫 글자. 없는 쪽이 정상이다.</summary>
    private static string Initial(string name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name[..1];

    /// <summary>이름에서 만든 색 씨앗. 같은 사람은 늘 같은 색이 된다.</summary>
    private static int Seed(string name) =>
        Math.Abs(name.GetHashCode(StringComparison.Ordinal)) % 360;
}
