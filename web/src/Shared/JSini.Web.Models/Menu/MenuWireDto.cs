using System.Text.Json.Serialization;
using JSini.Web.Abstractions;

namespace JSini.Web.Models.Menu;

/// <summary>
/// <c>GET /auth/menu/all</c> 응답 한 칸. AuthServer 의 <c>MenuDto</c> 와 짝이다.
///
/// [Component 를 읽지 않는다]
///
/// 이 응답에는 <c>component</c> 칸이 있고 값은 <c>#/views/portal/dashboard/index.vue</c>
/// 같은 Vue 파일 경로다. Vue 는 그 문자열로 라우트를 만들었지만(<c>import.meta.glob</c>
/// 매칭), Blazor 는 <c>@page</c> 로 컴파일 시점에 라우트가 정해진다. 그래서 이 칸은
/// <b>읽지 않는다</b> — DB 는 이제 라우트를 만들지 않고, <see cref="Path"/> 로
/// "이 라우트를 메뉴에 어떻게 노출할지" 만 정한다.
///
/// 그래서 DB 의 <c>path</c> 와 모듈의 <c>@page</c> 가 어긋나면 메뉴는 보이는데
/// 눌러도 404 가 된다. Vue 때는 런타임 <c>console.warn</c> 으로만 알 수 있었지만,
/// 이제는 기동 때 대조해서 로그로 남긴다 (<c>MenuProvider.ReportRouteMismatch</c>).
/// </summary>
public sealed class MenuWireDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

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
        // 서버가 옮겨 준 글자가 있으면 그것을 쓴다. 없으면 저장된 제목,
        // 그것도 비면 메뉴 이름으로 떨어진다.
        Title = FirstNonBlank(Meta.TitleText, Meta.Title, Name),
        Icon = Meta.Icon,

        // 유형은 CATALOG · MENU · EMBEDDED · LINK · BUTTON 다섯 가지다.
        // 사이드바가 갈라 봐야 하는 것은 "자기 화면이 없는 묶음인가" 하나뿐이다.
        IsCatalog = string.Equals(Meta.Type, "CATALOG", StringComparison.OrdinalIgnoreCase),

        UseMobile = Meta.UseMobile,
        UseTablet = Meta.UseTablet,
        HideInMenu = Meta.HideInMenu ?? false,
        Link = Meta.Link,
        OrderNo = Meta.Order ?? 0,
        Children = Children?.Select(c => c.ToNode()).ToList() ?? [],
    };
}

/// <summary>
/// 메뉴의 부가 정보. vben 라우트 메타를 그대로 본뜬 이름들이라 Blazor 에서
/// 쓰이지 않는 칸이 여럿 있다(<c>keepAlive</c> · <c>affixTab</c> · <c>domCached</c>).
/// 그 칸들은 여기 두지 않는다 — 안 쓰는 값을 옮겨 두면 나중에 누가 쓰려다
/// 아무 효과가 없어 헤맨다.
/// </summary>
public sealed class MenuMetaWireDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 서버가 옮겨 둔 제목. <b>있으면 이것을 찍는다.</b>
    ///
    /// <see cref="Title"/> 에는 번역 키(<c>system.menu.title</c>)와 이미 완성된
    /// 글자(<c>공통코드</c>)가 섞여 있다. 179건 중 17건이 키다.
    ///
    /// 화면이 제목마다 번역 함수를 부르는 방식은 쓰지 않는다 — 대부분이 키가
    /// 아니라서 "그런 키는 없다" 경고만 수백 줄 쏟아진다(Vue 에서 실제로 그랬고,
    /// 그게 사이드바가 늦게 뜨던 이유였다). 서버가
    /// <c>scom.i18n_resources</c> 를 한 번 읽어 <b>찾았을 때만</b> 담아 준다.
    ///
    /// 못 찾으면 <c>null</c> 이고, 그때는 저장된 제목이 이미 사람이 읽는 글자라는 뜻이다.
    /// </summary>
    [JsonPropertyName("titleText")]
    public string? TitleText { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("hideInMenu")]
    public bool? HideInMenu { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

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
