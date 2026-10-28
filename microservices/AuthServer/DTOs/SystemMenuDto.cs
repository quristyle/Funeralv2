namespace AuthServer.DTOs;

/// <summary>시스템 메뉴 관리(CRUD)를 위한 데이터 구조 DTO</summary>
public class SystemMenuDto
{
    /// <summary>메뉴 아이디 (GUID)</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>메뉴 명칭</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>브라우저 접속 경로. 역할-메뉴 권한표와 즐겨찾기의 열쇠이므로 함부로 바꾸지 않는다. 링크 주소는 <see cref="RouteKey"/> 가 정한다.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>이 메뉴가 가리키는 화면의 열쇠 (<c>funeral.room-status</c>). 메뉴 관리 화면이 <b>실려 있는 화면 목록에서 골라</b> 채운다.</summary>
    public string? RouteKey { get; set; }

    /// <summary>프론트엔드 컴포넌트 경로. Vue 시절 값이고 지금은 읽지 않는다.</summary>
    public string? Component { get; set; }

    /// <summary>상위 메뉴 아이디 (Pid)</summary>
    public string? Pid { get; set; }

    /// <summary>리다이렉트 경로</summary>
    public string? Redirect { get; set; }

    /// <summary>메뉴 유형 (menu, catalog 등)</summary>
    public string Type { get; set; } = "menu";

    /// <summary>권한 식별 코드</summary>
    public string? AuthCode { get; set; }

    /// <summary>메뉴 사용 상태 (0: 비활성, 1: 활성). 비활성 메뉴는 사이드바 조회가 내려주지 않으므로 라우트도 안 만들어진다. 메뉴 관리 화면이 비활성 메뉴까지 보여 주려면 이 값이 반드시 함께 나가야 한다.</summary>
    public int Status { get; set; } = 1;

    /// <summary>메뉴 메타 데이터 (아이콘, 정렬 등)</summary>
    public SystemMenuMetaDto Meta { get; set; } = new();

    /// <summary>하위 메뉴 트리</summary>
    public List<SystemMenuDto>? Children { get; set; }

    /// <summary>이 메뉴가 사용하는 권한 항목 설정</summary>
    public MenuPermissionItemsDto Permissions { get; set; } = new();
}

/// <summary>시스템 메뉴의 부가 정보를 담는 메타 데이터 DTO</summary>
public class SystemMenuMetaDto
{
    /// <summary>화면 표시 제목</summary>
    public string? Title { get; set; }

    /// <summary>화면에 그대로 찍을 제목. 서버가 옮겨 둔 글자다 — 찾았을 때만 담고, 못 찾으면 null 로 두어 화면이 <c>$tIfKey</c> 로 스스로 처리하게 한다(언어 파일에만 있는 키가 아직 있다).</summary>
    public string? TitleText { get; set; }

    /// <summary>아이콘 명칭</summary>
    public string? Icon { get; set; }

    /// <summary>정렬 순서</summary>
    public int Order { get; set; }

    /// <summary>메뉴 숨김 여부</summary>
    public bool HideInMenu { get; set; }

    /// <summary>하위 메뉴를 메뉴목록에서만 감춘다. 라우트는 살려 둔다.</summary>
    public bool HideChildrenInMenu { get; set; }

    /// <summary>브레드크럼에서 감출지 여부</summary>
    public bool HideInBreadcrumb { get; set; }

    /// <summary>탭 바에서 감출지 여부</summary>
    public bool HideInTab { get; set; }

    /// <summary>페이지 캐싱(Keep-Alive) 여부</summary>
    public bool KeepAlive { get; set; }

    /// <summary>탭 고정 여부</summary>
    public bool AffixTab { get; set; }

    /// <summary>DOM 캐싱 여부</summary>
    public bool DomCached { get; set; }

    /// <summary>권한 목록</summary>
    public List<string>? Authority { get; set; }

    /// <summary>권한 없을 때 메뉴 표시 여부</summary>
    public bool MenuVisibleWithForbidden { get; set; }

    /// <summary>외부 링크 URL</summary>
    public string? Link { get; set; }

    /// <summary>Iframe 소스 URL</summary>
    public string? IframeSrc { get; set; }

    /// <summary>뱃지 유형</summary>
    public string? BadgeType { get; set; }

    /// <summary>뱃지 내용</summary>
    public string? Badge { get; set; }

    /// <summary>휴대폰 크기(&lt;768px)에서 메뉴목록에 보일지. 끄면 그 크기의 메뉴목록에서만 빠지고 라우트는 그대로라 주소·즐겨찾기로는 열린다.</summary>
    public bool UseMobile { get; set; } = true;

    /// <summary>태블릿 크기(768~1023px)에서 메뉴목록에 보일지 여부. <see cref="UseMobile"/> 과 같은 규칙이다.</summary>
    public bool UseTablet { get; set; } = true;
}

/// <summary>시스템 메뉴 생성을 위한 데이터 구조 DTO</summary>
public class CreateSystemMenuDto
{
    /// <summary>생성할 메뉴 명칭</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>접속 경로. 권한표의 열쇠이기도 하다.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>가리킬 화면의 열쇠 (<c>funeral.room-status</c>). 메뉴 관리 화면이 목록에서 고른 값을 싣는다. 묶음(CATALOG)은 비운다.</summary>
    public string? RouteKey { get; set; }

    /// <summary>컴포넌트 경로. Vue 시절 값이고 지금은 읽지 않는다.</summary>
    public string? Component { get; set; }

    /// <summary>상위 메뉴 아이디</summary>
    public string? Pid { get; set; }

    /// <summary>리다이렉트 경로</summary>
    public string? Redirect { get; set; }

    /// <summary>메뉴 유형</summary>
    public string Type { get; set; } = "menu";

    /// <summary>권한 코드</summary>
    public string? AuthCode { get; set; }

    /// <summary>메뉴 사용 상태 (0: 비활성, 1: 활성). 일부러 nullable 이다 — 값을 싣지 않은 요청은 상태를 건드리지 않아, 이 필드를 모르는 호출자가 저장하며 비활성 메뉴를 되살리는 일을 막는다.</summary>
    public int? Status { get; set; }

    /// <summary>메타 데이터 설정</summary>
    public SystemMenuMetaDto Meta { get; set; } = new();

    /// <summary>이 메뉴가 사용하는 권한 항목 설정</summary>
    public MenuPermissionItemsDto Permissions { get; set; } = new();
}

/// <summary>메뉴가 어떤 권한 항목을 쓰는지 정하는 설정 DTO. 메뉴마다 의미 있는 권한 칸이 다르므로 여기 값으로 역할 권한 화면이 해당 칸만 켜서 보여 준다. 사용자 정의 1~8 은 이름을 함께 담는다.</summary>
public class MenuPermissionItemsDto
{
    public bool UseView { get; set; } = true;
    public bool UseSearch { get; set; } = true;
    public bool UseCreate { get; set; } = true;
    public bool UseDelete { get; set; } = true;
    public bool UseUpdate { get; set; } = true;
    public bool UsePrint { get; set; } = true;
    public bool UseExcel { get; set; } = true;

    public bool UseCust1 { get; set; }
    public bool UseCust2 { get; set; }
    public bool UseCust3 { get; set; }
    public bool UseCust4 { get; set; }
    public bool UseCust5 { get; set; }
    public bool UseCust6 { get; set; }
    public bool UseCust7 { get; set; }
    public bool UseCust8 { get; set; }

    public string? Cust1Name { get; set; }
    public string? Cust2Name { get; set; }
    public string? Cust3Name { get; set; }
    public string? Cust4Name { get; set; }
    public string? Cust5Name { get; set; }
    public string? Cust6Name { get; set; }
    public string? Cust7Name { get; set; }
    public string? Cust8Name { get; set; }
}
