using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Data;

public partial class CommGrd<TItem>
{
    [Inject] private ThemeSize Size { get; set; } = default!;

    /// <summary><see cref="StateKey"/> 를 적은 표의 모습을 맡아 두는 곳.</summary>
    [Inject] private ScreenState Screen { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>표에 넣을 자료.</summary>
    [Parameter, EditorRequired] public object Data { get; set; } = default!;

    /// <summary>표의 칸들. 순번·관리 칸은 이 부품이 붙인다.</summary>
    [Parameter, EditorRequired] public RenderFragment? Columns { get; set; }

    /// <summary>팝업 편집 폼.</summary>
    [Parameter] public RenderFragment<GridEditFormTemplateContext>? EditFormTemplate { get; set; }

    /// <summary>표 위에 놓을 것. 보통은 비운다 — 조건은 CommSch 로 간다.</summary>
    [Parameter] public RenderFragment? Toolbar { get; set; }

    /// <summary>아래 띠 왼쪽. 동작 단추는 두지 않는다 — 동작은 ContextMenuItems 로 간다. 상태 표시처럼 누를 것이 아닌 것만 둔다.</summary>
    [Parameter] public RenderFragment? FooterLeft { get; set; }

    /// <summary>아래 띠의 오른쪽 끝. <see cref="FooterLeft"/> 와 같은 규칙이다.</summary>
    [Parameter] public RenderFragment? FooterRight { get; set; }

    /// <summary>오른쪽 클릭 창에 보탤 화면만의 동작. 안에 CommMenuItem 을 적는다(권한은 PermissionView 로 감싼다).</summary>
    [Parameter] public RenderFragment? ContextMenuItems { get; set; }

    /// <summary>빈 목록 자리를 통째로 갈아 끼울 때.</summary>
    [Parameter] public RenderFragment? EmptyArea { get; set; }

    /// <summary>줄을 펼쳤을 때 안에 그릴 것.</summary>
    [Parameter] public RenderFragment<GridDetailRowTemplateContext>? DetailRowTemplate { get; set; }

    /// <summary>관리 칸에서 「수정」 앞에 놓을 단추들. 그 줄의 자료를 받는다. 제목 칸에 끼우지 않는다 — 화면마다 조작 자리가 달라진다.</summary>
    [Parameter] public RenderFragment<TItem>? RowActions { get; set; }

    /// <summary>표 위 머리줄의 제목. 자료 영역이 하나뿐인 화면에는 적지 않는다 — 사이드바·브레드크럼과 같은 글자가 세 곳에 선다.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>머리줄에 적을 건수. 안 주면 Data 를 센다. 표가 든 것이 전부가 아닐 때(서버가 쪽을 잘라 줄 때)만 준다.</summary>
    [Parameter] public int? Total { get; set; }

    /// <summary>건수를 세는 낱말. 기본은 「건」. 기기는 「대」, 회사는 「곳」처럼 그 화면이 쓰던 말을 쓸 때만 바꾼다.</summary>
    [Parameter] public string Unit { get; set; } = "건";

    /// <summary>머리줄(제목·건수)을 그릴지. 기본으로 켠다. 끄는 자리는 줄이 서넛뿐인 작은 표다.</summary>
    [Parameter] public bool ShowHeader { get; set; } = true;

    /// <summary>머리줄(상단)에 건수를 표시할지 여부. 기본은 거짓(보이지 않음). 보이고 싶으면 ShowHeaderCount="true".</summary>
    [Parameter] public bool ShowHeaderCount { get; set; }

    /// <summary>아래 띠(페이지네비게이션 영역)에 건수를 표시할지 여부. 기본은 참(보임).</summary>
    [Parameter] public bool ShowFooterCount { get; set; } = true;

    /// <summary>아래 띠(페이지네비게이션 영역)에 contextmenu 에 있던 동작 단추(등록·다시읽기·검색줄·엑셀 등)를 표시할지 여부. 기본은 참(보임).</summary>
    [Parameter] public bool ShowFooterActions { get; set; } = true;

    /// <summary>페이지네비게이션 영역(페이저)을 보일지 여부. 기본은 참(보임).</summary>
    [Parameter] public bool PagerVisible { get; set; } = true;

    /// <summary>자료가 없을 때 보여 줄 문구.</summary>
    [Parameter] public string EmptyText { get; set; } = "표시할 자료가 없습니다.";

    /// <summary>팝업 편집 폼의 제목.</summary>
    [Parameter] public string EditFormTitle { get; set; } = "편집";

    /// <summary>아래 띠와 오른쪽 클릭 창을 쓸지. 끄면 등록·다시 읽기·칸별 검색·엑셀이 함께 사라진다.</summary>
    [Parameter] public bool ShowFooter { get; set; } = true;

    /// <summary>맨 왼쪽 순번 칸. 기본으로 그리지 않는다 — 번호는 자리라서 정렬하면 어긋난다. 번호를 소리 내어 부르는 표에서만 ShowRowNumber="true".</summary>
    [Parameter] public bool ShowRowNumber { get; set; }

    /// <summary>칸별 검색 줄 토글 단추. 시작 상태는 화면의 <c>ShowFilterRow</c> 가 정한다.</summary>
    [Parameter] public bool ShowFilterToggle { get; set; } = true;

    /// <summary>엑셀 내보내기. 켜져 있어도 <c>use_excel</c> 권한이 없으면 안 보인다.</summary>
    [Parameter] public bool ShowExcelExport { get; set; } = true;

    /// <summary>짝수 줄에 옅은 바탕을 깐다(줄무늬). 칸이 많은 표에서 한 줄을 잘못 짚는 것을 막는다.</summary>
    [Parameter] public bool AlternateRows { get; set; } = true;

    /// <summary>휴대폰 폭(≤767px)에서 표를 카드 목록으로 편다. 기본은 켜짐. 폭은 CSS 가 정하고, 한 장으로 읽히지 않는 표만 MobileCards="false" 로 끈다.</summary>
    [Parameter] public bool MobileCards { get; set; } = true;

    /// <summary>내려받을 파일 이름(확장자 없이).</summary>
    [Parameter] public string ExportName { get; set; } = "목록";

    /// <summary>새 행의 기본값을 채운다. 부모 키를 여기서 넣는다.</summary>
    [Parameter] public Action<TItem>? OnNew { get; set; }

    /// <summary>편집 창이 열릴 때 부른다(등록·수정 모두). bool 이 「새로 만드는 것인가」다. 폼 상태를 수정 창을 열 때도 비워야 하면 이것을 쓴다.</summary>
    [Parameter] public Action<TItem, bool>? OnEditOpen { get; set; }

    /// <summary>저장. 주면 아래 띠에 ＋ 가 생긴다 — 고칠 수 있는 목록이라는 뜻이 이 하나다. 편집 자리가 표 밖이면 화면이 SaveAsync 로 직접 부른다.</summary>
    [Parameter] public EventCallback<(TItem Item, bool IsNew)> OnSave { get; set; }

    /// <summary>＋ 에 붙는 말풍선. 「새 작업」처럼 화면의 말이 따로 있으면 적는다.</summary>
    [Parameter] public string CreateTitle { get; set; } = "등록";

    /// <summary>삭제. 주면 「삭제」가 생긴다.</summary>
    [Parameter] public EventCallback<TItem> OnDelete { get; set; }

    /// <summary>목록을 다시 읽는다. 저장·삭제 뒤에도 부른다.</summary>
    [Parameter] public EventCallback Reload { get; set; }

    /// <summary>줄마다 덧붙일 CSS 클래스. 줄의 상태(폐기·마감 …)를 줄 전체의 생김새로 보여 줄 때 쓴다. null 이면 붙이지 않는다.</summary>
    [Parameter] public Func<TItem, string?>? RowCssClass { get; set; }

    /// <summary>삭제 확인 문구. 주면 기본 확인창 대신 ConfirmDialog 로 묻는다 — 딸린 것이 함께 사라지는 자리에 쓴다.</summary>
    [Parameter] public Func<TItem, string>? DeleteConfirm { get; set; }

    /// <summary>고른 줄. SelectedItemChanged 와 함께 준다 — 한쪽만 주면 고른 줄이 매 렌더마다 null 로 되돌아간다.</summary>
    [Parameter] public TItem? SelectedItem { get; set; }

    /// <summary>고른 줄이 바뀌었을 때. 여기서 받은 값이 다음 렌더의 선택이 된다.</summary>
    [Parameter] public EventCallback<TItem?> SelectedItemChanged { get; set; }

    /// <summary>줄을 두 번 눌렀을 때. 여는 동작에만 쓴다 — 두 번 누르기는 화면에 안 보이는 몸짓이라 되돌리기 어려운 동작은 걸지 않는다.
    /// splat 으로 넘기면 화면이 500 으로 죽으므로 이 이름을 선언해 둔다.</summary>
    [Parameter] public EventCallback<TItem> OnRowDoubleClick { get; set; }
    /// <summary>줄을 눌렀을 때.</summary>
    [Parameter] public EventCallback<TItem> OnRowClick { get; set; }


    /// <summary>한 줄만 고를 수 있는가. 여럿으로 바꾸면 체크 칸을 Columns 맨 앞에 손으로 넣는다(자동으로 붙이지 않는다 — 자리·폭이 화면마다 다르다).</summary>
    [Parameter] public GridSelectionMode SelectionMode { get; set; } = GridSelectionMode.Single;

    /// <summary>줄을 가리키는 열쇠 칸. 주면 자료를 다시 읽어도 고른 줄이 같은 열쇠를 따라 강조가 남는다. 안 주면 객체 동일성으로 찾는다.</summary>
    [Parameter] public string? KeyFieldName { get; set; }

    /// <summary>컬럼 너비 조절 방식. 기본은 ColumnsContainer(칸 머리를 끌어 조절).</summary>
    [Parameter] public GridColumnResizeMode ColumnResizeMode { get; set; } = GridColumnResizeMode.ColumnsContainer;

    /// <summary>자료가 들어오면 첫 줄을 스스로 고른다(기본 켬). 이미 고른 줄이 있으면 건드리지 않는다. 여러 줄 고르기에서는 하지 않는다 — 첫 줄이 체크되면 일괄 처리 대상이 된다.</summary>
    [Parameter] public bool AutoSelectFirstRow { get; set; } = true;

    /// <summary>체크한 줄들. SelectedItemsChanged 와 함께 준다 — 한쪽만 주면 체크가 곧 풀린다.</summary>
    [Parameter] public IReadOnlyList<TItem>? SelectedItems { get; set; }

    /// <summary>체크가 바뀌었을 때. 여기서 받은 값이 다음 렌더의 선택이 된다.</summary>
    [Parameter] public EventCallback<IReadOnlyList<TItem>> SelectedItemsChanged { get; set; }

    /// <summary>줄을 끌어 옮길 수 있는가. 권한으로 켜고 끈다. 「수정」 단추를 감추는 것과 같은 PermissionPath 판정을 써야 어긋나지 않는다. 차례가 뜻을 갖는 표에서만 켠다.</summary>
    [Parameter] public bool AllowDragRows { get; set; }

    /// <summary>놓을 수 있는 곳. 기본은 이 표 안. 다른 부품으로 끌고 나가는 길(External)은 받을 부품을 실제로 둔 화면만 연다.</summary>
    [Parameter] public GridAllowedDropTarget AllowedDropTarget { get; set; } = GridAllowedDropTarget.Internal;

    /// <summary>놓을 자리를 줄 사이까지 잡는다(기본). Component 로 두면 차례를 바꿀 수 없다.</summary>
    [Parameter] public GridDropTargetMode DropTargetMode { get; set; } = GridDropTargetMode.BetweenRows;

    /// <summary>줄을 끌어다 놓았을 때. 놓은 줄(DroppedItems)·놓인 자리(TargetItem·DropPosition)를 준다. splat 으로 넘기면 대리자가 감싸이지 않아 500 이 나므로 이름을 선언한다. 표는 스스로 옮기지 않는다.</summary>
    [Parameter] public EventCallback<GridItemsDroppedEventArgs> ItemsDropped { get; set; }

    /// <summary>표의 모습을 화면을 떠났다 돌아와도 맡길 이름(관례는 화면 경로). 겹치면 칸 구성이 다른 표끼리 모습을 덮어쓴다. 구른 자리는 ScrollSlot 이 따로 잰다.</summary>
    [Parameter] public string? StateKey { get; set; }

    /// <summary>선언하지 않은 모든 DxGrid 파라미터.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Extra { get; set; }

    /// <summary>감싼 DxGrid. 화면이 DevExpress API 를 직접 불러야 할 때.</summary>
    public IGrid? Grid => _grid;

    /// <summary>머리줄을 그리는가. 적을 것이 없으면 켜 두어도 안 그린다.</summary>
    private bool HeadVisible =>
        ShowHeader && (!string.IsNullOrWhiteSpace(Title) || (ShowHeaderCount && _total is not null));

    /// <summary>아래 띠(페이지네비게이션 영역)에 표시할 내용이 있는가.</summary>
    private bool HasFooterContent =>
        (ShowFooterCount && _total is not null)
        || (ShowFooterActions && HasAnyFooterAction)
        || FooterLeft is not null
        || FooterRight is not null;

    private bool HasAnyFooterAction =>
        (OnSave.HasDelegate && Can(MenuAction.Create))
        || Reload.HasDelegate
        || (ShowFilterToggle && !Carded)
        || _hasFilter
        || (ShowExcelExport && Can(MenuAction.Excel))
        || _screenItems.Count > 0;

    /// <summary>천 단위를 끊어 적는다. 서식은 자리에 따라 흔들리면 안 되므로 불변 문화다.</summary>
    private static string Count(int value) =>
        value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Data 의 건수 — 셀 수 있는 자료(목록·DataTable)만 센다. 질의를 여기서 세면 다시 그릴 때마다 자료를 또 읽으므로 그런 화면은 Total 로 적는다.</summary>
    private static int? CountOf(object? data) => data switch
    {
        null => null,
        System.Data.DataTable table => table.Rows.Count,
        System.Collections.ICollection rows => rows.Count,
        _ => null,
    };

    /// <summary>관리 칸 너비(px). 「⋮」 하나만 서므로 화면과 무관하게 같다.</summary>
    private const int ActionsPixels = 48;

    /// <summary>관리 칸을 세우는가 — 그 안의 판도 같은 조건이다.</summary>
    private bool HasActions => EditsInPopup || OnDelete.HasDelegate || RowActions is not null;

    /// <summary><c>Width</c> 는 글자로, <c>MinWidth</c> 는 정수로 받는다.</summary>
    private string ActionsColumnWidth =>
        ActionsPixels.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private IGrid? _grid;
    private IReadOnlyDictionary<string, object>? _forwarded;

    /// <summary>우리가 걷혔는가(화면이 이 표를 render tree 에서 뺐는가). _grid 가 남아 있어도 살아 있다는 뜻이 아니다 — 걷힌 뒤 GetVisibleRowCount 를 부르면 회로가 내려간다.</summary>
    private bool _gone;

    void IDisposable.Dispose() => _gone = true;

    /// <summary>표를 감싼 칸. 구를 칸을 이 안에서 찾는다(<c>grid-scroll.js</c>).</summary>
    private ElementReference _root;

    private IJSObjectReference? _scrollJs;

    /// <summary>구른 자리를 되돌리는 일이 끝났는가. 끝나면 더 부르지 않는다.</summary>
    private bool _scrollSynced;

    /// <summary>표가 지금 들고 있는 선택. 화면이 값을 주면 그것을 따른다.</summary>
    private object? _selected;

    /// <summary>첫 줄 자동 고르기를 이미 해 준 자료 묶음. 같은 것이면 다시 안 한다.</summary>
    private object? _autoPickedFor;

    /// <summary>표에 넘길 체크 목록. DevExpress 가 IReadOnlyList&lt;object&gt; 를 받으므로 한 겹 옮긴다.</summary>
    private IReadOnlyList<object>? _selectedMany;

    /// <summary>이번 렌더에 화면이 <see cref="SelectedItems"/> 를 주었는가.</summary>
    private bool _manyBound;

    /// <summary><see cref="DeleteConfirm"/> 을 준 화면에서만 쓰는 확인창.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>이번 렌더에 화면이 <see cref="SelectedItem"/> 을 주었는가.</summary>
    private bool _selectionBound;

    /// <summary>머리줄에 적을 전체 건수. 화면이 준 값이거나 <c>Data</c> 를 센 값이다.</summary>
    private int? _total;

    /// <summary>칸별 검색이 걸렸을 때 남은 줄 수. 표에 물어야 나온다 — 거르기는 표 안에서 끝나므로 화면이 든 목록은 그대로다.</summary>
    private int? _filtered;

    private bool _filterRow;
    private bool _filterRowSeeded;
    private bool _hasFilter;
    private bool _virtualScroll;

    /// <summary>쪽 넘김 줄을 보이는가. 카드로 펴면 켜진다(<see cref="ApplyScrollMode"/>).</summary>
    private bool _pagerVisible;

    /// <summary>한 쪽에 담는 줄 수.</summary>
    private int _pageSize = DefaultPageSize;

    /// <summary>화면이 따로 정하지 않았을 때의 한 쪽 크기.</summary>
    private const int DefaultPageSize = 15;

    /// <summary>지금 표가 든 줄 수. 셀 수 없는 자료 원본이면 <c>null</c>.</summary>
    private int? _rowCount;

    /// <summary>휴대폰 폭(≤767px)인가. 카드로 펴는 일은 CSS 가 하고, 이 값은 카드일 때 가상 스크롤을 끄는 데만 쓴다. 첫 그림 뒤에 오므로 부품은 걷히지 않는다.</summary>
    private bool _isPhone;

    /// <summary>오른쪽 클릭 창에서 카드/표 보기를 사용자가 직접 고른 값.</summary>
    private bool? _userCardView;

    /// <summary>지금 카드로 펴져 있는가.</summary>
    private bool Carded => (_userCardView ?? MobileCards) && _isPhone;

    /// <summary>루트 div 에 <see cref="GridCards.CssClass"/> 를 붙일지.</summary>
    private bool HasCardClass => _userCardView ?? MobileCards;

    private void OnPhoneChanged(bool phone)
    {
        if (_isPhone == phone)
        {
            return;
        }

        _isPhone = phone;
        ApplyScrollMode();
        StateHasChanged();
    }

    /// <summary>모바일에서 카드 형태와 표 형태를 전환한다.</summary>
    private void ToggleCardView(bool cardView)
    {
        _userCardView = cardView;
        ApplyScrollMode();

        try
        {
            StateHasChanged();
        }
        catch (InvalidOperationException)
        {
            // 단위 테스트처럼 RenderHandle 이 아직 붙지 않은 인스턴스에서는 무시한다.
        }
    }

    private static readonly Dictionary<string, Type> DxGridParameterTypes =
        typeof(DxGrid)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute), inherit: true))
            .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.OrdinalIgnoreCase);

    /// <summary>화면이 SelectedItem 을 주었는지. null 과 안 준 것은 파라미터로 구분되지 않으므로 ParameterView 로 본다.</summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // object 로 받는다. TItem 으로 받으면 값 형식일 때 null 을 캐스팅하다 죽는다.
        _selectionBound = parameters.TryGetValue<object>(nameof(SelectedItem), out _);
        _manyBound = parameters.TryGetValue<object>(nameof(SelectedItems), out _);

        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        _forwarded = Coerce(Extra);

        // 자료가 갈리면 관리 판을 닫는다(자동 갱신 화면 머리말).
        _more.ResetIfDataChanged(Data);

        // 선택의 주인이 화면이면 그 값을 따르고, 아니면 표가 든 것을 그대로 둔다 — 안 준 값(null)으로 덮으면 강조가 풀린다.
        if (_selectionBound)
        {
            _selected = SelectedItem;
        }

        // 체크도 같다. 빈 목록은 「전부 풀어라」, 안 준 것은 「표가 든 것을 그대로 둬라」다.
        if (_manyBound)
        {
            _selectedMany = SelectedItems is null ? null : [.. SelectedItems.Cast<object>()];
        }

        // 시작값이라 한 번만 읽는다. 매번 읽으면 사람이 끈 것을 다시 켜게 된다.
        if (!_filterRowSeeded)
        {
            _filterRowSeeded = true;
            _filterRow = Read("ShowFilterRow") is bool flag && flag;
        }

        // 건수는 자료가 바뀔 때만 달라진다. 거른 뒤의 수는 렌더 뒤에 따로 읽는다.
        _rowCount = CountOf(Data);
        _total = Total ?? _rowCount;

        ApplyScrollMode();
    }

    /// <summary>줄을 굴려 보는 길을 정한다 — 카드면 가상 스크롤을 끄고 쪽나누기를 켠다(카드에서는 높이 어림이 열 배 넘게 빗나간다).</summary>
    private void ApplyScrollMode()
    {
        _virtualScroll = VirtualScrolling();
        _pagerVisible = Read("PagerVisible") is bool pv ? pv : PagerVisible;
        _pageSize = Read("PageSize") is int size ? size : DefaultPageSize;

        if (!Carded)
        {
            return;
        }

        _virtualScroll = false;

        // 0 은 「전부 한 쪽에」다. 카드에서는 그 줄이 전부 DOM 에 깔린다.
        if (_pageSize <= 0)
        {
            _pageSize = DefaultPageSize;
        }

        // 쪽이 하나뿐이면 줄을 세우지 않는다. 몇 줄인지 모르면 세운다 — 못 세우면 첫 쪽 뒤 줄에 갈 길이 없다.
        _pagerVisible = _rowCount is not int rows || rows > _pageSize;
    }

    /// <summary>쪽나누기를 끈 화면에서 가상 스크롤을 켠다. 페이저만 감추고 쪽 크기를 두면 첫 쪽 뒤 줄에 갈 길이 없어진다.</summary>
    private bool VirtualScrolling()
    {
        if (Read("VirtualScrollingEnabled") is bool explicitly)
        {
            return explicitly;
        }

        if (Read("PageSize") is int pageSize && pageSize <= 0)
        {
            return true;
        }

        if (Read("PagerVisible") is bool pv)
        {
            return !pv;
        }

        return !PagerVisible;
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

    /// <summary>줄 번호. VisibleIndex 는 쪽 안이 아니라 전체 기준이라 앞 쪽의 건수를 더하지 않는다.</summary>
    private static int RowNumber(int visibleIndex) => visibleIndex + 1;

    /// <summary>자료가 들어오면 첫 줄을 골라 준다. 렌더 뒤에 해야 거르기·정렬이 끝난 뒤의 첫 줄을 고른다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        await SyncScrollAsync();

        // 바로 위의 JS 호출 뒤에 화면이 다시 그려져 표가 걷혔을 수 있다 — 묻지 않고 만지면 회로가 내려간다.
        if (_gone)
        {
            return;
        }

        // 거른 뒤 건수는 렌더 뒤에 읽고, 값이 바뀌었을 때만 다시 그린다(그냥 부르면 끝없이 돈다).
        if (((HeadVisible && ShowHeaderCount) || (ShowFooter && ShowFooterCount)) && _grid is not null)
        {
            var shown = _hasFilter ? _grid.GetVisibleRowCount() : (int?)null;

            if (shown != _filtered)
            {
                _filtered = shown;
                StateHasChanged();
            }
        }

        if (!AutoSelectFirstRow
            || SelectionMode != GridSelectionMode.Single
            || _grid is null
            || ReferenceEquals(_autoPickedFor, Data))
        {
            return;
        }

        // 이미 고른 줄은 그대로 둔다. 이 자료 묶음은 처리한 것으로 표시해 사람이 선택을 푼 뒤 되살아나지 않게 한다.
        if (_selected is not null)
        {
            _autoPickedFor = Data;
            return;
        }

        // 줄이 아직 안 그려졌으면 다음 렌더에 다시 온다 — 여기서 표시하면 늦게 오는 조회 결과가 자동 선택을 못 받는다.
        if (_grid.GetVisibleRowCount() <= 0)
        {
            return;
        }

        if (_grid.GetDataItem(0) is not { } first)
        {
            return;
        }

        _autoPickedFor = Data;
        await OnSelectedDataItemChangedAsync(first);

        // 고르기를 알리는 사이에도 걷힐 수 있다 — 화면이 그 알림을 받고 다른 모양으로 갈아 끼우는 곳이 여기다.
        if (_gone)
        {
            return;
        }

        StateHasChanged();
    }

    /// <summary>표의 편집 창에 붙는 클래스. DevExpress 가 그리는 창이라 jsini-gridpopup 으로 본문만 구르게 하고 머리 잡는 모양을 CommPopup 과 맞춘다.</summary>
    private string PopupClass =>
        Read("PopupEditFormCssClass") is string extra && extra.Length > 0
            ? $"jsini-gridpopup {extra}"
            : "jsini-gridpopup";

    /// <summary>이 쪽의 첫 줄이 전체에서 몇 번째인가. 쪽이 없으면 0.</summary>
    private int PageOffset => _grid is null ? 0 : _grid.PageIndex * Math.Max(_grid.PageSize, 0);

    private readonly Dictionary<string, GridColumnSortOrder> _sortSeen = new(StringComparer.Ordinal);

    /// <summary>칸 머리 클릭을 오름 → 내림 → 없음으로 돌린다. 정렬은 표 안에서 끝나 부모가 다시 그려지지 않으므로 OnAfterRender 가 아니라 LayoutAutoSaving 에 건다.</summary>
    private Task OnLayoutChanged(GridPersistentLayoutEventArgs e)
    {
        // 화면이 이름을 적어 두었으면 바뀐 모습을 그대로 맡긴다. 이 알림은 정렬·너비·검색·쪽 넘기기마다 오므로 떠나는 순간을 따로 잡지 않아도 최신이다.
        if (LayoutSlot is { } slot && e.Layout is { } layout)
        {
            Screen.Set(slot, layout);
        }

        if (_grid is null)
        {
            return Task.CompletedTask;
        }

        string? release = null;
        var now = new Dictionary<string, GridColumnSortOrder>(StringComparer.Ordinal);

        foreach (var column in _grid.GetSortedColumns())
        {
            if (column.FieldName is not { Length: > 0 } field)
            {
                continue;
            }

            now[field] = column.SortOrder;

            if (_sortSeen.TryGetValue(field, out var before)
                && before == GridColumnSortOrder.Descending
                && column.SortOrder == GridColumnSortOrder.Ascending)
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

        // 기억에서도 지운다 — 안 지우면 다음 첫 클릭이 또 세 번째로 읽힌다.
        _sortSeen.Remove(release);

        // 알림을 받는 도중에 layout 을 또 건드리지 않게 한 박자 뒤로 미룬다.
        var target = release;
        _ = InvokeAsync(() => _grid.SortBy(target, GridColumnSortOrder.None));

        return Task.CompletedTask;
    }

    /// <summary>구른 자리를 맡기고 되돌린다(StateKey 가 있을 때만). 가상 스크롤은 높이를 늦게 잡으므로 될 때까지 다시 부른다. 회로가 닫혔으면 조용히 넘어간다.</summary>
    private async Task SyncScrollAsync()
    {
        if (_scrollSynced || ScrollSlot is not { } slot)
        {
            return;
        }

        try
        {
            _scrollJs ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/grid-scroll.js");

            _scrollSynced = await _scrollJs.InvokeAsync<bool>("sync", _root, slot);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
            // 더 두드리지 않는다 — 최악이라야 표가 맨 위에서 시작할 뿐이다.
            _scrollSynced = true;
        }
    }

    /// <summary>표의 모습을 맡겨 둘 열쇠. StateKey 가 비면 없다.</summary>
    private string? LayoutSlot =>
        string.IsNullOrWhiteSpace(StateKey) ? null : $"grd:{StateKey}";

    /// <summary>구른 자리를 맡겨 둘 열쇠. layout 과 따로 둔다 — 재는 법이 다르다.</summary>
    private string? ScrollSlot =>
        string.IsNullOrWhiteSpace(StateKey) ? null : $"grd-scroll:{StateKey}";

    /// <summary>표가 제 모습을 읽어 갈 차례. 렌더 뒤에 LoadLayout 을 부르면 처음 모습으로 한 번 그려졌다가 바뀌어 칸이 뛴다.</summary>
    private Task OnLayoutLoading(GridPersistentLayoutEventArgs e)
    {
        if (LayoutSlot is { } slot && Screen.Get<GridPersistentLayout>(slot) is { } saved)
        {
            e.Layout = saved;
        }

        return Task.CompletedTask;
    }

    private void ToggleFilterRow()
    {
        _filterRow = !_filterRow;

        // 줄을 감출 때 조건도 지운다 — 안 지우면 보이지 않는 조건에 걸러진 표가 남는다.
        if (!_filterRow && _hasFilter)
        {
            ClearFilter();
        }
    }

    private void ClearFilter()
    {
        _grid?.ClearFilter();
        _hasFilter = false;
    }

    private DxContextMenu? _menu;

    /// <summary>창을 여는 순간 세운 항목. 연 뒤에 권한·상태가 바뀌어도 창은 그대로다.</summary>
    private IReadOnlyList<GridMenuItem> _menuItems = [];

    private sealed record GridMenuItem(
        string Text, string? Icon, Func<Task> Run, bool BeginGroup = false, bool Enabled = true);

    /// <summary>화면이 ContextMenuItems 로 보탠 항목. 적힌 차례대로다.</summary>
    private readonly List<CommMenuItem> _screenItems = [];

    void ICommMenuHost.Add(CommMenuItem item)
    {
        _screenItems.Add(item);
        StateHasChanged();
    }

    void ICommMenuHost.Remove(CommMenuItem item)
    {
        _screenItems.Remove(item);
        StateHasChanged();
    }

    /// <summary>오른쪽 클릭 창에 올릴 것. 아래 띠 아이콘과 같은 조건이다(등록·엑셀은 권한, 칸별 검색 지우기는 걸린 조건이 있을 때).</summary>
    private List<GridMenuItem> BuildMenu()
    {
        var items = new List<GridMenuItem>();

        if (!ShowFooter)
        {
            return items;
        }

        if (OnSave.HasDelegate && Can(MenuAction.Create))
        {
            items.Add(new(CreateTitle, "jsini-icon-plus", CreateAsync));
        }

        foreach (var mine in _screenItems)
        {
            items.Add(new(mine.Text, mine.IconCssClass, () => mine.Click.InvokeAsync(),
                BeginGroup: mine.BeginGroup, Enabled: mine.Enabled));
        }

        if (Reload.HasDelegate)
        {
            items.Add(new("다시 읽기", "jsini-icon-refresh", () => Reload.InvokeAsync(),
                BeginGroup: _screenItems.Count > 0));
        }

        // 카드로 펴면 머리줄과 칸별 검색이 안에 있어 눌러도 아무 일이 없는 항목이 생기므로 그 항목을 뺀다.
        if (ShowFilterToggle && !Carded)
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

        // 모바일에서 카드 형태와 표 형태를 전환한다.
        if (_isPhone)
        {
            if (Carded)
            {
                items.Add(new("표형태로보기", null, () =>
                {
                    ToggleCardView(false);
                    return Task.CompletedTask;
                }, BeginGroup: items.Count > 0));
            }
            else
            {
                items.Add(new("카드형태로 변환", null, () =>
                {
                    ToggleCardView(true);
                    return Task.CompletedTask;
                }, BeginGroup: items.Count > 0));
            }
        }

        return items;
    }

    /// <summary>브라우저 창을 막을지. 렌더할 때 정해진다 — 올릴 것이 없는 표에서 막으면 오른쪽 클릭이 아무 일도 안 한다.</summary>
    private bool HasMenu => BuildMenu().Count > 0;

    private async Task OpenMenuAsync(MouseEventArgs args)
    {
        _menuItems = BuildMenu();

        if (_menu is null || _menuItems.Count == 0)
        {
            return;
        }

        // 항목을 먼저 그려 놓고 연다 — 같은 렌더에서 열면 창이 옛 항목으로 뜬다.
        StateHasChanged();
        await _menu.ShowAsync(args);
    }

    private void OnFilterCriteriaChanged(GridFilterCriteriaChangedEventArgs e)
    {
        // CriteriaOperator 는 == 를 재정의해서 null 비교가 예상과 다르다.
        _hasFilter = !ReferenceEquals(_grid?.GetFilterCriteria(), null);
    }

    /// <summary>짝수 줄에 표시 클래스를 붙인다. 색은 CSS 가 정한다. 쪽 안의 자리로 세야 쪽을 넘겨도 무늬가 뒤집히지 않는다.</summary>
    private void OnCustomizeElement(GridCustomizeElementEventArgs e)
    {
        if (e.ElementType == GridElementType.DataRow)
        {
            var alt = AlternateRows && (e.VisibleIndex - PageOffset) % 2 == 1 ? "commgrd__row--alt" : null;
            var own = RowCssClass is not null && e.Grid.GetDataItem(e.VisibleIndex) is TItem item ? RowCssClass(item) : null;
            var css = string.Join(' ', new[] { alt, own }.Where(c => !string.IsNullOrWhiteSpace(c)));

            if (css.Length > 0)
            {
                e.CssClass = css;
            }

            return;
        }

        if (e.ElementType != GridElementType.DataCell)
        {
            return;
        }

        // 칸이 TextAlignment 를 직접 정했으면 건드리지 않는다. CSS 로는 이 구분이 안 되므로(같은 클래스가 붙는다) 칸이 선언한 값을 여기서 읽는다.
        if (e.Column?.TextAlignment == GridTextAlignment.Auto)
        {
            e.CssClass = "commgrd__cell--center";
        }

        if (MobileCards)
        {
            LabelForCard(e);
        }
    }

    /// <summary>카드로 펼 때 셀에 칸 이름(GridCards.Label)과 칸 클래스를 단다. 순번·관리 칸에는 이름을 적지 않는다 — 첫 값 칸이 카드 제목이 되므로 이름이 붙으면 제목이 밀린다.</summary>
    private static void LabelForCard(GridCustomizeElementEventArgs e)
    {
        var marker = e.Column?.Name switch
        {
            SeqColumnName => "commgrd__cell--seq",
            ActionsColumnName => "commgrd__cell--act",
            _ => null,
        };

        if (marker is null)
        {
            GridCards.Label(e);
            return;
        }

        // 가운데 정렬 표시가 이미 붙어 있을 수 있다 — 덮어쓰지 않고 잇는다.
        e.CssClass = string.IsNullOrEmpty(e.CssClass) ? marker : $"{e.CssClass} {marker}";
    }

    /// <summary>순번 칸을 가리키는 이름. IGridColumn.Name 으로 찾는다.</summary>
    private const string SeqColumnName = "commgrd-seq";

    /// <summary>관리 칸(수정·삭제)을 가리키는 이름.</summary>
    private const string ActionsColumnName = "commgrd-act";

    /// <summary>splat 으로 들어온 문자열을 DxGrid 가 기다리는 형으로 바꾼다.</summary>
    private static IReadOnlyDictionary<string, object>? Coerce(IReadOnlyDictionary<string, object>? source)
    {
        if (source is null || source.Count == 0)
        {
            return source;
        }

        // 바꿀 것이 없으면 원본을 그대로 쓴다 — 새 사전을 만들면 표가 다시 그려진다.
        Dictionary<string, object>? changed = null;

        foreach (var (name, value) in source)
        {
            if (!DxGridParameterTypes.TryGetValue(name, out var declared))
            {
                continue;
            }

            // 대리자는 감싸면 안 된다 — 감싸면 알림 받는이가 표만 다시 그려진다. 여기서 막아야 DevExpress 대입 때 죽는 원인을 알 수 있다.
            if (value is Delegate && IsEventCallback(declared))
            {
                throw new InvalidOperationException(
                    $"CommGrd: DxGrid 의 '{name}' 은 EventCallback 이라 splat 으로 넘길 수 없습니다. "
                    + "CommGrd 가 선언한 파라미터를 쓰십시오 — 선택은 SelectedItem · SelectedItemChanged 입니다. "
                    + $"그런 파라미터가 없으면 CommGrd 에 먼저 만드십시오({name} 을(를) 그대로 내려보내면 됩니다).");
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
                    $"CommGrd: DxGrid 의 '{name}' 에 준 값 \"{text}\" 을(를) {want.Name} 으로 읽지 못했습니다. "
                    + $"식으로 주려면 {name}=\"@(…)\" 로 적으십시오.", ex);
            }

            changed ??= new Dictionary<string, object>(source, StringComparer.Ordinal);
            changed[name] = converted;
        }

        return changed ?? source;
    }

    /// <summary>DxGrid 의 선택 알림을 화면이 아는 형으로 옮긴다. 받는이를 화면으로 두어야 고른 줄에 딸린 칸까지 다시 그려진다.</summary>
    private Task OnSelectedDataItemChangedAsync(object? item)
    {
        _selected = item;

        return SelectedItemChanged.InvokeAsync(item is TItem typed ? typed : default);
    }

    /// <summary>누른 줄을 화면에 넘긴다.</summary>
    private Task OnRowClickAsync(GridRowClickEventArgs e)
    {
        if (!OnRowClick.HasDelegate)
        {
            return Task.CompletedTask;
        }

        return e.Grid.GetDataItem(e.VisibleIndex) is TItem item
            ? OnRowClick.InvokeAsync(item)
            : Task.CompletedTask;
    }

    /// <summary>두 번 누른 줄을 화면에 넘긴다. DevExpress 는 보이는 차례만 주므로 표에 물어 줄을 얻는다(GetDataItem). 머리줄을 눌러도 오지만 자료가 없어 아무 일도 안 한다.</summary>
    private Task OnRowDoubleClickAsync(GridRowClickEventArgs e)
    {
        if (!OnRowDoubleClick.HasDelegate)
        {
            return Task.CompletedTask;
        }

        return e.Grid.GetDataItem(e.VisibleIndex) is TItem item
            ? OnRowDoubleClick.InvokeAsync(item)
            : Task.CompletedTask;
    }

    /// <summary>체크가 바뀌었다. 화면이 든 형(TItem)으로 되돌려 올린다 — 표는 object 로 다룬다.</summary>
    private Task OnSelectedDataItemsChangedAsync(IReadOnlyList<object> items)
    {
        _selectedMany = items;

        return SelectedItemsChanged.InvokeAsync([.. items.OfType<TItem>()]);
    }

    private static bool IsEventCallback(Type type) =>
        type == typeof(EventCallback)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));

    /// <summary>등록 팝업을 연다. 바깥에서도 부를 수 있다.</summary>
    public Task StartNewAsync() => _grid?.StartEditNewRowAsync() ?? Task.CompletedTask;

    /// <summary>고치는 자리가 팝업 편집 창인가. 폼을 주지 않으면 편집 자리가 표 밖이라 그 창은 빈 창이 된다 — EditFormTemplate 하나가 이미 말한다.</summary>
    private bool EditsInPopup => OnSave.HasDelegate && EditFormTemplate is not null;

    /// <summary>아래 띠의 ＋. 팝업 화면은 편집 창을 열고, 표 밖에서 고치는 화면은 OnNew 로 채운 새 줄을 OnEditOpen 으로 넘긴다.</summary>
    private Task CreateAsync()
    {
        if (EditsInPopup)
        {
            return StartNewAsync();
        }

        var fresh = Activator.CreateInstance<TItem>();

        OnNew?.Invoke(fresh);
        OnEditOpen?.Invoke(fresh, true);

        return Task.CompletedTask;
    }

    /// <summary>표 밖에서 고친 것을 저장한다. 팝업의 저장과 같은 길(OnSave → 문구 → 다시 읽기).</summary>
    /// <returns>저장했으면 true. 뒤처리는 부르는 화면이 정한다.</returns>
    public async Task<bool> SaveAsync(TItem item, bool isNew)
    {
        var saved = await RunAsync(
            () => OnSave.InvokeAsync((item, isNew)),
            isNew ? "등록했습니다." : "저장했습니다.",
            isNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (saved)
        {
            await Reload.InvokeAsync();
        }

        return saved;
    }

    /// <summary>「⋮」 판의 상태. 나무와 같은 부품을 쓴다(<see cref="CommMoreMenu"/>).</summary>
    private readonly CommMoreMenu _more;

    public CommGrd() => _more = new CommMoreMenu(() => StateHasChanged());

    private void OnMenuChanged() => StateHasChanged();

    private Task EditAsync(int visibleIndex) => _grid?.StartEditRowAsync(visibleIndex) ?? Task.CompletedTask;

    /// <summary>삭제 확인. DeleteConfirm 을 준 화면만 우리 확인창을 쓴다 — 맡기면 기본 확인창이 한 번 더 뜬다.</summary>
    private async Task ConfirmDeleteAsync(int visibleIndex, object? dataItem)
    {
        if (DeleteConfirm is null || dataItem is not TItem item)
        {
            _grid?.ShowRowDeleteConfirmation(visibleIndex);
            return;
        }

        if (await _confirm!.AskAsync(DeleteConfirm(item)))
        {
            await DeleteAsync(item);
        }
    }

    /// <summary>지금 보이는 그대로(정렬·필터가 걸린 상태) 내린다.</summary>
    private Task ExportAsync() =>
        _grid?.ExportToXlsxAsync($"{ExportName}.xlsx", new GridXlExportOptions
        {
            ExportSelectedRowsOnly = false,
        }) ?? Task.CompletedTask;

    private void OnCustomizeEditModel(GridCustomizeEditModelEventArgs e)
    {
        if (e.EditModel is not TItem item)
        {
            return;
        }

        if (e.IsNew)
        {
            OnNew?.Invoke(item);
        }

        OnEditOpen?.Invoke(item, e.IsNew);
    }

    private async Task OnEditModelSavingAsync(GridEditModelSavingEventArgs e)
    {
        if (e.EditModel is not TItem model)
        {
            return;
        }

        if (!await SaveAsync(model, e.IsNew))
        {
            // 실패하면 팝업을 닫지 않는다. 닫으면 쓴 내용이 사라진다.
            e.Cancel = true;
        }
    }

    private async Task OnDataItemDeletingAsync(GridDataItemDeletingEventArgs e)
    {
        if (e.DataItem is not TItem item)
        {
            return;
        }

        // 실패하면 표에서 줄을 걷어내지 않는다 — 서버에는 남아 있는데 화면에서만 사라지면 다시 읽을 때까지 틀린다.
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
}
