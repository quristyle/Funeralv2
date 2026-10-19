using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using JSini.Web.Components.Menu;
using JSini.Web.Components.Settings;
using JSini.Web.Components.Security;
using JSini.Web.Http;
using JSini.Web.Models.Menu;

namespace JSini.Web.Components.Layout;

/// <summary>레이아웃이 뜰 때 필요한 넷(권한표 · 메뉴 · 즐겨찾기 · 내 정보)을 한 번의 왕복으로 채운다. 레이아웃은 업무를 넘나들 때마다 새로 생기므로 전에는 화면 전환 한 번에 왕복이 넷이었다. 서버에서 묶으면 순서 제약(권한표가 메뉴보다 먼저)이 사라지고 넷이 같은 시점의 값이 된다.
/// 옛 엔드포인트 넷은 지우지 않는다 — 프론트를 먼저 배포해도 404 를 받으면 옛 길로 가므로 안전하다.</summary>
public sealed class PortalBootstrap(
    GatewayClient gateway,
    PortalBootstrapStore store,
    MenuProvider menus,
    PermissionContext permissions,
    MenuFavorites favorites,
    CurrentUser me,
    PortalPrefs prefs,
    HeaderIconManager headerIcons,
    ILogger<PortalBootstrap> logger)
{
    /// <summary>넷을 채운다. 통에 있으면 게이트웨이를 부르지 않는다.</summary>
    /// <param name="user">지금 사람. PortalBootstrapStore 의 열쇠를 만드는 데 쓴다. null 이면 통을 쓰지 않고 매번 읽는다.</param>
    /// <param name="cancellationToken">취소 신호.</param>
    public async Task LoadAsync(
        ClaimsPrincipal? user = null, CancellationToken cancellationToken = default)
    {
        var key = user is null ? null : PortalBootstrapStore.KeyFor(user);

        // 통에 있으면 그대로 쓴다 — 게이트웨이를 아예 안 부른다. 업무를 넘나들 때마다 레이아웃이 새로 생기므로 여기가 화면 전환마다 도는 자리다.
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

    /// <summary>받아 둔 한 벌을 넷에 나눠 담는다. 순서는 화면이 필요한 대로다 — 권한표가 먼저여야 메뉴를 담는 순간 이미 걸러진다.</summary>
    private void ApplyAll(PortalBootstrapWire wire)
    {
        permissions.Apply(wire.Permissions);

        // 트리를 기억하는 통을 함께 넘긴다. 통이 이 응답에 매달려 있으므로
        // 두 번째 업무 전환부터는 179노드를 다시 만들지 않고, 결과의 참조가
        // 같아서 사이드바(DxTreeView)도 다시 그리지 않는다.
        menus.Apply(wire.Menus, wire.MenuTree);
        favorites.Apply(wire.Favorites);
        me.Apply(wire.User);

        // 다섯째 — 계정에 담긴 화면 설정(아래 띠 · 바로가기 단추 · 토스트 자리).
        // **왕복을 아끼려고 여기 실려 온다**(`PortalPrefs` 머리말).
        prefs.Seed(wire.Preferences);
        KeepPrefsFresh(wire);

        // 여섯째 — 레이아웃 상단 도구 아이콘 목록 및 기본값 설정
        headerIcons.Apply(wire.HeaderIcons);
    }

    /// <summary>통에 든 설정을 사용자가 바꾼 것과 맞춰 둔다. 즐겨찾기와 같은 까닭이고 더 자주 걸린다 — 스위치를 켜고 업무를 옮기면 안 맞춘 통이 옛 값을 되살려 방금 켠 것이 꺼진 것처럼 보인다. 통에 넣기 전에 걸어도 된다 — 담기는 것은 같은 객체의 참조라 나중에 고쳐도 통 안의 것이 함께 바뀐다.</summary>
    private void KeepPrefsFresh(PortalBootstrapWire wire)
    {
        if (_watchingPrefs)
        {
            return;
        }

        _watchingPrefs = true;
        prefs.Changed += payload => wire.Preferences = payload;
    }

    private bool _watchingPrefs;

    /// <summary>통에 든 즐겨찾기를 사용자가 바꾼 것과 맞춰 둔다. 넷 중 사용자가 스스로 자주 바꾸는 것이 이것뿐이라 특별하다 — 별을 눌러 담고 업무를 옮기면 옛 목록이 되살아나 방금 담은 것이 사라진 것처럼 보인다. 메뉴·권한·내 정보는 관리자가 바꾸는 것이라 통의 수명만큼 늦어도 된다. 이 서비스와 MenuFavorites 는 수명이 같아 구독을 떼지 않아도 함께 사라진다.</summary>
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

    /// <summary>옛 길. 넷을 따로 읽는다. 순서가 넷 다 중요하다 — 권한표가 메뉴보다, 메뉴가 즐겨찾기보다 먼저다(즐겨찾기는 메뉴에서 이름을 붙인다). 헤더의 얼굴은 맨 뒤다 — 얼굴이 늦게 떠도 이름 첫 글자가 자리를 지킨다.</summary>
    private async Task LoadSeparatelyAsync(CancellationToken cancellationToken)
    {
        await permissions.ReloadAsync(cancellationToken);
        await menus.ReloadAsync(cancellationToken);
        await favorites.ReloadAsync(cancellationToken);
        await me.ReloadAsync(cancellationToken);
    }

    /// <summary>GET /auth/portal/bootstrap 응답. 칸마다 옛 엔드포인트가 하나씩 있고 모양이 같아 쓰던 파서를 그대로 쓴다.</summary>
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

        /// <summary>계정에 담아 둔 화면 설정. 담긴 것이 없으면 빈 객체다. 무엇이 들었는지 아는 곳은 PortalPrefs 하나다.</summary>
        [JsonPropertyName("preferences")]
        public JsonElement Preferences { get; set; }

        [JsonPropertyName("headerIcons")]
        public List<JSini.Web.Models.LayoutHeaderIconDto> HeaderIcons { get; set; } = [];

        /// <summary>이 응답으로 만든 메뉴 트리를 기억하는 통. 통의 수명이 이 응답의 수명과 같아 「메뉴와 권한표가 그때 그대로냐」를 따로 확인할 필요가 없다 — 통이 있으면 그렇다는 뜻이다(MenuTree 머리말). internal 이라 직렬화에 실리지 않고 받은 뒤에 채우는 자리다.</summary>
        internal Menu.MenuTree MenuTree { get; } = new();
    }
}
