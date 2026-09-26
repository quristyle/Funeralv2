using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;
using JSini.Web.Components.Menu;

namespace JSini.Web.Admin.Components.Pages;

public partial class MenuList
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private RouteInventory Routes { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.On(_onlyHidden, "숨긴 메뉴만"));

    private IReadOnlyList<SystemMenuDto> _menus = [];

    /// <summary>펴 놓은 전체. 검색·상위 고르개·줄기 만들기가 쓴다.</summary>
    private List<SystemMenuDto> _flat = [];

    /// <summary>자식 → 부모. 줄기(「포털관리 › 시스템 › 메뉴 관리」)를 만든다.</summary>
    private readonly Dictionary<string, string?> _parent = [];

    private readonly Dictionary<string, string> _titles = [];

    private string? _keyword;
    private bool _onlyHidden;

    private bool _editing;
    private bool _isNew;
    private string _editId = string.Empty;
    private SystemMenuDto _edit = new();

    /// <summary>
    /// 지금 고치는 것이 <b>바깥 화면을 끼워 넣는 메뉴</b>인가.
    ///
    /// <para>
    /// 이 값이 편집 창의 칸 둘을 가른다 — 「화면」은 감추고 「iframe 주소」를
    /// 내놓는다. 끼워 넣는 메뉴는 셸의 공용 화면 하나(<c>/embed/…</c>)가 전부
    /// 받으므로 <b>고를 화면이 없다.</b>
    /// </para>
    /// </summary>
    private bool IsEmbedded =>
        string.Equals(_edit.Type, "EMBEDDED", StringComparison.OrdinalIgnoreCase);

    private Stats? _stats;

    private sealed record Stats(int Total, int Catalogs, int Screens, int Hidden, int Stopped);

    private static readonly object[] TypeOptions =
    [
        new { Value = "MENU", Text = "화면" },
        new { Value = "CATALOG", Text = "묶음" },
        new { Value = "EMBEDDED", Text = "내장" },
        new { Value = "LINK", Text = "바깥 링크" },
        new { Value = "BUTTON", Text = "버튼" },
    ];

    private bool Filtering => !string.IsNullOrWhiteSpace(_keyword) || _onlyHidden;

    private IReadOnlyList<SystemMenuDto> Flat
    {
        get
        {
            IEnumerable<SystemMenuDto> rows = _flat;

            if (_onlyHidden)
            {
                rows = rows.Where(m => m.Meta.HideInMenu || m.Status != 1);
            }

            if (!string.IsNullOrWhiteSpace(_keyword))
            {
                var k = _keyword.Trim();
                rows = rows.Where(m =>
                    Title(m).Contains(k, StringComparison.OrdinalIgnoreCase)
                    || m.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || m.Path.Contains(k, StringComparison.OrdinalIgnoreCase));
            }

            return [.. rows];
        }
    }

    /// <summary>
    /// 화면 고르개. <b>지금 이 프로세스에 실려 있는 화면 전부</b>다.
    ///
    /// DB 를 보지 않는다 — 라우트가 컴파일 시점에 고정이라 기동 때 한 번 훑어
    /// 둔 목록을 그대로 쓴다(<c>RouteInventory</c>). 그래서 이 목록에 있는
    /// 화면은 반드시 열리고, 없는 화면은 고를 수가 없다.
    ///
    /// 열쇠와 주소를 함께 보여 준다. 열쇠만 보이면 어디로 가는지 알 수 없고,
    /// 주소만 보이면 저장되는 값이 무엇인지 알 수 없다.
    /// </summary>
    private IReadOnlyList<object> ScreenChoices =>
    [
        .. Routes.Entries.Select(e => (object)new
        {
            Value = e.Key,
            Text = $"{e.Key}  —  {e.Path}",
        })
    ];

    /// <summary>상위 메뉴 고르개. 묶음만 담는다 — 화면 밑에 화면을 달지 않는다.</summary>
    private IReadOnlyList<object> ParentChoices =>
    [
        .. _flat
            .Where(m => !string.Equals(m.Type, "BUTTON", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.Id != _editId)
            .OrderBy(Trail, StringComparer.OrdinalIgnoreCase)
            .Select(m => (object)new { Value = m.Id, Text = Trail(m.Id) })
    ];

    protected override Task OnInitializedAsync()
    {
        // `PermissionView` 와 **똑같이** 묻는다 — 물어보기만 하고 다시 읽지
        // 않는다. 권한은 셸이 한 번 실어 두고, 여기서 또 읽으면 프리렌더까지
        // 합쳐 게이트웨이를 두 벌 태운다(커밋 「프리렌더에서는 조회하지 않는다」).
        //
        // 프리렌더 때 못 실려 있으면 이 값은 false 가 되지만, 회로가 붙으면
        // 화면이 다시 만들어지면서 여기도 다시 돈다.
        _canMove = Can(MenuAction.Update);

        return ReloadAsync();
    }

    /// <summary>
    /// 이 화면에서 메뉴를 옮길 수 있는가. 표의 「수정」 단추와 <b>같은 판정</b>이다.
    ///
    /// <para>
    /// <c>PermissionView</c> 를 쓸 수 없는 자리라 직접 묻는다 — 끌어 옮기기는
    /// 그릴지 말지가 아니라 <c>CommTree</c> 의 파라미터로 켜고 끄는 것이다.
    /// 경로를 꺼내는 규칙은 그 부품과 한 곳(<c>PermissionPath</c>)을 쓴다.
    /// </para>
    /// </summary>
    private bool _canMove;

    /// <summary>
    /// 끌어 옮긴 결과를 저장한다.
    ///
    /// <para>
    /// <b>화면이 배치를 확정해서 보낸다.</b> 형제 하나가 움직이면 그 묶음
    /// 전체의 순번이 바뀌므로, 옮긴 한 건만 보내면 서버가 나머지를 어떻게
    /// 밀지 짐작해야 하고 그 짐작이 화면과 어긋난다. 떠난 묶음과 도착한
    /// 묶음 둘을 0 부터 다시 번호 붙여 한 번에 보낸다.
    /// </para>
    ///
    /// <para>
    /// 자리 계산은 <b>옮길 줄을 빼낸 뒤의</b> 형제 목록에서 한다. 빼기 전
    /// 목록에서 세면 같은 묶음 안에서 아래로 옮길 때 한 칸씩 어긋난다 —
    /// 자기가 차지하고 있던 자리가 목록에 아직 남아 있기 때문이다.
    /// </para>
    /// </summary>
    private async Task OnDroppedAsync(TreeListItemsDroppedEventArgs e)
    {
        if (e.DroppedItems.FirstOrDefault() is not SystemMenuDto moved)
        {
            return;
        }

        var target = e.TargetItem as SystemMenuDto;

        // 줄 **위**에 놓았으면 그 메뉴의 하위가 된다. 줄 **사이**면 형제다.
        var inside = target is not null
            && e.DropPosition is TreeListItemDropPosition.Inside or TreeListItemDropPosition.Append;

        var newParentId = target is null ? null
            : inside ? target.Id
            : _parent.GetValueOrDefault(target.Id);

        // 자기 자신이나 자기 하위 아래로는 옮길 수 없다. 서버도 막지만
        // 왕복하기 전에 말해 준다 — 끌어 놓은 손이 아직 그 자리에 있다.
        if (newParentId is not null && IsUnder(newParentId, moved.Id))
        {
            Say("자기 자신이나 하위 메뉴 아래로는 옮길 수 없습니다.", NoticeTone.Warning);
            return;
        }

        var oldParentId = _parent.GetValueOrDefault(moved.Id);
        var sameParent = string.Equals(oldParentId, newParentId, StringComparison.Ordinal);

        var arrived = Siblings(newParentId).Where(m => m.Id != moved.Id).ToList();
        var at = arrived.Count;

        if (target is not null && !inside)
        {
            at = arrived.FindIndex(m => m.Id == target.Id);

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

        var items = new List<MenuOrderDto>();
        Number(newParentId, arrived);

        if (!sameParent)
        {
            Number(oldParentId, Siblings(oldParentId).Where(m => m.Id != moved.Id).ToList());
        }

        if (await RunAsync(() => Api.ReorderSystemMenusAsync(items),
                $"「{Title(moved)}」 을(를) 옮겼습니다.", "옮기지 못했습니다"))
        {
            await ReloadAsync();
        }

        void Number(string? parentId, List<SystemMenuDto> rows)
            => items.AddRange(rows.Select((m, i) => new MenuOrderDto
            {
                Id = m.Id,
                Pid = parentId,
                OrderNo = i,
            }));
    }

    /// <summary>그 부모의 자식들. 최상위는 나무의 뿌리다.</summary>
    private List<SystemMenuDto> Siblings(string? parentId) =>
        parentId is null
            ? [.. _menus]
            : [.. _flat.FirstOrDefault(m => m.Id == parentId)?.Children ?? []];

    /// <summary>
    /// <paramref name="id"/> 가 <paramref name="ancestorId"/> 자신이거나 그 하위인가.
    /// 자료가 이미 순환이면 돌지 않도록 걸음 수를 센다.
    /// </summary>
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

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _menus = await Api.GetSystemMenusAsync("ko-KR");
        Index();
        return _flat.Count;
    }, "등록된 메뉴가 없습니다.", "메뉴 목록을 읽지 못했습니다");

    /// <summary>
    /// 아이콘 고르개에 줄 목록 — <b>이미 메뉴가 쓰고 있는 아이콘</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 새 엔드포인트를 만들지 않는다. 이 화면은 조회할 때 이미 메뉴 전체를
    /// 받아 두므로(<c>_flat</c>) 거기서 뽑으면 된다.
    /// </para>
    ///
    /// <para>
    /// <b>그 목록이 곧 그림이 있는 아이콘들이다.</b> <c>menu-icons.css</c> 를
    /// 만드는 <c>scripts/build-menu-icons.py</c> 가 <b>DB 에 실제로 쓰이는
    /// 이름으로</b> 만들기 때문이다. 그래서 고른 것은 반드시 그림이 나온다.
    /// </para>
    ///
    /// <para>
    /// 목록을 코드에 적어 두지 않는 이유는 <c>MenuIcons</c> 머리말과 같다 —
    /// 적으면 CSS 를 다시 만들 때 두 곳을 맞춰야 하고, 어긋나면 아이콘이
    /// 사라지는 쪽으로 틀린다.
    /// </para>
    /// </remarks>
    private IReadOnlyCollection<string> IconChoices =>
        [.. _flat
            .Select(m => m.Meta.Icon)
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>나무를 한 번 훑어 평평한 목록·부모 표·통계를 만든다.</summary>
    private void Index()
    {
        _flat = [];
        _parent.Clear();
        _titles.Clear();

        void Walk(IEnumerable<SystemMenuDto> nodes, string? parentId)
        {
            foreach (var node in nodes)
            {
                _flat.Add(node);
                _parent[node.Id] = parentId;
                _titles[node.Id] = Title(node);

                if (node.Children is { Count: > 0 } children)
                {
                    Walk(children, node.Id);
                }
            }
        }

        Walk(_menus, null);

        _stats = new Stats(
            _flat.Count,
            _flat.Count(m => string.Equals(m.Type, "CATALOG", StringComparison.OrdinalIgnoreCase)),
            _flat.Count(m => string.Equals(m.Type, "MENU", StringComparison.OrdinalIgnoreCase)),
            _flat.Count(m => m.Meta.HideInMenu),
            _flat.Count(m => m.Status != 1));
    }

    /// <summary>
    /// 사람이 읽는 제목.
    ///
    /// 서버가 다국어 표에서 찾아 <c>TitleText</c> 에 넣어 준다. 못 찾으면
    /// <c>Title</c> 이 이미 글자라는 뜻이고, 그것도 비면 이름으로 떨어진다.
    /// </summary>
    private static string Title(SystemMenuDto m) =>
        !string.IsNullOrWhiteSpace(m.Meta.TitleText) ? m.Meta.TitleText!
        : !string.IsNullOrWhiteSpace(m.Meta.Title) ? m.Meta.Title!
        : m.Name;

    /// <summary>뿌리부터의 줄기. 검색 결과에서 어느 업무의 메뉴인지 보이게 한다.</summary>
    private string Trail(string id)
    {
        var parts = new List<string>();
        var cursor = id;

        // 자기 참조가 섞이면 무한히 돈다. 깊이로 막는다.
        for (var depth = 0; depth < 12 && cursor is not null; depth++)
        {
            if (!_titles.TryGetValue(cursor, out var title))
            {
                break;
            }

            parts.Insert(0, title);
            _parent.TryGetValue(cursor, out cursor);
        }

        return string.Join(" › ", parts);
    }

    private string Trail(SystemMenuDto m) => Trail(m.Id);

    private static string Permits(MenuPermissionItemsDto p)
    {
        var on = new List<string>();

        if (p.UseView) on.Add("조회");
        if (p.UseSearch) on.Add("검색");
        if (p.UseCreate) on.Add("등록");
        if (p.UseUpdate) on.Add("수정");
        if (p.UseDelete) on.Add("삭제");
        if (p.UsePrint) on.Add("출력");
        if (p.UseExcel) on.Add("엑셀");

        return on.Count == 7 ? "전부" : string.Join("·", on);
    }

    private void StartNew()
    {
        _isNew = true;
        _editId = string.Empty;
        _edit = new SystemMenuDto { Type = "MENU", Status = 1 };
        _editing = true;
    }

    /// <summary>
    /// 편집 창에 **복사본**을 띄운다.
    ///
    /// 원본을 그대로 묶으면 저장을 취소해도 표에는 이미 바뀐 값이 남는다 —
    /// 화면과 서버가 어긋난 채로 보이고, 새로 고치기 전에는 알 수 없다.
    /// </summary>
    private void StartEdit(SystemMenuDto m)
    {
        _isNew = false;
        _editId = m.Id;
        _edit = new SystemMenuDto
        {
            Id = m.Id,
            Name = m.Name,
            Path = m.Path,
            RouteKey = m.RouteKey,
            Component = m.Component,
            Pid = m.Pid ?? _parent.GetValueOrDefault(m.Id),
            Redirect = m.Redirect,
            Type = m.Type,
            AuthCode = m.AuthCode,
            Status = m.Status,
            Meta = new SystemMenuMetaDto
            {
                Title = m.Meta.Title,
                TitleText = m.Meta.TitleText,
                Icon = m.Meta.Icon,
                Order = m.Meta.Order,
                HideInMenu = m.Meta.HideInMenu,
                HideChildrenInMenu = m.Meta.HideChildrenInMenu,
                HideInBreadcrumb = m.Meta.HideInBreadcrumb,
                HideInTab = m.Meta.HideInTab,
                KeepAlive = m.Meta.KeepAlive,
                AffixTab = m.Meta.AffixTab,
                Link = m.Meta.Link,
                IframeSrc = m.Meta.IframeSrc,
                UseMobile = m.Meta.UseMobile,
                UseTablet = m.Meta.UseTablet,
            },
            Permissions = new MenuPermissionItemsDto
            {
                UseView = m.Permissions.UseView,
                UseSearch = m.Permissions.UseSearch,
                UseCreate = m.Permissions.UseCreate,
                UseUpdate = m.Permissions.UseUpdate,
                UseDelete = m.Permissions.UseDelete,
                UsePrint = m.Permissions.UsePrint,
                UseExcel = m.Permissions.UseExcel,
            },
        };
        _editing = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_edit.Name) || string.IsNullOrWhiteSpace(_edit.Path))
        {
            Say("메뉴 이름과 경로는 반드시 넣어야 합니다.", NoticeTone.Warning);
            return;
        }

        if (IsEmbedded && string.IsNullOrWhiteSpace(_edit.Meta.IframeSrc))
        {
            // 끼워 넣는 메뉴에서 이 칸은 **곧 화면이다.** 비워 두고 저장하면
            // 메뉴는 생기는데 눌러도 빈 안내만 뜬다 — 그 상태를 만들어 두느니
            // 여기서 막는 편이 낫다.
            Say("끼워 넣는 메뉴에는 iframe 주소가 있어야 합니다.", NoticeTone.Warning);
            return;
        }

        // 이름·경로가 겹치는지 먼저 묻는다. 저장할 때 서버도 검사하지만,
        // 여기서 걸러야 사용자가 폼을 다 채운 뒤에 거절당하지 않는다.
        var duplicate = await Api.MenuNameExistsAsync(_edit.Name, _isNew ? null : _editId)
            || await Api.MenuPathExistsAsync(_edit.Path, _isNew ? null : _editId);

        if (duplicate)
        {
            Say("같은 이름이나 경로를 쓰는 메뉴가 이미 있습니다.", NoticeTone.Warning);
            return;
        }

        var body = new SaveSystemMenuDto
        {
            Name = _edit.Name,
            Path = _edit.Path,

            // 끼워 넣는 메뉴는 화면을 고르지 않으므로 열쇠를 비워 저장한다.
            // 남겨 두면 **없는 화면을 가리키는 메뉴**가 되어(그 화면들은 공용
            // 화면 하나로 합치면서 지웠다) 기동 로그에 「화면이 없다」로 잡힌다.
            RouteKey = IsEmbedded ? null : _edit.RouteKey,

            // 끼워 넣는 메뉴는 공용 화면 하나가 받는다. 여기서 맞춰 두지 않으면
            // **DB 를 고쳐 놔도 다음에 만든 메뉴부터 다시 갈린다** — 「화면 파일」
            // 칸이 비어 있거나 옛 Vue 경로가 남아, 화면이 여럿인 것처럼 읽힌다.
            Component = IsEmbedded ? EmbeddedRoute.ScreenFile : _edit.Component,
            Pid = _edit.Pid,
            Redirect = _edit.Redirect,
            Type = _edit.Type,
            AuthCode = _edit.AuthCode,
            Status = _edit.Status,
            Meta = _edit.Meta,
            Permissions = _edit.Permissions,
        };

        var saved = await RunAsync(
            () => _isNew ? Api.CreateSystemMenuAsync(body) : Api.UpdateSystemMenuAsync(_editId, body),
            _isNew ? "등록했습니다." : "저장했습니다.",
            _isNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (!saved)
        {
            // 창을 닫지 않는다. 닫으면 채운 내용이 사라진다.
            return;
        }

        _editing = false;
        await ReloadAsync();
    }

    private async Task DeleteAsync(SystemMenuDto m)
    {
        // 하위가 달린 묶음을 지우면 그 아래가 통째로 미아가 된다. 서버도 막지만
        // 여기서 먼저 말해 주는 편이 낫다.
        if (m.Children is { Count: > 0 })
        {
            Say("하위 메뉴가 있어 지울 수 없습니다. 먼저 하위를 옮기거나 지우십시오.", NoticeTone.Warning);
            return;
        }

        if (await RunAsync(() => Api.DeleteSystemMenuAsync(m.Id), "지웠습니다.", "지우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
