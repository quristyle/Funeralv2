namespace JSini.Web.Abstractions;

/// <summary>
/// 사이드바에 그릴 메뉴 한 칸. <c>/auth/menu/all</c> 응답
/// (<c>scom.system_menus</c>)을 그대로 옮긴 것이다.
///
/// Vue 의 <c>MenuRecordRaw</c> 와 달리 라우트 정보(<c>component</c>)를 담지 않는다.
/// 라우트는 모듈의 <c>@page</c> 가 소유하고, 여기 있는 <see cref="Path"/> 는
/// 그 라우트를 <b>가리키는 열쇠</b>일 뿐이다. 권한 판정도 이 경로로 한다.
/// </summary>
public sealed record MenuNode
{
    /// <summary>
    /// 라우트 경로 (<c>/funeral/status</c>). 모듈의 <c>@page</c> 와 같아야 한다.
    /// 권한표(<c>canView</c>)와 즐겨찾기가 모두 이 값을 열쇠로 쓴다.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 사이드바가 실제로 거는 링크 주소.
    ///
    /// **<see cref="Path"/> 와 갈라 둔 이유가 있다.** 둘은 이행이 끝날 때까지
    /// 다를 수 있다 — DB 의 <c>path</c> 는 Vue 시절 경로(<c>/room_status</c>)
    /// 인데 Blazor 라우트는 업무 접두사가 붙은 <c>/funeral/room-status</c> 다.
    ///
    /// 그 차이를 <see cref="Path"/> 쪽에서 흡수하면 안 된다. 권한표와
    /// 즐겨찾기가 그 값을 열쇠로 쓰기 때문에, 바꾸는 순간 <b>권한이 없는데
    /// 메뉴가 보이는</b> 쪽으로 틀린다. 그래서 링크만 옮긴다.
    ///
    /// 비워 두면 <see cref="Path"/> 를 쓴다.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>사이드바가 걸 주소. <see cref="Href"/> 가 있으면 그것, 없으면 <see cref="Path"/>.</summary>
    public string LinkTarget => string.IsNullOrEmpty(Href) ? Path : Href;

    /// <summary>사이드바에 보이는 이름.</summary>
    public required string Title { get; init; }

    /// <summary>아이콘 이름. 없으면 기본 아이콘.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 자기 화면이 없는 묶음인가 (<c>type = 'CATALOG'</c>).
    ///
    /// 자식이 있다고 다 묶음이 아니다 — <c>/funeral/status</c>(현황관리)는 자식이
    /// 다섯인 <b>화면 있는 메뉴</b>다. 이걸 묶음으로 다루면 자식이 모두 걸러졌을 때
    /// 자기 열람 권한이 있는데도 함께 사라지고, 그 위 묶음까지 빈 묶음이 되어
    /// 사이드바가 통째로 비어 버린다. Vue 에서 겪은 문제라 컬럼을 그대로 들고 온다.
    /// </summary>
    public bool IsCatalog { get; init; }

    /// <summary>휴대폰에서 이 메뉴를 목록에 넣는가 (<c>use_mobile</c>).</summary>
    public bool UseMobile { get; init; } = true;

    /// <summary>태블릿에서 이 메뉴를 목록에 넣는가 (<c>use_tablet</c>).</summary>
    public bool UseTablet { get; init; } = true;

    /// <summary>메뉴에서 숨긴다 (<c>hide_in_menu</c>). 라우트는 살아 있다.</summary>
    public bool HideInMenu { get; init; }

    /// <summary>외부 링크. 있으면 앱 안의 화면이 아니라 새 창으로 연다.</summary>
    public string? Link { get; init; }

    /// <summary>같은 부모 안에서의 정렬 순서 (<c>order_no</c>).</summary>
    public int OrderNo { get; init; }

    /// <summary>하위 메뉴.</summary>
    public IReadOnlyList<MenuNode> Children { get; init; } = [];

    /// <summary>외부 링크 메뉴인가. 앱 라우트가 아니므로 권한표에도 없다.</summary>
    public bool IsExternalLink =>
        !string.IsNullOrEmpty(Link) || Path.StartsWith("http", StringComparison.OrdinalIgnoreCase);
}
