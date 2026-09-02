namespace AuthServer.DTOs;

/// <summary>
/// 시스템 메뉴 관리(CRUD)를 위한 데이터 구조 DTO
/// </summary>
public class SystemMenuDto
{
    /// <summary>
    /// 메뉴 아이디 (GUID)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 메뉴 명칭
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 브라우저 접속 경로
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 프론트엔드 컴포넌트 경로
    /// </summary>
    public string? Component { get; set; }

    /// <summary>
    /// 상위 메뉴 아이디 (Pid)
    /// </summary>
    public string? Pid { get; set; }

    /// <summary>
    /// 리다이렉트 경로
    /// </summary>
    public string? Redirect { get; set; }

    /// <summary>
    /// 메뉴 유형 (menu, catalog 등)
    /// </summary>
    public string Type { get; set; } = "menu";

    /// <summary>
    /// 권한 식별 코드
    /// </summary>
    public string? AuthCode { get; set; }

    /// <summary>
    /// 메뉴 사용 상태 (0: 비활성, 1: 활성)
    ///
    /// 비활성 메뉴는 사이드바 조회 API(<c>/auth/menu/all</c>)가 아예 내려주지 않으므로
    /// 라우트도 만들어지지 않는다. 메뉴 관리 화면은 비활성 메뉴까지 보여 주고 켜고 끌 수 있어야 하므로
    /// 이 값이 반드시 함께 나가야 한다.
    /// </summary>
    public int Status { get; set; } = 1;

    /// <summary>
    /// 메뉴 메타 데이터 (아이콘, 정렬 등)
    /// </summary>
    public SystemMenuMetaDto Meta { get; set; } = new();

    /// <summary>
    /// 하위 메뉴 트리
    /// </summary>
    public List<SystemMenuDto>? Children { get; set; }

    /// <summary>
    /// 이 메뉴가 사용하는 권한 항목 설정
    /// </summary>
    public MenuPermissionItemsDto Permissions { get; set; } = new();
}

/// <summary>
/// 시스템 메뉴의 부가 정보를 담는 메타 데이터 DTO
/// </summary>
public class SystemMenuMetaDto
{
    /// <summary>
    /// 화면 표시 제목
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 화면에 그대로 찍을 제목 — <b>서버가 옮겨 둔 글자</b>다.
    ///
    /// <see cref="Title"/> 에는 번역 키(<c>system.menu.title</c>)와 이미 완성된
    /// 글자(<c>공통코드</c>)가 섞여 있다. 예전에는 화면이 제목마다 <c>$t()</c> 를
    /// 불러 옮겼는데, 대부분이 키가 아니어서 vue-i18n 이 "키를 못 찾았다" 경고를
    /// 한 번 그릴 때마다 수백 줄 쏟아냈다. 그게 화면이 늦게 뜨던 이유였다.
    ///
    /// 그래서 <c>scom.i18n_resources</c> 에서 <b>찾았을 때만</b> 옮긴 글자를 담는다.
    /// 못 찾으면 <c>null</c> 로 두어, 화면이 알던 방식(<c>$tIfKey</c>)으로 스스로
    /// 처리하게 남겨 둔다 — 프론트의 언어 파일에만 있는 키가 아직 있기 때문이다.
    /// </summary>
    public string? TitleText { get; set; }

    /// <summary>
    /// 아이콘 명칭
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 메뉴 숨김 여부
    /// </summary>
    public bool HideInMenu { get; set; }

    /// <summary>
    /// 하위 메뉴를 메뉴목록에서 감출지 여부.
    ///
    /// 아래 셋은 수정 창에 체크가 있는데도 저장되지 않던 항목이다
    /// (칸도 DTO 도 없었다). 라우트는 살려 두고 목록에서만 감춘다.
    /// </summary>
    public bool HideChildrenInMenu { get; set; }

    /// <summary>브레드크럼에서 감출지 여부</summary>
    public bool HideInBreadcrumb { get; set; }

    /// <summary>탭 바에서 감출지 여부</summary>
    public bool HideInTab { get; set; }

    /// <summary>
    /// 페이지 캐싱(Keep-Alive) 여부
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// 탭 고정 여부
    /// </summary>
    public bool AffixTab { get; set; }

    /// <summary>
    /// DOM 캐싱 여부
    /// </summary>
    public bool DomCached { get; set; }

    /// <summary>
    /// 권한 목록
    /// </summary>
    public List<string>? Authority { get; set; }

    /// <summary>
    /// 권한 없을 때 메뉴 표시 여부
    /// </summary>
    public bool MenuVisibleWithForbidden { get; set; }

    /// <summary>
    /// 외부 링크 URL
    /// </summary>
    public string? Link { get; set; }

    /// <summary>
    /// Iframe 소스 URL
    /// </summary>
    public string? IframeSrc { get; set; }

    /// <summary>
    /// 뱃지 유형
    /// </summary>
    public string? BadgeType { get; set; }

    /// <summary>
    /// 뱃지 내용
    /// </summary>
    public string? Badge { get; set; }

    /// <summary>
    /// 휴대폰 크기(&lt;768px)에서 메뉴목록에 보일지 여부.
    ///
    /// 끄면 그 크기의 <b>메뉴목록에서만</b> 빠진다 — 라우트는 그대로라 주소·즐겨찾기로는 열린다.
    /// 기본값이 true 라 값을 싣지 않은 옛 호출자는 지금과 똑같이 동작한다.
    /// </summary>
    public bool UseMobile { get; set; } = true;

    /// <summary>
    /// 태블릿 크기(768~1023px)에서 메뉴목록에 보일지 여부. <see cref="UseMobile"/> 과 같은 규칙이다.
    /// </summary>
    public bool UseTablet { get; set; } = true;
}

/// <summary>
/// 시스템 메뉴 생성을 위한 데이터 구조 DTO
/// </summary>
public class CreateSystemMenuDto
{
    /// <summary>
    /// 생성할 메뉴 명칭
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 접속 경로
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 컴포넌트 경로
    /// </summary>
    public string? Component { get; set; }

    /// <summary>
    /// 상위 메뉴 아이디
    /// </summary>
    public string? Pid { get; set; }

    /// <summary>
    /// 리다이렉트 경로
    /// </summary>
    public string? Redirect { get; set; }

    /// <summary>
    /// 메뉴 유형
    /// </summary>
    public string Type { get; set; } = "menu";

    /// <summary>
    /// 권한 코드
    /// </summary>
    public string? AuthCode { get; set; }

    /// <summary>
    /// 메뉴 사용 상태 (0: 비활성, 1: 활성).
    ///
    /// <b>일부러 nullable 이다.</b> 값을 싣지 않은 요청은 상태를 건드리지 않는다 —
    /// 이 필드를 모르는 호출자가 메뉴를 저장했다가 비활성 메뉴를 되살리는 일을 막는다.
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// 메타 데이터 설정
    /// </summary>
    public SystemMenuMetaDto Meta { get; set; } = new();

    /// <summary>
    /// 이 메뉴가 사용하는 권한 항목 설정
    /// </summary>
    public MenuPermissionItemsDto Permissions { get; set; } = new();
}

/// <summary>
/// 메뉴가 어떤 권한 항목을 쓰는지에 대한 설정 DTO.
/// </summary>
/// <remarks>
/// role_menus 는 메뉴마다 15가지 권한 칸(열람·조회·추가·삭제·수정·출력·엑셀,
/// 사용자 정의 1~8)을 들고 있지만, 메뉴마다 실제로 의미 있는 항목은 다르다.
/// 여기서 정해둔 값에 따라 역할 권한 화면이 해당 항목만 켜서 보여준다.
/// 사용자 정의 1~8 은 이름을 붙여야 무엇인지 알 수 있으므로 이름도 함께 담는다.
/// </remarks>
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
