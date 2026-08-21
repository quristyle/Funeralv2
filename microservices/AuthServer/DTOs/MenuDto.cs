namespace AuthServer.DTOs;

/// <summary>
/// 프론트엔드 라우터 연동을 위한 메뉴 정보 DTO (Vben Admin RouteRecord 대 응)
/// </summary>
public class MenuDto
{
    /// <summary>
    /// 메뉴 및 라우트의 고유 명칭
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 브라우저 접속 경로
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 프론트엔드 컴포넌트 경로 (예: layouts/default/index)
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// 메뉴의 부가 메타 데이터 (아이콘, 제목 등)
    /// </summary>
    public MenuMetaDto Meta { get; set; } = new();

    /// <summary>
    /// 하위 메뉴 목록
    /// </summary>
    public List<MenuDto>? Children { get; set; }
}

/// <summary>
/// 메뉴의 부가 설정을 담는 메타 데이터 클래스
/// </summary>
public class MenuMetaDto
{
    /// <summary>
    /// 메뉴 표시 제목
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 메뉴 아이콘 명칭
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 메뉴바에서 숨김 여부
    /// </summary>
    public bool? HideInMenu { get; set; }

    /// <summary>
    /// 페이지 유지(Keep-Alive) 여부
    /// </summary>
    public bool? KeepAlive { get; set; }

    /// <summary>
    /// 탭 고정 여부
    /// </summary>
    public bool? AffixTab { get; set; }

    /// <summary>
    /// DOM 캐싱 여부
    /// </summary>
    public bool? DomCached { get; set; }

    /// <summary>
    /// 프론트엔드 컴포넌트 경로 (meta 에도 담아 주자)
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// 권한 목록
    /// </summary>
    public List<string>? Authority { get; set; }

    /// <summary>
    /// 권한 없을 때 메뉴 표시 여부
    /// </summary>
    public bool? MenuVisibleWithForbidden { get; set; }

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
    /// 특정 하위 경로 접속 시 활성화될 상위 메뉴
    /// </summary>
    public string? ActiveMenu { get; set; }

    /// <summary>
    /// 메뉴 정렬 순서
    /// </summary>
    public int? Order { get; set; }
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


/// <summary>
/// 로그인한 사용자가 한 메뉴에 대해 실제로 가진 권한.
/// </summary>
/// <remarks>
/// scom.role_menus 는 역할별 권한이다. 한 사람이 여러 역할에 속할 수 있으므로
/// 역할들의 권한을 OR 로 합친 결과를 내려준다. 화면은 이 값만 보고 버튼을 켜고 끈다.
/// 어떤 역할에도 걸려 있지 않은 메뉴는 목록에 아예 담기지 않는다(= 모든 권한 없음).
/// </remarks>
public class MenuPermissionDto
{
    public string MenuId { get; set; } = string.Empty;

    /// <summary>
    /// 메뉴의 라우트 경로. 화면은 자기 경로로 자기 권한을 찾는다.
    /// (/menu/all 응답에는 메뉴 아이디가 없어서 경로가 연결 고리가 된다)
    /// </summary>
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
