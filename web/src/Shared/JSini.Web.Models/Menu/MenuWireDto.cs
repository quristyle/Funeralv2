using System.Text.Json.Serialization;
using JSini.Web.Abstractions;

namespace JSini.Web.Models.Menu;

/// <summary>
/// GET /auth/menu/all 응답 한 칸. AuthServer 의 MenuDto 와 짝이다. component 칸(Vue 파일 경로)은 읽지 않는다. 라우트는 모듈의 @page 가 컴파일 시점에 정하고, DB 는 Path 로 메뉴 노출만 정한다.
/// DB 의 path 와 모듈의 @page 가 어긋나면 메뉴는 보이는데 404 가 된다. 기동 때 대조해 로그로 남긴다(MenuProvider.ReportRouteMismatch).
/// </summary>
public sealed class MenuWireDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>이 메뉴가 가리키는 화면의 열쇠(<c>scom.system_menus.route_key</c>). 라우트를 찾는 길이다. URL 이 바뀌어도 이 값은 그대로라 메뉴가 화면을 잃지 않는다. 비어 있으면 옛 길(RouteAliases + Path)로 떨어진다.</summary>
    [JsonPropertyName("routeKey")]
    public string? RouteKey { get; set; }

    [JsonPropertyName("meta")]
    public MenuMetaWireDto Meta { get; set; } = new();

    [JsonPropertyName("children")]
    public List<MenuWireDto>? Children { get; set; }

    /// <summary>비어 있지 않은 첫 값. 전부 비면 빈 문자열.</summary>
    private static string FirstNonBlank(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;

    /// <summary>셸이 다루는 모양(<see cref="MenuNode"/>)으로 옮긴다.</summary>
    public MenuNode ToNode() => new()
    {
        Path = Path,
        RouteKey = string.IsNullOrWhiteSpace(RouteKey) ? null : RouteKey,
        // 서버가 옮겨 준 글자가 있으면 그것을 쓴다. 없으면 저장된 제목,
        // 그것도 비면 메뉴 이름으로 떨어진다.
        Title = FirstNonBlank(Meta.TitleText, Meta.Title, Name),
        Icon = Meta.Icon,

        // 유형은 CATALOG · MENU · EMBEDDED · LINK · BUTTON 다섯 가지다.
        // 사이드바가 갈라 봐야 하는 것은 "자기 화면이 없는 묶음인가" 하나뿐이다.
        IsCatalog = string.Equals(Meta.Type, "CATALOG", StringComparison.OrdinalIgnoreCase),
        IsEmbedded = string.Equals(Meta.Type, "EMBEDDED", StringComparison.OrdinalIgnoreCase),

        UseMobile = Meta.UseMobile,
        UseTablet = Meta.UseTablet,
        HideInMenu = Meta.HideInMenu ?? false,
        Link = Meta.Link,
        IframeSrc = Meta.IframeSrc,
        KeepAlive = Meta.KeepAlive ?? false,
        OrderNo = Meta.Order ?? 0,
        Children = Children?.Select(c => c.ToNode()).ToList() ?? [],
    };
}

/// <summary>
/// 메뉴의 부가 정보. vben 라우트 메타를 그대로 본뜬 이름들이라 Blazor 에서
/// 쓰이지 않는 칸이 여럿 있다(<c>affixTab</c> · <c>domCached</c>).
/// 그 칸들은 여기 두지 않는다 — 안 쓰는 값을 옮겨 두면 나중에 누가 쓰려다
/// 아무 효과가 없어 헤맨다.
/// </summary>
public sealed class MenuMetaWireDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>서버가 옮겨 둔 제목. 있으면 이것을 찍는다. Title 에는 번역 키와 이미 완성된 글자가 섞여 있다. 화면이 제목마다 번역 함수를 부르면 대부분이 키가 아니라 경고가 쏟아진다. 서버가 i18n 표를 한 번 읽어 찾았을 때만 담고, 못 찾으면 null 이다.</summary>
    [JsonPropertyName("titleText")]
    public string? TitleText { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("hideInMenu")]
    public bool? HideInMenu { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    /// <summary>iframe 으로 띄울 주소. 유형이 EMBEDDED 인 메뉴가 쓴다. 서버는 오래전부터 보냈는데 이 칸이 없어 버려지고 있었다. 화면들이 주소를 코드에 박아 두던 까닭이다.</summary>
    [JsonPropertyName("iframeSrc")]
    public string? IframeSrc { get; set; }

    /// <summary>탭을 옮겨도 화면을 살려 둘지(<c>keep_alive</c>).</summary>
    [JsonPropertyName("keepAlive")]
    public bool? KeepAlive { get; set; }

    [JsonPropertyName("order")]
    public int? Order { get; set; }

    /// <summary>CATALOG · MENU · EMBEDDED · LINK · BUTTON.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "MENU";

    [JsonPropertyName("useMobile")]
    public bool UseMobile { get; set; } = true;

    [JsonPropertyName("useTablet")]
    public bool UseTablet { get; set; } = true;
}
