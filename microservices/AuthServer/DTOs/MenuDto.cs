namespace AuthServer.DTOs;

/// <summary>프론트엔드 라우터 연동을 위한 메뉴 정보 DTO (Vben Admin RouteRecord 대 응)</summary>
public class MenuDto
{
    /// <summary>메뉴 및 라우트의 고유 명칭</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>브라우저 접속 경로. 권한표·즐겨찾기의 열쇠이기도 하지만 링크 주소는 RouteKey 에서 나온다.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 이 메뉴가 가리키는 화면의 열쇠 (<c>funeral.room-status</c>).
    /// <b>프론트가 링크 주소를 푸는 값이다.</b> 안 채운 메뉴는 <c>null</c>.
    /// </summary>
    public string? RouteKey { get; set; }

    /// <summary>
    /// 프론트엔드 컴포넌트 경로 (예: layouts/default/index).
    /// Vue 시절의 라우트 생성원이고 <b>지금은 아무도 읽지 않는다</b>.
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>메뉴의 부가 메타 데이터 (아이콘, 제목 등)</summary>
    public MenuMetaDto Meta { get; set; } = new();

    /// <summary>하위 메뉴 목록</summary>
    public List<MenuDto>? Children { get; set; }
}

/// <summary>메뉴의 부가 설정을 담는 메타 데이터 클래스</summary>
public class MenuMetaDto
{
    /// <summary>
    /// 저장된 메뉴 제목. 번역 키(<c>system.menu.title</c>)와 이미 완성된
    /// 글자(<c>공통코드</c>)가 섞여 있다.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>화면에 찍을 제목. 서버가 i18n 에서 찾았을 때만 담는다.</summary>
    /// 못 찾으면 null 이라 화면은 Title 을 찍는다.
    public string? TitleText { get; set; }

    /// <summary>메뉴 아이콘 명칭</summary>
    public string? Icon { get; set; }

    /// <summary>메뉴바에서 숨김 여부</summary>
    public bool? HideInMenu { get; set; }

    /// <summary>하위 메뉴를 메뉴목록에서 감출지 여부. 아래 셋은 vben 라우트 메타가 알아듣는 이름이다.</summary>
    public bool? HideChildrenInMenu { get; set; }

    /// <summary>브레드크럼에서 감출지 여부</summary>
    public bool? HideInBreadcrumb { get; set; }

    /// <summary>탭 바에서 감출지 여부</summary>
    public bool? HideInTab { get; set; }

    /// <summary>페이지 유지(Keep-Alive) 여부</summary>
    public bool? KeepAlive { get; set; }

    /// <summary>탭 고정 여부</summary>
    public bool? AffixTab { get; set; }

    /// <summary>DOM 캐싱 여부</summary>
    public bool? DomCached { get; set; }

    /// <summary>프론트엔드 컴포넌트 경로 (meta 에도 담아 주자)</summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>권한 목록</summary>
    public List<string>? Authority { get; set; }

    /// <summary>권한 없을 때 메뉴 표시 여부</summary>
    public bool? MenuVisibleWithForbidden { get; set; }

    /// <summary>외부 링크 URL</summary>
    public string? Link { get; set; }

    /// <summary>Iframe 소스 URL</summary>
    public string? IframeSrc { get; set; }

    /// <summary>뱃지 유형</summary>
    public string? BadgeType { get; set; }

    /// <summary>뱃지 내용</summary>
    public string? Badge { get; set; }

    /// <summary>특정 하위 경로 접속 시 활성화될 상위 메뉴</summary>
    public string? ActiveMenu { get; set; }

    /// <summary>메뉴 정렬 순서</summary>
    public int? Order { get; set; }

    /// <summary>메뉴 유형(CATALOG · MENU · EMBEDDED · LINK · BUTTON). 묶음은 자식이 남을 때만 목록에 남는다.</summary>
    public string Type { get; set; } = "MENU";

    /// <summary>휴대폰 크기(&lt;768px) 메뉴목록 노출 여부. 라우트는 걸러 내지 않는다.</summary>
    public bool UseMobile { get; set; } = true;

    /// <summary>태블릿 크기(768~1023px)에서 메뉴목록에 보일지 여부. <see cref="UseMobile"/> 과 같은 규칙이다.</summary>
    public bool UseTablet { get; set; } = true;
}


/// <summary>
/// 메뉴의 위치(부모)와 순서를 한 건 나타내는 DTO.
/// 트리에서 드래그한 결과를 일괄 저장할 때 쓴다.
/// </summary>
public class MenuOrderDto
{
    /// <summary>메뉴 아이디</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>새 부모 메뉴 아이디 (최상위는 null)</summary>
    public string? Pid { get; set; }

    /// <summary>형제 안에서의 순번 (0부터)</summary>
    public int OrderNo { get; set; }
}


/// <summary>로그인 사용자가 한 메뉴에 대해 가진 권한. 역할들의 권한을 OR 로 합친 값이다.</summary>
public class MenuPermissionDto
{
    public string MenuId { get; set; } = string.Empty;

    /// <summary>메뉴의 라우트 경로. 화면은 자기 경로로 자기 권한을 찾는다.</summary>
    public string Path { get; set; } = string.Empty;

    public bool CanView { get; set; }
    public bool CanSearch { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanPrint { get; set; }
    public bool CanExcel { get; set; }

    public bool CanCust1 { get; set; }
    public bool CanCust2 { get; set; }
    public bool CanCust3 { get; set; }
    public bool CanCust4 { get; set; }
    public bool CanCust5 { get; set; }
    public bool CanCust6 { get; set; }
    public bool CanCust7 { get; set; }
    public bool CanCust8 { get; set; }
}
