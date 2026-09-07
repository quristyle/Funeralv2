using System.Security.Claims;
using System.Text.Json.Serialization;
using JSini.Web.Components.Menu;
using JSini.Web.Components.Security;
using JSini.Web.Http;
using JSini.Web.Models.Menu;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 레이아웃이 뜰 때 필요한 넷을 <b>한 번의 왕복</b>으로 채운다.
///
/// [무엇을 고친 것인가]
///
/// <c>MainLayout</c> 은 권한표 · 메뉴 · 즐겨찾기 · 내정보를 순차로 불렀다.
/// 넷 다 같은 사용자의 것이고 서로 기다릴 이유가 없는데 왕복이 네 번이었다.
/// 게다가 포털은 <b>업무 모듈을 넘나들 때마다</b> 레이아웃을 새로 만든다
/// (Piral 모듈 컨테이너가 갈린다). 그래서 화면 전환 한 번의 값이 왕복 넷이었다.
///
/// [왜 프론트에서 병렬로 쏘지 않았나]
///
/// 순서 제약이 있다 — 권한표가 메뉴보다 먼저 와야 걸러지지 않은 사이드바가
/// 한 번 번쩍이지 않는다. 병렬로 쏘면 그 제약이 깨진다. 서버에서 묶으면
/// 순서 문제가 사라지고, 넷이 <b>같은 시점의 값</b>이라는 것도 덤으로 얻는다.
///
/// [떨어지는 길을 남겨 둔다]
///
/// 부트스트랩이 실패하면 옛 방식으로 넷을 따로 읽는다. 그래야 <b>프론트를
/// 먼저 배포해도 안전하다</b> — 아직 그 엔드포인트가 없는 백엔드를 만나면
/// 404 를 받고 조용히 옛 길로 간다. 반대 순서로 배포해도 마찬가지다.
/// 옛 엔드포인트 넷은 그래서 지우지 않는다.
/// </summary>
public sealed class PortalBootstrap(
    GatewayClient gateway,
    PortalBootstrapStore store,
    MenuProvider menus,
    PermissionContext permissions,
    MenuFavorites favorites,
    CurrentUser me,
    ILogger<PortalBootstrap> logger)
{
    /// <summary>넷을 채운다. 통에 있으면 게이트웨이를 부르지 않는다.</summary>
    /// <param name="user">
    /// 지금 사람. <see cref="PortalBootstrapStore"/> 의 열쇠를 만드는 데 쓴다.
    /// <c>null</c> 이면 통을 쓰지 않고 매번 읽는다.
    /// </param>
    /// <param name="cancellationToken">취소 신호.</param>
    public async Task LoadAsync(
        ClaimsPrincipal? user = null, CancellationToken cancellationToken = default)
    {
        var key = user is null ? null : PortalBootstrapStore.KeyFor(user);

        // ① 통에 있으면 그대로 쓴다. **게이트웨이를 아예 안 부른다.**
        //
        // 업무 모듈을 넘나들 때마다 레이아웃이 새로 생기므로(Piral 모듈
        // 컨테이너가 갈린다) 여기가 화면 전환마다 도는 자리다.
        if (key is not null && store.TryGet(key, out var cached) && cached is not null)
        {
            ApplyAll(cached);
            return;
        }

        // ② 한 번의 왕복으로 넷을 받는다.
        try
        {
            var wire = await gateway.GetOneAsync<PortalBootstrapWire>(
                "auth/portal/bootstrap", cancellationToken);

            if (wire is not null)
            {
                if (key is not null)
                {
                    store.Put(key, wire);
                }

                ApplyAll(wire);
                KeepFavoritesFresh(wire);
                return;
            }

            logger.LogWarning("부트스트랩이 빈 응답이었다. 넷을 따로 읽는다.");
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "부트스트랩을 읽지 못했다. 넷을 따로 읽는다.");
        }

        // ③ 옛 길.
        await LoadSeparatelyAsync(cancellationToken);
    }

    /// <summary>
    /// 받아 둔 한 벌을 넷에 나눠 담는다.
    ///
    /// 순서는 서버가 준 대로가 아니라 <b>화면이 필요한 대로</b>다 —
    /// 권한표가 먼저여야 메뉴를 담는 순간 이미 걸러진다.
    /// </summary>
    private void ApplyAll(PortalBootstrapWire wire)
    {
        permissions.Apply(wire.Permissions);
        menus.Apply(wire.Menus);
        favorites.Apply(wire.Favorites);
        me.Apply(wire.User);
    }

    /// <summary>
    /// 통에 든 즐겨찾기를 사용자가 바꾼 것과 맞춰 둔다.
    ///
    /// <b>즐겨찾기만 특별한 이유</b> — 넷 중에서 사용자가 스스로, 자주 바꾸는
    /// 것이 이것뿐이다. 별을 눌러 담아 놓고 업무를 옮기면 통에 든 옛 목록이
    /// 되살아나 <i>방금 담은 것이 사라진 것처럼</i> 보인다.
    ///
    /// 메뉴·권한·내 정보는 관리자나 다른 화면이 바꾸는 것이라 통의 수명
    /// (<see cref="PortalBootstrapStore.Ttl"/>)만큼 늦어도 된다.
    ///
    /// 이 서비스와 <c>MenuFavorites</c> 는 수명이 같으므로(둘 다 scoped)
    /// 구독을 떼지 않아도 함께 사라진다.
    /// </summary>
    private void KeepFavoritesFresh(PortalBootstrapWire wire)
    {
        if (_watchingFavorites)
        {
            return;
        }

        _watchingFavorites = true;
        favorites.Changed += () => wire.Favorites = [.. favorites.Items];
    }

    private bool _watchingFavorites;

    /// <summary>
    /// 옛 길. 넷을 따로 읽는다.
    ///
    /// <b>순서가 넷 다 중요하다</b> — 권한표가 메뉴보다, 메뉴가 즐겨찾기보다
    /// 먼저다(즐겨찾기는 메뉴가 있어야 이름을 붙인다). 헤더의 얼굴은 맨 뒤다.
    /// 사이드바가 먼저 그려지는 편이 낫고, 얼굴은 늦게 떠도 이름 첫 글자가
    /// 자리를 지킨다.
    /// </summary>
    private async Task LoadSeparatelyAsync(CancellationToken cancellationToken)
    {
        await permissions.ReloadAsync(cancellationToken);
        await menus.ReloadAsync(cancellationToken);
        await favorites.ReloadAsync(cancellationToken);
        await me.ReloadAsync(cancellationToken);
    }

    /// <summary>
    /// <c>GET /auth/portal/bootstrap</c> 응답. 칸마다 옛 엔드포인트가 하나씩
    /// 있고 <b>모양이 똑같다</b> — 쓰던 파서를 그대로 쓴다.
    /// </summary>
    public sealed class PortalBootstrapWire
    {
        [JsonPropertyName("menus")]
        public List<MenuWireDto> Menus { get; set; } = [];

        [JsonPropertyName("permissions")]
        public List<MenuPermissionDto> Permissions { get; set; } = [];

        [JsonPropertyName("favorites")]
        public List<MenuFavorite> Favorites { get; set; } = [];

        [JsonPropertyName("user")]
        public CurrentUser.UserInfoWire? User { get; set; }
    }
}
