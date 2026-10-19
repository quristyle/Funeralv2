namespace JSini.Web.Abstractions;

/// <summary>사이드바에 그릴 메뉴 한 칸(<c>/auth/menu/all</c> 의 scom.system_menus). 라우트 정보는 모듈의 @page 가 가졌고, Path 는 그 라우트를 가리키는 열쇠이자 권한 판정의 열쇠다.</summary>
public sealed record MenuNode
{
    /// <summary>라우트 경로. 모듈의 @page 와 같아야 한다. 권한표와 즐겨찾기가 이 값을 열쇠로 쓴다.</summary>
    public required string Path { get; init; }

    /// <summary>화면 열쇠(<c>funeral.room-status</c>). 링크 주소는 이 값에서 나온다. 비어 있으면 RouteAliases 가 Path 를 옮기는 옛 길로 떨어진다.</summary>
    public string? RouteKey { get; init; }

    /// <summary>사이드바가 실제로 거는 링크 주소. Path 와 다를 수 있다. 권한표와 즐겨찾기가 Path 를 열쇠로 쓰므로 Path 를 바꾸면 권한이 틀어진다. 링크만 옮긴다. 비우면 Path.</summary>
    public string? Href { get; init; }

    /// <summary>사이드바가 걸 주소. <see cref="Href"/> 가 있으면 그것, 없으면 <see cref="Path"/>.</summary>
    public string LinkTarget => string.IsNullOrEmpty(Href) ? Path : Href;

    /// <summary>트리가 링크로 거는 주소. 묶음은 null 이다(주소를 주면 눌렀을 때 준비 중 화면으로 간다). DxTreeViewDataMapping.NavigateUrl 이 이 이름을 리플렉션으로 찾으므로 이름을 바꾸면 링크가 죽는다.</summary>
    public string? NavigateUrl => IsCatalog ? null : LinkTarget;

    /// <summary>사이드바에 보이는 이름.</summary>
    public required string Title { get; init; }

    /// <summary>아이콘 이름. 없으면 기본 아이콘.</summary>
    public string? Icon { get; init; }

    /// <summary>자기 화면이 없는 묶음(<c>type = 'CATALOG'</c>). 자식이 있다고 다 묶음이 아니다. 화면이 있는 메뉴를 묶음으로 다루면 자식이 걸러질 때 자기 권한이 있어도 같이 사라진다.</summary>
    public bool IsCatalog { get; init; }

    /// <summary>바깥 화면을 끼워 넣는 메뉴(<c>type = 'EMBEDDED'</c>). 가리킬 화면은 공용 화면이 받고 무엇을 띄울지는 IframeSrc 가 정한다. RouteKey 는 비어 있어도 정상이다.</summary>
    public bool IsEmbedded { get; init; }

    /// <summary>휴대폰에서 이 메뉴를 목록에 넣는가 (<c>use_mobile</c>).</summary>
    public bool UseMobile { get; init; } = true;

    /// <summary>태블릿에서 이 메뉴를 목록에 넣는가 (<c>use_tablet</c>).</summary>
    public bool UseTablet { get; init; } = true;

    /// <summary>메뉴에서 숨긴다 (<c>hide_in_menu</c>). 라우트는 살아 있다.</summary>
    public bool HideInMenu { get; init; }

    /// <summary>외부 링크. 있으면 앱 안의 화면이 아니라 새 창으로 연다.</summary>
    public string? Link { get; init; }

    /// <summary>iframe 으로 띄울 바깥 주소(EMBEDDED). Link 는 새 창으로 나가는 주소라 다르다. 화면이 주소를 박아 두지 않으려고 DB 값을 그대로 나른다.</summary>
    public string? IframeSrc { get; init; }

    /// <summary>같은 부모 안에서의 정렬 순서 (<c>order_no</c>).</summary>
    public int OrderNo { get; init; }

    /// <summary>하위 메뉴.</summary>
    public IReadOnlyList<MenuNode> Children { get; init; } = [];

    /// <summary>외부 링크 메뉴인가. 앱 라우트가 아니므로 권한표에도 없다.</summary>
    public bool IsExternalLink =>
        !string.IsNullOrEmpty(Link) || Path.StartsWith("http", StringComparison.OrdinalIgnoreCase);
}
