using System.Text.Json;

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

    /// <summary>
    /// 계정에 담아 둔 화면 환경설정. <c>/user/preferences</c> 와 같다.
    /// 저장된 것이 없으면 빈 객체(<c>{}</c>)다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 여기 실리나.</b> 휴대폰 아래 띠·떠다니는 단추·토스트 위치가
    /// 브라우저 저장소를 떠나 계정으로 올라왔다. 그런데 그것들을 읽는 자리는
    /// <b>레이아웃이 뜨는 바로 그 순간</b>이라, 따로 부르게 두면 포털이
    /// 업무를 넘나들 때마다(레이아웃이 새로 생긴다) 왕복이 하나씩 는다.
    /// </para>
    /// <para>
    /// 여기 얹으면 <b>왕복이 0 이다</b> — 이 응답은 프론트가 2분간 들고 있다
    /// (<c>PortalBootstrapStore</c>).
    /// </para>
    /// <para>
    /// 서버는 내용을 해석하지 않는다. 담긴 모양을 아는 것은 프론트뿐이다.
    /// </para>
    /// </remarks>
    public JsonElement Preferences { get; set; }
}
