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
    /// 메뉴 메타 데이터 (아이콘, 정렬 등)
    /// </summary>
    public SystemMenuMetaDto Meta { get; set; } = new();

    /// <summary>
    /// 하위 메뉴 트리
    /// </summary>
    public List<SystemMenuDto>? Children { get; set; }
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
    /// 메타 데이터 설정
    /// </summary>
    public SystemMenuMetaDto Meta { get; set; } = new();
}
