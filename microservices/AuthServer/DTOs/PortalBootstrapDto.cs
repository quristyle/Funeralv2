namespace AuthServer.DTOs;

/// <summary>
/// 포털 레이아웃 한 벌을 채우는 데 필요한 전부.
/// <c>GET /auth/portal/bootstrap</c> 의 응답이다.
///
/// 칸마다 옛 엔드포인트가 하나씩 있고 <b>모양이 똑같다</b> — 프론트가
/// 쓰던 파서를 그대로 쓸 수 있어야 갈아 끼우는 값이 싸다.
/// </summary>
public class PortalBootstrapDto
{
    /// <summary>사이드바 메뉴 트리. <c>/menu/all</c> 과 같다.</summary>
    public List<MenuDto> Menus { get; set; } = [];

    /// <summary>메뉴별 권한. <c>/menu/permissions</c> 와 같다.</summary>
    public List<MenuPermissionDto> Permissions { get; set; } = [];

    /// <summary>즐겨찾기. <c>/menu/favorites</c> 와 같다.</summary>
    public List<MenuFavoriteDto> Favorites { get; set; } = [];

    /// <summary>
    /// 헤더에 쓸 내 정보. <c>/user/info</c> 와 같다.
    /// 못 찾으면 <c>null</c> — 헤더에 이름만 나오고 포털은 그대로 돈다.
    /// </summary>
    public UserInfoDto? User { get; set; }
}
