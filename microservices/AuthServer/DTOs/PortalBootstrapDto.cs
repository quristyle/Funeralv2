using System.Text.Json;

namespace AuthServer.DTOs;

/// <summary>
/// 포털 레이아웃 한 벌을 채우는 데 필요한 전부.
/// <c>GET /auth/portal/bootstrap</c> 의 응답이다.
///
/// 칸마다 기존 엔드포인트와 모양이 같아, 프론트 파서를 그대로 쓴다. 서버는 내용을 해석하지 않는다.
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

    /// <summary>계정에 담아 둔 화면 환경설정. /user/preferences 와 같다. 없으면 빈 객체다.</summary>
    /// 휴대폰 띠·떠다니는 단추 위치 같은 값을 계정에 올려, 레이아웃이 뜨는 순간 왕복 없이 읽게 한다.
    public JsonElement Preferences { get; set; }

    /// <summary>레이아웃 상단 도구 아이콘 목록 및 기본 사용 여부.</summary>
    public List<LayoutHeaderIconDto> HeaderIcons { get; set; } = [];
}
