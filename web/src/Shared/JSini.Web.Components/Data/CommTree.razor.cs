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

    /// <summary>팝업 편집 폼.</summary>
    [Parameter] public RenderFragment<TreeListEditFormTemplateContext>? EditFormTemplate { get; set; }

    /// <summary>나무 위에 놓을 것. 보통은 비운다 — 조건은 CommSch 로 간다.</summary>
    [Parameter] public RenderFragment? Toolbar { get; set; }

    /// <summary>
    /// 아래 띠의 왼쪽. <b>동작 단추를 두지 않는다</b> — 그것은
    /// <see cref="ContextMenuItems"/> 로 간다. 상태 표시처럼 누를 것이 아닌 것만 둔다.
    /// </summary>
    [Parameter] public RenderFragment? FooterLeft { get; set; }

    /// <summary>아래 띠의 오른쪽 끝. <see cref="FooterLeft"/> 와 같은 규칙이다.</summary>
    [Parameter] public RenderFragment? FooterRight { get; set; }

    /// <summary>
    /// 오른쪽 클릭 창에 보탤 화면만의 동작. 안에 <see cref="CommMenuItem"/> 을
    /// 적는다(권한은 <c>PermissionView</c> 로 감싼다). 「등록」 바로 아래에 선다.
    /// </summary>
    [Parameter] public RenderFragment? ContextMenuItems { get; set; }

    /// <summary>빈 목록 자리를 통째로 갈아 끼울 때.</summary>
    [Parameter] public RenderFragment? EmptyArea { get; set; }

    /// <summary>
    /// 관리 칸에서 「하위 추가」 앞에 놓을 단추들. 그 줄의 자료를 그대로 받는다.
    ///
    /// <para>
    /// 미리보기·복제처럼 <b>그 화면에만 있는 동작</b>을 두는 자리다. 제목 칸의
    /// <c>CellDisplayTemplate</c> 에 끼워 넣으면 화면마다 조작 자리가 달라져,
    /// 「관리 칸에 있겠거니」 하고 오른쪽 끝을 보는 사람이 못 찾는다.
    /// </para>
    /// </summary>
    [Parameter] public RenderFragment<TItem>? RowActions { get; set; }

    /// <summary>
    /// 관리 칸의 너비(px). 비우면 단추 수에 맞춘 기본값을 쓴다 —
    /// 하위 추가·수정·삭제 셋이면 116, <see cref="RowActions"/> 가 있으면 148.
    /// 단추를 더 넣으면 여기서 직접 정한다.
    /// </summary>
    [Parameter] public int? ActionsWidth { get; set; }

    /// <summary>자료가 없을 때 보여 줄 문구.</summary>
    [Parameter] public string EmptyText { get; set; } = "표시할 자료가 없습니다.";

    /// <summary>팝업 편집 폼의 제목.</summary>
    [Parameter] public string EditFormTitle { get; set; } = "편집";

    /// <summary>아래 띠와 오른쪽 클릭 창을 쓸지. 끄면 등록·다시 읽기·펼치기·칸별 검색·엑셀이 함께 사라진다.</summary>
    [Parameter] public bool ShowFooter { get; set; } = true;

    /// <summary>
    /// 관리 칸의 「하위 추가」. <see cref="OnSave"/> 를 준 화면에서만 보인다.
    ///
    /// <para>
    /// 끄는 자리는 <b>깊이가 정해진 나무</b>다 — 회사 &gt; 부서처럼 두 단이
    /// 전부인 곳에서는 부서 아래 하위 추가가 뜻이 없다.
    /// </para>
    /// </summary>
    [Parameter] public bool ShowAddChild { get; set; } = true;

    /// <summary>모두 펼치기·접기 토글 단추.</summary>
    [Parameter] public bool ShowExpandToggle { get; set; } = true;

    /// <summary>칸별 검색 줄 토글 단추. 시작 상태는 화면의 <c>ShowFilterRow</c> 가 정한다.</summary>
    [Parameter] public bool ShowFilterToggle { get; set; } = true;

    /// <summary>엑셀 내보내기. 켜져 있어도 <c>use_excel</c> 권한이 없으면 안 보인다.</summary>
    [Parameter] public bool ShowExcelExport { get; set; } = true;

    /// <summary>
    /// 짝수 줄에 옅은 바탕을 깐다(줄무늬).
    ///
    /// <para>
    /// <b>나무에서는 CommGrd 보다 덜 중요하다</b> — 들여쓰기가 이미 줄을
    /// 가른다. 그래도 칸이 열몇 개면 오른쪽 끝에서 한 줄을 잘못 짚는 것은
    /// 마찬가지라 기본으로 켜 둔다.
    /// </para>
    /// </summary>
    [Parameter] public bool AlternateRows { get; set; } = true;

    /// <summary>내려받을 파일 이름(확장자 없이).</summary>
    [Parameter] public string ExportName { get; set; } = "목록";

    /// <summary>
    /// 새 줄의 기본값을 채운다. <b>둘째 값이 부모</b>다 —
    /// 「하위 추가」로 열었으면 그 줄, 오른쪽 클릭 창의 「등록」이면 <c>null</c>(최상위).
    /// 부모 열쇠(<c>Pid</c>)를 여기서 넣는다.
    /// </summary>
    [Parameter] public Action<TItem, TItem?>? OnNew { get; set; }

    /// <summary>
    /// 편집 창이 열릴 때. 등록이든 수정이든 부른다 —
    /// <c>bool</c> 이 「새로 만드는 것인가」다.
    ///
    /// <para>
    /// <see cref="OnNew"/> 로는 모자란 자리가 있어 두었다. 폼 안의 상태를
    /// 화면이 들고 있어야 하는 경우(고른 첨부파일 같은 것)에는 <b>수정 창을
    /// 열 때도</b> 그 상태를 비우거나 채워야 하는데, 그쪽은 등록일 때만 불린다.
    /// </para>
    /// </summary>
    [Parameter] public Action<TItem, bool>? OnEditOpen { get; set; }

    /// <summary>
    /// 저장. 주면 「등록」·「하위 추가」·「수정」이 생긴다.
    /// <c>Parent</c> 는 새로 만들 때의 부모다(수정이면 그 줄의 부모).
    /// </summary>
    [Parameter] public EventCallback<(TItem Item, bool IsNew, TItem? Parent)> OnSave { get; set; }

    /// <summary>삭제. 주면 「삭제」가 생긴다.</summary>
    [Parameter] public EventCallback<TItem> OnDelete { get; set; }

    /// <summary>목록을 다시 읽는다. 저장·삭제 뒤에도 부른다.</summary>
    [Parameter] public EventCallback Reload { get; set; }

    /// <summary>
    /// 삭제 확인 문구를 화면이 정한다. 주면 DevExpress 기본 확인창 대신
    /// <c>ConfirmDialog</c> 로 묻는다.
    ///
    /// <para>
    /// <b>나무에서는 CommGrd 보다 자주 필요하다</b> — 가지를 지우면 그 아래가
    /// 함께 사라지는데, 기본 확인창은 어느 줄인지도 무엇이 딸려 가는지도
    /// 말해 주지 않는다.
    /// </para>
    /// </summary>
    [Parameter] public Func<TItem, string>? DeleteConfirm { get; set; }

    /// <summary>
    /// 고른 줄. <see cref="SelectedItemChanged"/> 와 함께 주면
    /// <c>@bind-SelectedItem</c> 처럼 화면이 선택의 주인이 된다.
    ///
    /// <para>
    /// <b>둘 중 하나만 주지 않는다.</b> 값 없이 알림만 받으면 화면이 고른 줄을
    /// 보관하지 않는다는 뜻이 되어, 매 렌더마다 선택이 <c>null</c> 로 되돌아간다
    /// (줄을 눌러도 강조가 곧 풀린다).
    /// </para>
    /// </summary>
    [Parameter] public TItem? SelectedItem { get; set; }

    /// <summary>고른 줄이 바뀌었을 때. 여기서 받은 값이 다음 렌더의 선택이 된다.</summary>
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }

    /// <summary>
    /// 줄을 끌어 옮길 수 있는가. <b>권한으로 켜고 끄는 자리</b>다 —
    /// <c>AllowDragRows="@(_canMove)"</c>.
    ///
    /// <para>
    /// 「수정」 단추를 감추는 것과 <b>같은 판정</b>을 써야 한다
    /// (<c>PermissionPath</c>). 두 판정이 갈리면 「단추는 없는데 끌어
    /// 옮기기는 된다」가 된다. <c>PermissionView</c> 로는 할 수 없다 —
    /// 그릴지 말지가 아니라 파라미터로 켜고 끄는 것이라서.
    /// </para>
    /// </summary>
    [Parameter] public bool AllowDragRows { get; set; }

    /// <summary>
    /// 어디에 놓을 수 있는가. 기본은 <b>이 나무 안</b>이다.
    ///
    /// <para>
    /// 다른 부품으로 끌고 나가는 길(<c>External</c>)을 열어 두면 받을 곳이
    /// 없는 화면에서 <b>끌다가 사라지는 것처럼</b> 보인다. 받을 부품을 실제로
    /// 둔 화면만 이 값을 바꾼다.
    /// </para>
    /// </summary>
    [Parameter] public TreeListAllowedDropTarget AllowedDropTarget { get; set; } = TreeListAllowedDropTarget.Internal;

    /// <summary>
    /// 놓을 자리를 줄 <b>사이</b>까지 잡는다. 기본이 그렇다.
    ///
    /// <para>
    /// <c>Component</c> 로 두면 놓을 자리가 「이 나무 안」 하나뿐이라
    /// <b>상위 변경만</b> 되고 형제 사이 순서는 손댈 수 없다. 나무에서 순서가
    /// 뜻을 갖는 자리(메뉴 차례)가 대부분이라 <c>BetweenRows</c> 를 기본으로 둔다.
    /// </para>
    /// </summary>
    [Parameter] public TreeListDropTargetMode DropTargetMode { get; set; } = TreeListDropTargetMode.BetweenRows;

    /// <summary>
    /// 줄을 끌어다 놓았을 때. 어디서 어디로 갔는지는 이 알림이 준다 —
    /// 놓은 줄(<c>DroppedItems</c>) · 놓인 자리(<c>TargetItem</c> ·
    /// <c>DropPosition</c>).
    ///
    /// <para>
    /// <b>이 이름을 선언해 둔 이유는 splat 으로 넘길 수 없기 때문이다.</b>
    /// <c>EventCallback</c> 은 Razor 가 화면 쪽에서 감싸 줘야 하고, 그러려면
    /// 이 부품이 그 이름을 파라미터로 알고 있어야 한다.
    /// </para>
    /// </summary>
    [Parameter] public EventCallback<TreeListItemsDroppedEventArgs> ItemsDropped { get; set; }

    /// <summary>선언하지 않은 모든 DxTreeList 파라미터.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Extra { get; set; }

    /// <summary>감싼 DxTreeList. 화면이 DevExpress API 를 직접 불러야 할 때.</summary>
    public ITreeList? Tree => _tree;

    /// <summary>관리 칸 너비(px).</summary>
    private int ActionsPixels => ActionsWidth ?? (RowActions is null ? 116 : 148);

    /// <summary><c>Width</c> 는 글자로, <c>MinWidth</c> 는 정수로 받는다.</summary>
    private string ActionsColumnWidth =>
        ActionsPixels.ToString(System.Globalization.CultureInfo.InvariantCulture);

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

    /// <summary>
    /// 화면이 <see cref="SelectedItem"/> 을 <b>주었는지</b> 본다.
    ///
    /// <para>
    /// 값이 <c>null</c> 인 것과 아예 주지 않은 것을 갈라야 해서 파라미터로는
    /// 알 수 없다 — 둘 다 <c>null</c> 로 보인다. <c>ParameterView</c> 에는
    /// 화면이 실제로 적어 준 것만 들어 있으므로 여기서만 구분이 된다.
    /// </para>
    /// </summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // object 로 받는다. TItem 으로 받으면 값 형식일 때 null 을 캐스팅하다 죽는다.
        _selectionBound = parameters.TryGetValue<object>(nameof(SelectedItem), out _);

        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        _forwarded = Coerce(Extra);

        RequireTreeShape();

        // 선택의 주인이 화면이면 그 값을 따른다. 아니면 나무가 들고 있는 것을
        // 그대로 둔다 — 여기서 안 준 값(늘 null)을 덮어쓰면 줄을 눌러도
        // 강조가 곧 풀린다.
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

    /// <summary>
    /// 나무 모양을 알려 주었는지 본다.
    ///
    /// <para>
    /// [증상이 「나무인데 나무가 아님」이라 막는다]
    /// </para>
    ///
    /// <para>
    /// 열쇠나 자식 칸을 빠뜨리면 DevExpress 는 아무 말 없이 <b>평평한 목록</b>을
    /// 그린다. 자료가 나무 꼴로 들어오는 화면에서는 그때 뿌리 줄만 보이므로
    /// (자식은 어디에도 안 그려진다) 「자료가 절반만 온다」로 읽힌다.
    /// 자료를 의심하며 게이트웨이·서비스·DB 를 뒤지게 되는 종류의 실수다.
    /// </para>
    ///
    /// <para>
    /// 자식을 그때그때 불러오는 화면(<c>ChildrenLoadingOnDemand</c>)은
    /// 자식 칸이 없어도 되므로 그 경우는 통과시킨다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 칸 머리 클릭을 오름 → 내림 → 없음으로 돌린다. DevExpress 는 둘만 오가서
    /// 원래 차례로 되돌릴 길이 없다. 내림이던 칸이 다시 오름이 되면 세 번째
    /// 클릭이므로 정렬을 푼다.
    ///
    /// <para>
    /// <b>나무에서 이것이 CommGrd 보다 더 중요하다.</b> 나무의 원래 차례는
    /// 서버가 준 계층·순서이고, 그것이 곧 화면의 뜻이다(메뉴 순서가 그렇다).
    /// 되돌릴 길이 없으면 칸 머리를 한 번 잘못 눌렀을 때 다시 읽기 전까지
    /// 순서를 볼 수 없다.
    /// </para>
    ///
    /// <para>
    /// <c>OnAfterRender</c> 에 두면 안 된다 — 정렬은 <c>DxTreeList</c>
    /// <b>안에서 끝나는 일</b>이라 부모인 우리는 다시 그려지지 않는다.
    /// layout 이 바뀔 때 알려 주는 <c>LayoutAutoSaving</c> 에 건다
    /// (CommGrd 에서 한동안 안 돌고 있었던 것이 그 이유였다).
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 모두 펼치기 · 모두 접기.
    ///
    /// <para>
    /// <c>AutoExpandAllNodes</c> 를 켜는 것과 다르다 — 그것은 파라미터라서
    /// 사람이 접은 것을 다음 렌더에 다시 펴 버린다. 여기서는 한 번만 부른다.
    /// </para>
    /// </summary>
    private void ToggleExpand()
    {
        if (_tree is null)
        {
            return;
        }

        _expanded = !_expanded;

        if (_expanded)
        {
            _tree.ExpandAll();
        }
        else
        {
            _tree.CollapseAll();
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

    /// <summary>
    /// 오른쪽 클릭 창에 올릴 것. 옛 아래 띠의 아이콘과 **같은 조건**이다
    /// (`CommGrd.BuildMenu` 와 같은 차례에 「모두 펼치기 · 접기」가 더해진다).
    /// </summary>
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

    /// <summary>
    /// 짝수 줄에 표시 클래스를 붙이고, 자료 칸을 가운데로 맞춘다.
    ///
    /// <para>
    /// 줄무늬는 쪽 보정이 필요 없다 — 쪽나누기를 끄고 있어서
    /// <c>VisibleIndex</c> 가 곧 보이는 자리다(CommGrd 는 쪽마다 무늬가
    /// 뒤집히지 않게 <c>PageOffset</c> 을 뺀다).
    /// </para>
    ///
    /// <para>
    /// [첫 칸은 가운데로 맞추지 않는다 — 계층 칸이다]
    /// </para>
    ///
    /// <para>
    /// 첫 자료 칸의 셀 안에는 들여쓰기와 펼침 화살표가 함께 들어 있다.
    /// 가운데로 맞추면 <b>깊이마다 제목이 다른 자리에서 시작</b>해 계층이
    /// 눈에 안 들어온다. 그래서 그 칸만 건드리지 않는다.
    /// </para>
    /// </summary>
    private void OnCustomizeElement(TreeListCustomizeElementEventArgs e)
    {
        if (AlternateRows
            && e.ElementType == TreeListElementType.DataRow
            && e.VisibleIndex % 2 == 1)
        {
            e.CssClass = "commtree__row--alt";
            return;
        }

        // 칸이 TextAlignment 를 직접 정했으면(Auto 가 아니면) 건드리지 않는다 —
        // 그 값이 그 칸의 뜻이다. CSS 로만 하면 이 구분을 할 수 없다:
        // DevExpress 는 우리가 정한 것과 자기가 자료형을 보고 정한 것(숫자는
        // 오른쪽)에 **같은 클래스**를 붙인다.
        if (e.ElementType == TreeListElementType.DataCell
            && e.Column?.TextAlignment == TreeListTextAlignment.Auto
            && !IsTreeColumn(e))
        {
            e.CssClass = "commtree__cell--center";
        }
    }

    /// <summary>
    /// 계층 칸인가 — <b>보이는 칸들 중 첫째</b>다.
    ///
    /// <para>
    /// <c>e.Column.VisibleIndex</c> 로 가릴 수 없다. 이 알림에서 그 값은
    /// <b>어느 칸이든 -1</b> 이다(화면에 클래스로 찍어 재어 봤다). 그대로
    /// 믿으면 「0보다 큰 칸」이 하나도 없어 <b>가운데 정렬이 통째로 안 걸린다</b> —
    /// 아무 예외도 나지 않고 그냥 전부 왼쪽에 붙는다.
    /// </para>
    ///
    /// <para>
    /// 대신 <c>GetVisibleColumns()</c> 의 첫째와 견준다. 이 알림이 주는
    /// <c>e.Column</c> 이 그 목록에 있는 <b>같은 객체</b>라서 참조 비교가
    /// 성립한다(같은 화면에서 확인했다). 목록에는 끌기 손잡이나 펼침 칸 같은
    /// 가짜 칸이 섞이지 않는다 — 화면이 적은 칸만 들어 있다.
    /// </para>
    /// </summary>
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

            // 대리자는 감쌀 수 없다 — 감싸면 알림 받는이가 이 부품이 되어
            // 화면이 다시 그려지지 않는다(OnSelectedDataItemChangedAsync 주석).
            // 그래서 바꿔 주는 대신 여기서 막는다. 안 막으면 DevExpress 가
            // 대입할 때 죽고, 그때 나오는 말은
            // "Unable to cast … Func`2 … to EventCallback`1" 뿐이라
            // 어느 화면의 어느 줄인지 알 수 없다.
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

    /// <summary>
    /// DxTreeList 의 선택 알림을 화면이 아는 형으로 옮긴다.
    ///
    /// <para>
    /// 이 다리가 있어야 하는 이유는 <b>알림을 누가 받는가</b>다. Razor 가
    /// 화면의 <c>SelectedItemChanged</c> 를 감쌀 때 받는이로 <b>화면</b>을
    /// 적어 주므로, 알림이 끝나면 화면이 다시 그려진다. 여기서 대리자를
    /// 직접 감싸면 받는이가 이 부품이 되어 <b>나무만</b> 다시 그려진다 —
    /// 고른 줄에 딸린 오른쪽 칸이 안 바뀌는 쪽으로 조용히 틀린다.
    /// </para>
    /// </summary>
    private Task OnSelectedDataItemChangedAsync(object? item)
    {
        _selected = item;

        return SelectedItemChanged.InvokeAsync(item is TItem typed ? typed : default);
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

    /// <summary>
    /// 삭제 확인. <see cref="DeleteConfirm"/> 을 준 화면만 우리 확인창을 쓴다.
    ///
    /// <para>
    /// 우리 확인창을 쓸 때는 DevExpress 에 삭제를 맡기지 않는다 — 맡기면
    /// 기본 확인창이 <b>한 번 더</b> 뜬다. 지우고 목록을 다시 읽는 일은
    /// <see cref="DeleteAsync"/> 한 곳에 모아 두어 두 길이 갈리지 않는다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 지금 보이는 그대로(정렬·필터가 걸린 상태) 내린다.
    ///
    /// <para>
    /// <b>접힌 가지도 함께 내린다</b>(<c>RowExpandMode</c>). 기본값은
    /// 화면의 펼침 상태를 그대로 옮기는 것인데, 그러면 접어 둔 가지가
    /// <b>말없이 빠진</b> 파일이 나온다 — 받은 사람은 그 사실을 알 길이 없다.
    /// </para>
    /// </summary>
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

        // 실패하면 나무에서 줄을 걷어내지 않는다. 걷어내면 서버에는 남아 있는데
        // 화면에서는 사라져, 다시 읽을 때까지 지운 줄 안다.
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
