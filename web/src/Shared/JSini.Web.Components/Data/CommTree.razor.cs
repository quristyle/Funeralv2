using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;

namespace JSini.Web.Components.Data;

public partial class CommTree<TItem>
{
    /// <summary>나무에 넣을 자료.</summary>
    [Parameter, EditorRequired] public object Data { get; set; } = default!;

    /// <summary>나무의 칸들. 관리 칸은 이 부품이 붙인다 — 순번 칸은 없다(머리말 5번).</summary>
    [Parameter, EditorRequired] public RenderFragment? Columns { get; set; }

    /// <summary>줄을 가리키는 열쇠 칸. 두 모양 모두 필요하다.</summary>
    [Parameter, EditorRequired] public string? KeyFieldName { get; set; }

    /// <summary>자식 목록을 담은 칸(자식 목록형).</summary>
    [Parameter] public string? ChildrenFieldName { get; set; }

    /// <summary>부모의 열쇠를 담은 칸(부모 열쇠형).</summary>
    [Parameter] public string? ParentKeyFieldName { get; set; }

    /// <summary>컬럼 너비 조절 방식. CommGrd 와 같은 기본값 — 끌어 바꾼 그 칸만 바뀐다(옆 칸을 밀지 않는다).</summary>
    [Parameter] public TreeListColumnResizeMode ColumnResizeMode { get; set; } = TreeListColumnResizeMode.ColumnsContainer;

    /// <summary>팝업 편집 폼.</summary>
    [Parameter] public RenderFragment<TreeListEditFormTemplateContext>? EditFormTemplate { get; set; }

    /// <summary>나무 위에 놓을 것. 보통은 비운다 — 조건은 CommSch 로 간다.</summary>
    [Parameter] public RenderFragment? Toolbar { get; set; }

    /// <summary>아래 띠 왼쪽. 동작 단추는 두지 않는다 — 동작은 ContextMenuItems 로 간다.</summary>
    [Parameter] public RenderFragment? FooterLeft { get; set; }

    /// <summary>아래 띠의 오른쪽 끝. <see cref="FooterLeft"/> 와 같은 규칙이다.</summary>
    [Parameter] public RenderFragment? FooterRight { get; set; }

    /// <summary>오른쪽 클릭 창에 보탤 화면만의 동작. 안에 CommMenuItem 을 적는다(권한은 PermissionView 로 감싼다).</summary>
    [Parameter] public RenderFragment? ContextMenuItems { get; set; }

    /// <summary>빈 목록 자리를 통째로 갈아 끼울 때.</summary>
    [Parameter] public RenderFragment? EmptyArea { get; set; }

    /// <summary>관리 칸에서 「하위 추가」 앞에 놓을 단추들. 그 줄의 자료를 받는다. 제목 칸에 끼우면 화면마다 조작 자리가 달라진다.</summary>
    [Parameter] public RenderFragment<TItem>? RowActions { get; set; }

    /// <summary>관리 칸의 「하위 메뉴 추가」. 화면이 편집 창을 직접 띄울 때 쓰며 그 줄을 준다. OnSave 로 여는 팝업이면 주지 않는다.</summary>
    [Parameter] public EventCallback<TItem> OnAddChild { get; set; }

    /// <summary>관리 칸을 세우는가 — 「⋮」 판에 들어갈 것이 하나라도 있으면 선다.</summary>
    private bool HasActions => OnSave.HasDelegate || OnDelete.HasDelegate || OnAddChild.HasDelegate || RowActions is not null;


    /// <summary>자료가 없을 때 보여 줄 문구.</summary>
    [Parameter] public string EmptyText { get; set; } = "표시할 자료가 없습니다.";

    /// <summary>팝업 편집 폼의 제목.</summary>
    [Parameter] public string EditFormTitle { get; set; } = "편집";

    /// <summary>아래 띠와 오른쪽 클릭 창을 쓸지. 끄면 등록·다시 읽기·펼치기·칸별 검색·엑셀이 함께 사라진다.</summary>
    [Parameter] public bool ShowFooter { get; set; } = true;

    /// <summary>관리 칸의 「하위 추가」. OnSave 를 준 화면에서만 보인다. 깊이가 정해진 나무(회사 &gt; 부서)에서는 끈다.</summary>
    [Parameter] public bool ShowAddChild { get; set; } = true;

    /// <summary>모두 펼치기·접기 토글 단추.</summary>
    [Parameter] public bool ShowExpandToggle { get; set; } = true;

    /// <summary>칸별 검색 줄 토글 단추. 시작 상태는 화면의 <c>ShowFilterRow</c> 가 정한다.</summary>
    [Parameter] public bool ShowFilterToggle { get; set; } = true;

    /// <summary>엑셀 내보내기. 켜져 있어도 <c>use_excel</c> 권한이 없으면 안 보인다.</summary>
    [Parameter] public bool ShowExcelExport { get; set; } = true;

    /// <summary>짝수 줄에 옅은 바탕을 깐다(줄무늬). 들여쓰기가 줄을 가르지만 칸이 많으면 기본으로 켠다.</summary>
    [Parameter] public bool AlternateRows { get; set; } = true;

    /// <summary>내려받을 파일 이름(확장자 없이).</summary>
    [Parameter] public string ExportName { get; set; } = "목록";

    /// <summary>새 줄의 기본값을 채운다. 둘째 값이 부모다 — 하위 추가로 열었으면 그 줄, 등록이면 null(최상위). 부모 열쇠(Pid)를 여기서 넣는다.</summary>
    [Parameter] public Action<TItem, TItem?>? OnNew { get; set; }

    /// <summary>편집 창이 열릴 때 부른다(등록·수정 모두). bool 이 「새로 만드는 것인가」다. 폼 상태를 수정 창을 열 때도 비워야 하면 이것을 쓴다.</summary>
    [Parameter] public Action<TItem, bool>? OnEditOpen { get; set; }

    /// <summary>저장. 주면 「등록」·「하위 추가」·「수정」이 생긴다. Parent 는 새로 만들 때의 부모다(수정이면 그 줄의 부모).</summary>
    [Parameter] public EventCallback<(TItem Item, bool IsNew, TItem? Parent)> OnSave { get; set; }

    /// <summary>삭제. 주면 「삭제」가 생긴다.</summary>
    [Parameter] public EventCallback<TItem> OnDelete { get; set; }

    /// <summary>목록을 다시 읽는다. 저장·삭제 뒤에도 부른다.</summary>
    [Parameter] public EventCallback Reload { get; set; }

    /// <summary>삭제 확인 문구. 주면 기본 확인창 대신 ConfirmDialog 로 묻는다 — 가지를 지우면 아래가 함께 사라진다.</summary>
    [Parameter] public Func<TItem, string>? DeleteConfirm { get; set; }

    /// <summary>고른 줄. SelectedItemChanged 와 함께 준다 — 한쪽만 주면 고른 줄이 매 렌더마다 null 로 되돌아간다.</summary>
    [Parameter] public TItem? SelectedItem { get; set; }

    /// <summary>고른 줄이 바뀌었을 때. 여기서 받은 값이 다음 렌더의 선택이 된다.</summary>
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }

    /// <summary>줄을 두 번 눌렀을 때. 그 줄을 준다. splat 으로 넘기면 500 으로 죽으므로 파라미터로 선언한다.</summary>
    [Parameter] public EventCallback<TItem> OnRowDoubleClick { get; set; }

    /// <summary>줄을 끌어 옮길 수 있는가. 권한으로 켜고 끈다 — 「수정」 단추와 같은 PermissionPath 판정을 써야 어긋나지 않는다.</summary>
    [Parameter] public bool AllowDragRows { get; set; }

    /// <summary>어디에 놓을 수 있는가. 기본은 이 나무 안. 받을 부품을 실제로 둔 화면만 External 을 연다.</summary>
    [Parameter] public TreeListAllowedDropTarget AllowedDropTarget { get; set; } = TreeListAllowedDropTarget.Internal;

    /// <summary>놓을 자리를 줄 사이까지 잡는다(기본 BetweenRows). Component 로 두면 상위 변경만 되고 형제 순서를 못 바꾼다.</summary>
    [Parameter] public TreeListDropTargetMode DropTargetMode { get; set; } = TreeListDropTargetMode.BetweenRows;

    /// <summary>줄을 끌어다 놓았을 때. 놓은 줄(DroppedItems)·놓인 자리(TargetItem·DropPosition)를 준다. splat 으로 넘길 수 없어 이름을 선언한다.</summary>
    [Parameter] public EventCallback<TreeListItemsDroppedEventArgs> ItemsDropped { get; set; }

    /// <summary>선언하지 않은 모든 DxTreeList 파라미터.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Extra { get; set; }

    /// <summary>감싼 DxTreeList. 화면이 DevExpress API 를 직접 불러야 할 때.</summary>
    public ITreeList? Tree => _tree;

    /// <summary>관리 칸 너비(px). 「⋮」 하나만 서므로 화면과 무관하게 같다 — CommGrd 와 같다.</summary>
    private const int ActionsPixels = 48;

    /// <summary><c>Width</c> 는 글자로, <c>MinWidth</c> 는 정수로 받는다.</summary>
    private static string ActionsColumnWidth =>
        ActionsPixels.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>「⋮」 판의 상태. 표 두 개가 같은 판을 쓴다(<see cref="CommMoreMenu"/>).</summary>
    private readonly CommMoreMenu _more;

    public CommTree() => _more = new CommMoreMenu(() => StateHasChanged());

    private void OnMenuChanged() => StateHasChanged();

    private ITreeList? _tree;
    private IReadOnlyDictionary<string, object>? _forwarded;

    /// <summary>나무가 지금 들고 있는 선택. 화면이 값을 주면 그것을 따른다.</summary>
    private object? _selected;

    /// <summary><see cref="DeleteConfirm"/> 을 준 화면에서만 쓰는 확인창.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>이번 렌더에 화면이 <see cref="SelectedItem"/> 을 주었는가.</summary>
    private bool _selectionBound;

    private bool _filterRow;
    private bool _filterRowSeeded;
    private bool _hasFilter;
    private bool _expanded;

    private static readonly Dictionary<string, Type> DxTreeListParameterTypes =
        typeof(DxTreeList)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute), inherit: true))
            .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.OrdinalIgnoreCase);

    /// <summary>화면이 SelectedItem 을 주었는지. null 과 안 준 것은 파라미터로 구분되지 않으므로 ParameterView 로 본다.</summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // object 로 받는다. TItem 으로 받으면 값 형식일 때 null 을 캐스팅하다 죽는다.
        _selectionBound = parameters.TryGetValue<object>(nameof(SelectedItem), out _);

        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        _forwarded = Coerce(Extra);

        // 자료가 갈리면 관리 판을 닫는다(줄을 차례 번호로 가리키므로).
        _more.ResetIfDataChanged(Data);

        RequireTreeShape();

        // 선택의 주인이 화면이면 그 값을 따르고, 아니면 나무가 든 것을 그대로 둔다 — 안 준 값(null)으로 덮으면 강조가 풀린다.
        if (_selectionBound)
        {
            _selected = SelectedItem;
        }

        // 시작값이라 한 번만 읽는다. 매번 읽으면 사람이 끈 것을 다시 켜게 된다.
        if (!_filterRowSeeded)
        {
            _filterRowSeeded = true;
            _filterRow = Read("ShowFilterRow") is bool flag && flag;
        }
    }

    /// <summary>나무 모양을 알려 주었는지 본다. 열쇠나 자식 칸을 빠뜨리면 DevExpress 는 말없이 평평한 목록을 그려 「자료가 절반만 온다」로 잘못 읽힌다. 자식을 그때그때 부르는 화면(ChildrenLoadingOnDemand)은 통과시킨다.</summary>
    private void RequireTreeShape()
    {
        if (string.IsNullOrWhiteSpace(KeyFieldName))
        {
            throw new InvalidOperationException(
                "CommTree: KeyFieldName 이 없습니다. 줄을 가리키는 열쇠 칸을 주십시오 — "
                + "KeyFieldName=\"@nameof(내자료.Id)\". "
                + "없으면 DxTreeList 가 말없이 평평한 목록을 그립니다.");
        }

        if (!string.IsNullOrWhiteSpace(ChildrenFieldName)
            || !string.IsNullOrWhiteSpace(ParentKeyFieldName)
            || Read("ChildrenLoadingOnDemand") is not null
            || Read("ChildrenLoading") is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            "CommTree: 나무 모양을 알려 주지 않았습니다. 둘 중 하나를 주십시오 — "
            + "ChildrenFieldName(자식 목록형) 또는 ParentKeyFieldName(부모 열쇠형). "
            + "없으면 DxTreeList 가 말없이 평평한 목록을 그립니다.");
    }

    /// <summary>splat 으로 들어온 값 하나를 이름으로 찾는다.</summary>
    private object? Read(string name)
    {
        if (_forwarded is null)
        {
            return null;
        }

        foreach (var (key, value) in _forwarded)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private readonly Dictionary<string, TreeListColumnSortOrder> _sortSeen = new(StringComparer.Ordinal);

    /// <summary>칸 머리 클릭을 오름 → 내림 → 없음으로 돌린다. 나무의 원래 차례는 서버가 준 계층·순서라 되돌릴 길이 꼭 있어야 한다. OnAfterRender 가 아니라 LayoutAutoSaving 에 건다 — 정렬은 나무 안에서 끝나 부모가 다시 그려지지 않는다.</summary>
    private Task OnLayoutChanged(TreeListPersistentLayoutEventArgs e)
    {
        if (_tree is null)
        {
            return Task.CompletedTask;
        }

        string? release = null;
        var now = new Dictionary<string, TreeListColumnSortOrder>(StringComparer.Ordinal);

        foreach (var column in _tree.GetSortedColumns())
        {
            if (column.FieldName is not { Length: > 0 } field)
            {
                continue;
            }

            now[field] = column.SortOrder;

            if (_sortSeen.TryGetValue(field, out var before)
                && before == TreeListColumnSortOrder.Descending
                && column.SortOrder == TreeListColumnSortOrder.Ascending)
            {
                release = field;
            }
        }

        _sortSeen.Clear();

        foreach (var (name, order) in now)
        {
            _sortSeen[name] = order;
        }

        if (release is null)
        {
            return Task.CompletedTask;
        }

        // 기억에서도 지운다. 안 지우면 다음 첫 클릭이 또 세 번째로 읽힌다.
        _sortSeen.Remove(release);

        // 알림을 받는 도중에 layout 을 또 건드리지 않는다. 한 박자 뒤로 미룬다.
        var target = release;
        _ = InvokeAsync(() => _tree.SortBy(target, TreeListColumnSortOrder.None));

        return Task.CompletedTask;
    }

    /// <summary>모두 펼친다. 바깥에서도 부를 수 있다.</summary>
    public void ExpandAll()
    {
        _expanded = true;
        _tree?.ExpandAll();
    }

    /// <summary>모두 접는다. 바깥에서도 부를 수 있다.</summary>
    public void CollapseAll()
    {
        _expanded = false;
        _tree?.CollapseAll();
    }

    /// <summary>모두 펼치기 · 모두 접기. AutoExpandAllNodes 는 사람이 접은 것을 다음 렌더에 다시 펴므로 쓰지 않고 한 번만 부른다.</summary>
    private void ToggleExpand()
    {
        if (_expanded)
        {
            CollapseAll();
        }
        else
        {
            ExpandAll();
        }
    }

    private DxContextMenu? _menu;

    /// <summary>창을 여는 순간 세운 항목. 연 뒤에 권한·상태가 바뀌어도 창은 그대로다.</summary>
    private IReadOnlyList<TreeMenuItem> _menuItems = [];

    private sealed record TreeMenuItem(
        string Text, string? Icon, Func<Task> Run, bool BeginGroup = false, bool Enabled = true);

    /// <summary>화면이 <see cref="ContextMenuItems"/> 로 보탠 항목. 적힌 차례대로다.</summary>
    private readonly List<CommMenuItem> _screenItems = [];

    void ICommMenuHost.Add(CommMenuItem item)
    {
        _screenItems.Add(item);

        // 브라우저 창을 막을지는 렌더 때 정해진다 — 첫 항목이 들어오면 한 번 다시 그린다.
        if (_screenItems.Count == 1)
        {
            StateHasChanged();
        }
    }

    void ICommMenuHost.Remove(CommMenuItem item) => _screenItems.Remove(item);

    /// <summary>오른쪽 클릭 창에 올릴 것. 아래 띠 아이콘과 같은 조건이다(CommGrd.BuildMenu 와 같은 차례에 「모두 펼치기 · 접기」가 더해진다).</summary>
    private List<TreeMenuItem> BuildMenu()
    {
        var items = new List<TreeMenuItem>();

        if (!ShowFooter)
        {
            return items;
        }

        if (OnSave.HasDelegate && Can(MenuAction.Create))
        {
            items.Add(new("등록 (최상위)", "jsini-icon-plus", StartNewAsync));
        }

        foreach (var mine in _screenItems)
        {
            items.Add(new(mine.Text, mine.IconCssClass, () => mine.Click.InvokeAsync(),
                BeginGroup: mine.BeginGroup, Enabled: mine.Enabled));
        }

        if (Reload.HasDelegate)
        {
            items.Add(new("다시 읽기", "jsini-icon-refresh", () => Reload.InvokeAsync(),
                BeginGroup: items.Count > 0));
        }

        if (ShowExpandToggle)
        {
            items.Add(new(_expanded ? "모두 접기" : "모두 펼치기",
                _expanded ? "jsini-icon-fold" : "jsini-icon-unfold",
                () => { ToggleExpand(); return Task.CompletedTask; },
                BeginGroup: !Reload.HasDelegate && items.Count > 0));
        }

        if (ShowFilterToggle)
        {
            items.Add(new(_filterRow ? "칸별 검색 줄 감추기" : "칸별 검색 줄 보이기", "jsini-icon-filter",
                () => { ToggleFilterRow(); return Task.CompletedTask; },
                BeginGroup: items.Count > 0));
        }

        if (_hasFilter)
        {
            items.Add(new("칸별 검색 지우기", "jsini-icon-filter-off",
                () => { ClearFilter(); return Task.CompletedTask; },
                BeginGroup: !ShowFilterToggle && items.Count > 0));
        }

        if (ShowExcelExport && Can(MenuAction.Excel))
        {
            items.Add(new("엑셀로 내려받기", "jsini-icon-excel", ExportAsync, BeginGroup: items.Count > 0));
        }

        return items;
    }

    /// <summary>브라우저 창을 막을지. 렌더할 때 정해진다(`CommGrd.HasMenu` 머리말).</summary>
    private bool HasMenu => BuildMenu().Count > 0;

    private async Task OpenMenuAsync(MouseEventArgs args)
    {
        _menuItems = BuildMenu();

        if (_menu is null || _menuItems.Count == 0)
        {
            return;
        }

        // 항목을 먼저 그려 놓고 연다. 같은 렌더에서 열면 창이 옛 항목으로 뜬다.
        StateHasChanged();
        await _menu.ShowAsync(args);
    }

    private void ToggleFilterRow()
    {
        _filterRow = !_filterRow;

        // 줄을 감출 때 조건도 지운다. 안 지우면 보이지 않는 조건에 걸러진 나무가 남는다.
        if (!_filterRow && _hasFilter)
        {
            ClearFilter();
        }
    }

    private void ClearFilter()
    {
        _tree?.ClearFilter();
        _hasFilter = false;
    }

    private void OnFilterCriteriaChanged(TreeListFilterCriteriaChangedEventArgs e)
    {
        // CriteriaOperator 는 `==` 를 재정의해서 null 비교가 예상과 다르게 돈다.
        _hasFilter = !ReferenceEquals(_tree?.GetFilterCriteria(), null);
    }

    /// <summary>짝수 줄에 표시 클래스를 붙이고 자료 칸을 가운데로 맞춘다. 첫 칸은 계층 칸이라 가운데로 맞추지 않는다 — 깊이마다 제목 자리가 달라져 계층이 안 보인다.</summary>
    private void OnCustomizeElement(TreeListCustomizeElementEventArgs e)
    {
        if (AlternateRows
            && e.ElementType == TreeListElementType.DataRow
            && e.VisibleIndex % 2 == 1)
        {
            e.CssClass = "commtree__row--alt";
            return;
        }

        // 칸이 TextAlignment 를 직접 정했으면 건드리지 않는다. CSS 로는 이 구분이 안 된다(DevExpress 가 같은 클래스를 붙인다).
        if (e.ElementType == TreeListElementType.DataCell
            && e.Column?.TextAlignment == TreeListTextAlignment.Auto
            && !IsTreeColumn(e))
        {
            e.CssClass = "commtree__cell--center";
        }
    }

    /// <summary>계층 칸인가 — 보이는 칸 중 첫째. e.Column.VisibleIndex 는 이 알림에서 늘 -1 이라 쓸 수 없다. GetVisibleColumns() 의 첫째와 참조로 견준다.</summary>
    private static bool IsTreeColumn(TreeListCustomizeElementEventArgs e)
    {
        var columns = e.TreeList.GetVisibleColumns();

        return columns.Count > 0 && ReferenceEquals(columns[0], e.Column);
    }

    /// <summary>splat 으로 들어온 문자열을 DxTreeList 가 기다리는 형으로 바꾼다.</summary>
    private static IReadOnlyDictionary<string, object>? Coerce(IReadOnlyDictionary<string, object>? source)
    {
        if (source is null || source.Count == 0)
        {
            return source;
        }

        // 바꿀 것이 없으면 원본을 그대로 쓴다. 새 사전을 만들면 나무가 다시 그려진다.
        Dictionary<string, object>? changed = null;

        foreach (var (name, value) in source)
        {
            if (!DxTreeListParameterTypes.TryGetValue(name, out var declared))
            {
                continue;
            }

            // 대리자는 감싸면 안 된다 — 감싸면 알림 받는이가 나무만 다시 그려진다. 여기서 막아야 DevExpress 대입 때 죽는 원인을 알 수 있다.
            if (value is Delegate && IsEventCallback(declared))
            {
                throw new InvalidOperationException(
                    $"CommTree: DxTreeList 의 '{name}' 은 EventCallback 이라 splat 으로 넘길 수 없습니다. "
                    + "CommTree 가 선언한 파라미터를 쓰십시오 — 선택은 SelectedItem · SelectedItemChanged, "
                    + "끌어 옮기기는 ItemsDropped 입니다. "
                    + $"그런 파라미터가 없으면 CommTree 에 먼저 만드십시오({name} 을(를) 그대로 내려보내면 됩니다).");
            }

            if (value is not string text)
            {
                continue;
            }

            var want = Nullable.GetUnderlyingType(declared) ?? declared;

            if (want == typeof(string) || want == typeof(object))
            {
                continue;
            }

            object converted;

            try
            {
                converted = want.IsEnum
                    ? Enum.Parse(want, text, ignoreCase: true)
                    : Convert.ChangeType(text, want, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"CommTree: DxTreeList 의 '{name}' 에 준 값 \"{text}\" 을(를) {want.Name} 으로 읽지 못했습니다. "
                    + $"식으로 주려면 {name}=\"@(…)\" 로 적으십시오.", ex);
            }

            changed ??= new Dictionary<string, object>(source, StringComparer.Ordinal);
            changed[name] = converted;
        }

        return changed ?? source;
    }

    /// <summary>DxTreeList 의 선택 알림을 화면이 아는 형으로 옮긴다. 받는이를 화면으로 두어야 고른 줄에 딸린 칸까지 다시 그려진다.</summary>
    private Task OnSelectedDataItemChangedAsync(object? item)
    {
        _selected = item;

        return SelectedItemChanged.InvokeAsync(item is TItem typed ? typed : default);
    }

    private Task OnRowDoubleClickAsync(TreeListRowClickEventArgs e)
    {
        if (!OnRowDoubleClick.HasDelegate)
        {
            return Task.CompletedTask;
        }

        return e.TreeList.GetDataItem(e.VisibleIndex) is TItem item
            ? OnRowDoubleClick.InvokeAsync(item)
            : Task.CompletedTask;
    }

    private static bool IsEventCallback(Type type) =>
        type == typeof(EventCallback)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));

    /// <summary>최상위에 새 줄을 만드는 팝업을 연다. 바깥에서도 부를 수 있다.</summary>
    public Task StartNewAsync() => _tree?.StartEditNewRowAsync() ?? Task.CompletedTask;

    /// <summary>그 줄 아래에 새 줄을 만드는 팝업을 연다. 부모는 DevExpress 가 넘겨 준다.</summary>
    public Task StartChildAsync(int parentVisibleIndex) =>
        _tree?.StartEditNewRowAsync(parentVisibleIndex) ?? Task.CompletedTask;

    private Task EditAsync(int visibleIndex) => _tree?.StartEditRowAsync(visibleIndex) ?? Task.CompletedTask;

    /// <summary>삭제 확인. DeleteConfirm 을 준 화면만 우리 확인창을 쓴다 — 맡기면 기본 확인창이 한 번 더 뜬다.</summary>
    private async Task ConfirmDeleteAsync(int visibleIndex, object? dataItem)
    {
        if (DeleteConfirm is null || dataItem is not TItem item)
        {
            _tree?.ShowRowDeleteConfirmation(visibleIndex);
            return;
        }

        if (await _confirm!.AskAsync(DeleteConfirm(item)))
        {
            await DeleteAsync(item);
        }
    }

    /// <summary>지금 보이는 그대로(정렬·필터 상태) 내린다. 접힌 가지도 함께 내린다 — 기본값은 펼침 상태를 옮겨 접힌 가지가 말없이 빠진다.</summary>
    private Task ExportAsync() =>
        _tree?.ExportToXlsxAsync($"{ExportName}.xlsx", new TreeListXlExportOptions
        {
            ExportSelectedRowsOnly = false,
            RowExpandMode = TreeListExportRowExpandMode.ExpandAll,
        }) ?? Task.CompletedTask;

    private void OnCustomizeEditModel(TreeListCustomizeEditModelEventArgs e)
    {
        if (e.EditModel is not TItem item)
        {
            return;
        }

        if (e.IsNew)
        {
            OnNew?.Invoke(item, Parent(e.ParentDataItem));
        }

        OnEditOpen?.Invoke(item, e.IsNew);
    }

    private async Task OnEditModelSavingAsync(TreeListEditModelSavingEventArgs e)
    {
        if (e.EditModel is not TItem model)
        {
            return;
        }

        var saved = await RunAsync(
            () => OnSave.InvokeAsync((model, e.IsNew, Parent(e.ParentDataItem))),
            e.IsNew ? "등록했습니다." : "저장했습니다.",
            e.IsNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (!saved)
        {
            // 실패하면 팝업을 닫지 않는다. 닫으면 쓴 내용이 사라진다.
            e.Cancel = true;
            return;
        }

        await Reload.InvokeAsync();
    }

    private async Task OnDataItemDeletingAsync(TreeListDataItemDeletingEventArgs e)
    {
        if (e.DataItem is not TItem item)
        {
            return;
        }

        // 실패하면 나무에서 줄을 걷어내지 않는다 — 서버에는 남아 있는데 화면에서만 사라지면 다시 읽을 때까지 틀린다.
        if (!await DeleteAsync(item))
        {
            e.Cancel = true;
        }
    }

    /// <summary>지우고 목록을 다시 읽는다. 성공했으면 <c>true</c>.</summary>
    private async Task<bool> DeleteAsync(TItem item)
    {
        if (!await RunAsync(() => OnDelete.InvokeAsync(item), "삭제했습니다.", "삭제하지 못했습니다"))
        {
            return false;
        }

        await Reload.InvokeAsync();
        return true;
    }

    /// <summary>부모 줄. 최상위면 <c>null</c> 이다.</summary>
    private static TItem? Parent(object? parentDataItem) =>
        parentDataItem is TItem parent ? parent : default;
}
