using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;
using JSini.Web.Http;
using JSini.Web.Components.Menu;

namespace JSini.Web.Admin.Components.Pages;

public partial class MenuList
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private RouteInventory Routes { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.On(_onlyHidden, "숨긴 메뉴만"),
        SliceLabel(_slice));

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

    /// <summary>지금 고치는 메뉴가 바깥 화면을 끼워 넣는 메뉴인가. 이 값이 편집 창의 칸 둘을 가른다 — 「화면」은 감추고 「iframe 주소」를 낸다.</summary>
    private bool IsEmbedded =>
        string.Equals(_edit.Type, "EMBEDDED", StringComparison.OrdinalIgnoreCase);

    // ── 유형별 칸 ───────────────────────────────────────────────
    // 유형마다 쓰는 칸이 다르다. 쓰지 않는 칸은 지우지 않고 끈다 — 유형을 바꿔 되돌렸을 때 적어 둔 값이 남아야 한다.
    // 조건은 칸마다 적지 않고 이 목록 한 곳에 둔다 — 유형을 추가할 때 빠뜨리지 않게.

    private string Kind => string.IsNullOrWhiteSpace(_edit.Type) ? "MENU" : _edit.Type.Trim().ToUpperInvariant();

    /// <summary>화면을 고른다. 화면이 있는 메뉴만.</summary>
    private bool UsesScreen => Kind == "MENU";

    /// <summary>경로는 권한 열쇠다. 묶음은 칸이 있어도 쓰지 않는다 — 입력은 되지만 검증하지 않는다.</summary>
    private bool UsesPath => Kind is "CATALOG" or "EMBEDDED" or "MENU";

    /// <summary>경로를 필수로 보고 중복을 확인하는 유형. 묶음·버튼·바깥 링크는 경로를 검증하지 않는다.</summary>
    private bool ValidatesPath => Kind is "EMBEDDED" or "MENU";

    /// <summary>아이콘은 버튼 말고는 모두 쓴다.</summary>
    private bool UsesIcon => Kind != "BUTTON";

    /// <summary>바깥 링크는 「바깥 링크」 유형만.</summary>
    private bool UsesLink => Kind == "LINK";

    /// <summary>iframe 주소는 「내장」 유형만 — 그 주소가 곧 화면이다.</summary>
    private bool UsesIframe => Kind == "EMBEDDED";

    /// <summary>하위를 숨김은 묶음과 화면에만 뜻이 있다.</summary>
    private bool UsesChildrenHide => Kind is "CATALOG" or "MENU";

    /// <summary>길잡이는 버튼·바깥 링크에서 뜻이 없다.</summary>
    private bool UsesBreadcrumb => Kind is not ("BUTTON" or "LINK");

    /// <summary>탭 고정은 화면이 있는 메뉴(화면·내장)에서만.</summary>
    private bool UsesAffixTab => Kind is "EMBEDDED" or "MENU";

    /// <summary>메뉴에서 숨김·탭·기기 노출은 버튼만 뺀다.</summary>
    private bool UsesVisibility => Kind != "BUTTON";

    /// <summary>권한 항목은 묶음·버튼에는 없다 — 구 화면과 같다.</summary>
    private bool UsesPermissions => Kind is not ("BUTTON" or "CATALOG");

    /// <summary>메뉴 코드의 모양. 영문자로 시작하는 영문·숫자 2~40자다.</summary>
    private static readonly Regex MenuCodeShape = new("^[A-Za-z][A-Za-z0-9]{1,39}$");

    /// <summary>고치기 전의 코드. 바꾸지 않았으면 옛 데이터(한글 등)도 그대로 둔다.</summary>
    private string _originalName = string.Empty;

    private Stats? _stats;

    private sealed record Stats(int Total, int Catalogs, int Screens, int Hidden, int Stopped);

    /// <summary>요약 띠에서 고를 수 있는 갈래. 판정은 <see cref="InSlice"/> 한 곳에 둔다 — 건수와 거른 줄 수가 갈리면 안 된다.</summary>
    private enum MenuSlice { Catalogs, Screens, Hidden, Stopped }

    /// <summary>요약 띠에서 고른 갈래. null 이면 거르지 않는다.</summary>
    private MenuSlice? _slice;

    private static bool InSlice(SystemMenuDto m, MenuSlice slice) => slice switch
    {
        MenuSlice.Catalogs => string.Equals(m.Type, "CATALOG", StringComparison.OrdinalIgnoreCase),
        MenuSlice.Screens => string.Equals(m.Type, "MENU", StringComparison.OrdinalIgnoreCase),
        MenuSlice.Hidden => m.Meta.HideInMenu,
        MenuSlice.Stopped => m.Status != 1,
        _ => true,
    };

    private static string? SliceLabel(MenuSlice? slice) => slice switch
    {
        MenuSlice.Catalogs => "묶음",
        MenuSlice.Screens => "화면",
        MenuSlice.Hidden => "숨김",
        MenuSlice.Stopped => "중지",
        _ => null,
    };

    /// <summary>요약 칸을 눌렀을 때. 고른 칸을 다시 누르면 푼다.</summary>
    private void PickSlice(MenuSlice? slice) => _slice = _slice == slice ? null : slice;

    private static readonly object[] TypeOptions =
    [
        new { Value = "MENU", Text = "화면" },
        new { Value = "CATALOG", Text = "묶음" },
        new { Value = "EMBEDDED", Text = "내장" },
        new { Value = "LINK", Text = "바깥 링크" },
        new { Value = "BUTTON", Text = "버튼" },
    ];

    private bool Filtering => !string.IsNullOrWhiteSpace(_keyword) || _onlyHidden || _slice is not null;

    private IReadOnlyList<SystemMenuDto> Flat
    {
        get
        {
            IEnumerable<SystemMenuDto> rows = _flat;

            if (_slice is { } slice)
            {
                rows = rows.Where(m => InSlice(m, slice));
            }

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

    /// <summary>화면 고르개. 지금 이 프로세스에 실린 화면 전부다 — 라우트가 컴파일 때 고정이라 기동 때 훑은 목록을 쓴다(RouteInventory). 열쇠와 주소를 함께 보여 준다.</summary>
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
        // 권한은 셸이 한 번 실어 둔 것을 PermissionView 와 같이 묻기만 한다 — 다시 읽으면 게이트웨이를 두 벌 태운다.
        // 프리렌더 때 못 실려 있으면 false 이고, 회로가 붙어 화면이 다시 만들어지면 여기도 다시 돈다.
        _canMove = Can(MenuAction.Update);

        return ReloadAsync();
    }

    /// <summary>이 화면에서 메뉴를 옮길 수 있는가. 표의 「수정」 단추와 같은 판정이다 — PermissionView 를 쓸 수 없어 직접 묻는다.</summary>
    private bool _canMove;

    /// <summary>끌어 옮긴 결과를 저장한다. 화면이 배치를 확정해서 보낸다 — 떠난 묶음과 도착한 묶음을 0 부터 다시 번호 붙여 한 번에 보낸다. 자리 계산은 옮길 줄을 뺀 뒤의 형제 목록에서 한다.</summary>
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

        // 자기 자신이나 하위 아래로는 옮길 수 없다 — 서버도 막지만 왕복 전에 말해 준다.
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

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _menus = await Api.GetSystemMenusAsync("ko-KR");
        Index();
        return _flat.Count;
    }, "등록된 메뉴가 없습니다.", "메뉴 목록을 읽지 못했습니다");

    /// <summary>아이콘 고르개에 줄 목록 — 이미 메뉴가 쓰고 있는 아이콘. 그림이 있는 이름만 나온다(menu-icons.css 를 만드는 스크립트가 DB 의 실제 이름으로 만든다).</summary>
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
            _flat.Count(m => InSlice(m, MenuSlice.Catalogs)),
            _flat.Count(m => InSlice(m, MenuSlice.Screens)),
            _flat.Count(m => InSlice(m, MenuSlice.Hidden)),
            _flat.Count(m => InSlice(m, MenuSlice.Stopped)));
    }

    /// <summary>사람이 읽는 제목. 서버가 다국어 표에서 찾아 TitleText 에 넣고, 못 찾으면 Title 이 글자다.</summary>
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

    /// <summary>하위 메뉴로 등록할 때의 상위 메뉴 줄. 등록 창 머리에 적는다.</summary>
    private string? _parentTrail;

    private string EditHeader =>
        !_isNew ? "메뉴 수정" : _parentTrail is null ? "메뉴 등록" : $"하위 메뉴 등록 · {_parentTrail}";

    private Task StartNew()
    {
        _isNew = true;
        _editId = string.Empty;
        _edit = new SystemMenuDto { Type = "MENU", Status = 1 };
        _parentTrail = null;
        ResetTitleTexts();
        _brief = string.Empty;
        _editing = true;
        return Task.CompletedTask;
    }

    /// <summary>관리 칸의 「하위 메뉴 추가」. 상위 메뉴를 그 줄로 정해 두고 등록 창을 연다.</summary>
    private Task StartChild(SystemMenuDto parent)
    {
        // 버튼 밑에는 메뉴를 달 수 없다. 고르개에서도 빠지므로 여기서 막는다.
        if (string.Equals(parent.Type, "BUTTON", StringComparison.OrdinalIgnoreCase))
        {
            Say("버튼 밑에는 메뉴를 달 수 없습니다.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        StartNew();
        _edit.Pid = parent.Id;
        _parentTrail = Trail(parent.Id);
        return Task.CompletedTask;
    }

    /// <summary>편집 창에 복사본을 띄운다 — 원본을 묶으면 저장을 취소해도 표에 바뀐 값이 남는다.</summary>
    private async Task StartEdit(SystemMenuDto m)
    {
        _isNew = false;
        _editId = m.Id;
        _originalName = m.Name;
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

        _brief = string.Empty;
        _editing = true;

        // 제목의 번역은 따로 읽는다 — 메뉴 목록에는 저장된 키와 옮긴 글자만 있다.
        ResetTitleTexts();
        await EnsureI18nAsync();
        RefreshTitleTexts();
    }

    // ── 제목의 다국어 ──────────────────────────────────────────
    // 제목은 번역 키다 — 서버가 scom.i18n_resources 에서 글자를 붙이고, 이 창에서 한국어·영어 값을 짝으로 고친다. 키가 없는 옛 제목은 번역 칸이 비어 있다.

    /// <summary>번역 표 전체를 한 번 읽어 둔 것. 창을 열 때 채우고, 저장하면 비운다.</summary>
    private IReadOnlyList<I18nResourceDto>? _i18n;

    private string _titleKo = string.Empty;
    private string _titleEn = string.Empty;

    private async Task EnsureI18nAsync()
    {
        _i18n ??= await Api.GetAllI18nAsync();
    }

    private void ResetTitleTexts()
    {
        _titleKo = string.Empty;
        _titleEn = string.Empty;
    }

    /// <summary>현재 제목 키의 한국어·영어 값을 표에서 찾아 칸에 넣는다. 표에 없으면 칸은 그대로다.</summary>
    private void RefreshTitleTexts()
    {
        var key = _edit.Meta.Title?.Trim();
        if (string.IsNullOrEmpty(key) || _i18n is null)
        {
            return;
        }

        if (_i18n.FirstOrDefault(r => r.Key == key && r.Locale == "ko") is { } ko)
        {
            _titleKo = ko.Value;
        }

        if (_i18n.FirstOrDefault(r => r.Key == key && r.Locale == "en") is { } en)
        {
            _titleEn = en.Value;
        }
    }

    /// <summary>제목 키 칸을 떠나면 그 키의 번역을 읽어 온다.</summary>
    private async Task OnTitleKeyChangedAsync()
    {
        await EnsureI18nAsync();
        RefreshTitleTexts();
    }

    /// <summary>AI 가 고른 영문 이름을 키로 바꿔 넣는다(예: 「Menu Management」 → <c>menu-management</c>).</summary>
    private async Task PickTitleKeyAsync(string english)
    {
        var text = english.Trim();
        var key = Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (key.Length == 0)
        {
            return;
        }

        _edit.Meta.Title = key;
        if (string.IsNullOrWhiteSpace(_titleEn))
        {
            _titleEn = text;
        }

        await OnTitleKeyChangedAsync();
    }

    /// <summary>제목 번역을 표에 맞춘다. 있으면 고치고 없으면 만든다. 빈 칸은 건드리지 않는다 — 빈 값으로 지우면 옮긴 글자가 사라진다.</summary>
    private async Task<bool> SaveTitleTextsAsync()
    {
        var key = _edit.Meta.Title?.Trim();
        var ko = _titleKo.Trim();
        var en = _titleEn.Trim();

        if (ko.Length == 0 && en.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrEmpty(key))
        {
            Say("번역을 적으려면 제목 키를 먼저 넣어 주세요.", NoticeTone.Warning);
            return false;
        }

        await EnsureI18nAsync();
        var rows = _i18n ?? [];

        try
        {
            foreach (var (locale, value) in new[] { ("ko", ko), ("en", en) })
            {
                if (value.Length == 0)
                {
                    continue;
                }

                var row = rows.FirstOrDefault(r => r.Key == key && r.Locale == locale);
                if (row is null)
                {
                    await Api.CreateI18nAsync(new I18nResourceDto { Key = key, Locale = locale, Value = value, Category = "menu" });
                }
                else if (row.Value != value)
                {
                    await Api.UpdateI18nAsync(row.Id, new I18nResourceDto { Key = key, Locale = locale, Value = value, Category = row.Category ?? "menu" });
                }
            }
        }
        catch (ApiException ex)
        {
            Say(ex.Message, NoticeTone.Warning);
            return false;
        }
        finally
        {
            // 저장한 뒤에는 다시 읽는다 — 다음 창이 옛 표를 보지 않도록.
            _i18n = null;
        }

        return true;
    }

    // ── AI 로 채우기 ───────────────────────────────────────────

    /// <summary>「무엇을 하는 메뉴인가」 한 줄. 비우면 한국어 제목을 설명으로 쓴다.</summary>
    private string _brief = string.Empty;

    private bool _autoFilling;

    private async Task AutoFillAsync()
    {
        var brief = string.IsNullOrWhiteSpace(_brief) ? _titleKo.Trim() : _brief.Trim();
        if (string.IsNullOrWhiteSpace(brief))
        {
            Say("무엇을 하는 메뉴인지 한두 줄 적어 주세요.", NoticeTone.Warning);
            return;
        }

        _autoFilling = true;
        try
        {
            var result = await Api.SuggestMenuAsync(new MenuSuggestRequestDto
            {
                Brief = brief,
                Parent = _parentTrail ?? (_edit.Pid is null ? null : Trail(_edit.Pid)),
                Screens = [.. Routes.Entries.Select(e => new MenuSuggestScreenDto { Key = e.Key, Path = e.Path })],
                Icons = [.. IconChoices],
                ExistingNames = [.. _flat.Select(m => m.Name)],
            });

            if (result is null)
            {
                Say("추천을 받지 못했습니다. 다시 눌러 보세요.", NoticeTone.Warning);
                return;
            }

            await ApplySuggestionAsync(result);
        }
        catch (ApiException ex)
        {
            // 대개 「키가 없다」·「하루 한도를 다 썼다」다. 서버가 준 말을 그대로 보인다.
            Say(ex.Message, NoticeTone.Warning);
        }
        catch (Exception)
        {
            Say("추천을 받지 못했습니다. 다시 눌러 보세요.", NoticeTone.Warning);
        }
        finally
        {
            _autoFilling = false;
        }
    }

    /// <summary>추천을 칸에 넣는다. 비어 있는 칸만 채운다 — 사람이 고른 값을 AI 가 덮어쓰면 안 된다.</summary>
    private async Task ApplySuggestionAsync(MenuSuggestionDto s)
    {
        if (string.IsNullOrWhiteSpace(_edit.Name) && !string.IsNullOrWhiteSpace(s.Name) && MenuCodeShape.IsMatch(s.Name))
        {
            _edit.Name = s.Name;
        }

        if (string.IsNullOrWhiteSpace(_edit.Meta.Title) && !string.IsNullOrWhiteSpace(s.TitleKey))
        {
            _edit.Meta.Title = s.TitleKey;
        }

        if (!string.IsNullOrWhiteSpace(_edit.Meta.Title))
        {
            // 키가 표에 이미 있으면 그 값이 정본이다. 없으면 추천한 번역을 넣는다.
            await OnTitleKeyChangedAsync();
            if (string.IsNullOrWhiteSpace(_titleKo) && !string.IsNullOrWhiteSpace(s.TitleKo))
            {
                _titleKo = s.TitleKo;
            }

            if (string.IsNullOrWhiteSpace(_titleEn) && !string.IsNullOrWhiteSpace(s.TitleEn))
            {
                _titleEn = s.TitleEn;
            }
        }

        if (UsesScreen && string.IsNullOrWhiteSpace(_edit.RouteKey) && !string.IsNullOrWhiteSpace(s.ScreenKey))
        {
            _edit.RouteKey = s.ScreenKey;

            // 화면의 주소를 권한 열쇠로 쓴다 — 비어 있을 때만.
            var route = Routes.Entries.FirstOrDefault(e => e.Key == s.ScreenKey);
            if (route is not null && UsesPath && string.IsNullOrWhiteSpace(_edit.Path))
            {
                _edit.Path = route.Path;
            }
        }

        if (UsesIcon && string.IsNullOrWhiteSpace(_edit.Meta.Icon) && !string.IsNullOrWhiteSpace(s.Icon))
        {
            _edit.Meta.Icon = s.Icon;
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_edit.Name))
        {
            Say("메뉴 코드를 넣어야 합니다.", NoticeTone.Warning);
            return;
        }

        // 새 코드거나 고쳤을 때만 모양을 본다. 옛 한글 코드를 열어 다른 칸만 고칠 때는 막지 않는다.
        var codeChanged = _isNew || !string.Equals(_edit.Name, _originalName, StringComparison.Ordinal);
        if (codeChanged && !MenuCodeShape.IsMatch(_edit.Name.Trim()))
        {
            Say("메뉴 코드는 영문자로 시작하는 영문·숫자 2~40자여야 합니다. 예: PushHistory", NoticeTone.Warning);
            return;
        }

        if (ValidatesPath && string.IsNullOrWhiteSpace(_edit.Path))
        {
            Say("이 유형은 경로를 넣어야 합니다.", NoticeTone.Warning);
            return;
        }

        if (IsEmbedded && string.IsNullOrWhiteSpace(_edit.Meta.IframeSrc))
        {
            // 끼워 넣는 메뉴는 화면이 곧 열쇠다 — 비워 두고 저장하면 눌러도 빈 안내만 뜨므로 여기서 막는다.
            Say("끼워 넣는 메뉴에는 iframe 주소가 있어야 합니다.", NoticeTone.Warning);
            return;
        }

        // 이름·경로가 겹치는지 먼저 묻는다 — 저장 때 서버도 검사하지만 폼을 다 채운 뒤 거절당하지 않게. 경로 겹침은 쓰는 유형에서만 본다.
        var duplicate = await Api.MenuNameExistsAsync(_edit.Name, _isNew ? null : _editId)
            || (ValidatesPath && await Api.MenuPathExistsAsync(_edit.Path, _isNew ? null : _editId));

        if (duplicate)
        {
            Say("같은 코드나 경로를 쓰는 메뉴가 이미 있습니다.", NoticeTone.Warning);
            return;
        }

        // 제목 번역을 메뉴보다 먼저 맞춘다 — 메뉴가 먼저 저장되면 그 키가 가리키는 글자가 잠깐 없다.
        if (!await SaveTitleTextsAsync())
        {
            return;
        }

        var body = new SaveSystemMenuDto
        {
            Name = _edit.Name,
            Path = _edit.Path,

            // 끼워 넣는 메뉴는 화면을 고르지 않으므로 열쇠를 비운다 — 없는 화면을 가리키면 기동 로그에 잡힌다.
            RouteKey = UsesScreen ? _edit.RouteKey : null,

            // 끼워 넣는 메뉴는 공용 화면 하나가 받는다 — 여기서 맞추지 않으면 다음에 만든 메뉴부터 다시 갈린다.
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
