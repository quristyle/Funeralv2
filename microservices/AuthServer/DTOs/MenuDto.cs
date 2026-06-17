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
