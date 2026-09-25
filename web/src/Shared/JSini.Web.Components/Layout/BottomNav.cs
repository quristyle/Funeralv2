using System.Text.Json;
using System.Text.Json.Serialization;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 휴대폰 화면 아래에 붙는 띠(<see cref="MobileBottomNav"/>)에 무엇을 놓을지.
///
/// <para>
/// [칸 다섯을 사람이 고른다]
/// </para>
///
/// <para>
/// 한동안 그 다섯은 코드에 박혀 있었다(홈 · 빠른지시 · 알림 · 설정 · 내 정보).
/// 그런데 이 포털은 업무가 여덟이고 사람마다 하루 종일 붙어 있는 화면이
/// 다르다 — 장례식장만 쓰는 사람에게 「빠른지시」는 한 번도 안 누르는 칸이고,
/// 그 사람이 정작 자주 여는 빈소 현황은 메뉴를 열어야 닿는다.
/// </para>
///
/// <para>
/// [고르지 않으면 지금 그대로다]
/// </para>
///
/// <para>
/// 저장해 둔 것이 없으면 <see cref="Defaults"/> 다. <b>기본 다섯은 아이콘을
/// 직접 들고 있다</b>(<see cref="BottomNavItem.Icon"/>) — 홈과 설정은 뒤에
/// 메뉴가 없고, 나머지 셋도 지금 화면에 떠 있는 그림이 그것이라 여기서
/// 메뉴 아이콘으로 갈아 끼우면 <b>아무것도 안 고친 사람의 띠가 바뀐다.</b>
/// 사람이 고른 칸만 메뉴의 아이콘을 따라간다.
/// </para>
///
/// <para>
/// [브라우저에 남는다]
/// </para>
///
/// <para>
/// 떠다니는 단추 위치·토스트 위치와 같은 갈래다(<see cref="PortalBoot"/>).
/// <b>이 띠는 휴대폰에서만 보이므로</b> 계정에 담아 봐야 큰 모니터에서는
/// 쓰이지 않고, 반대로 담아 두면 회사 컴퓨터에서 고른 것이 휴대폰까지
/// 따라온다. 게다가 계정에 두면 읽으려고 왕복이 하나 더 는다 —
/// <see cref="PortalBoot"/> 의 단일 왕복에 열쇠 두 줄을 더하는 것으로 끝난다.
/// </para>
/// </summary>
public static class BottomNav
{
    /// <summary>
    /// 띠에 놓을 수 있는 칸 수. <b>다섯을 넘기지 않는다</b> — 360px 짜리
    /// 화면에서 칸 하나가 72px 이고, 여섯째부터는 이름이 두 글자에서 끊긴다.
    /// </summary>
    public const int MaxItems = 5;

    /// <summary>홈. 메뉴가 아니라 주소 하나라 따로 둔다.</summary>
    public const string HomePath = "/";

    /// <summary>
    /// 테마 서랍을 여는 칸. <b>주소가 아니다</b> — 오른쪽 위 팔레트 단추와
    /// 같은 서랍을 그 자리에서 연다. 주소로 흉내 낼 수 없어서 표시를 하나 쓴다
    /// (<c>#</c> 로 시작하므로 메뉴 경로와 절대 겹치지 않는다).
    /// </summary>
    public const string ThemePath = "#theme";

    /// <summary>전체 메뉴(사이드바)를 여는 가짜 주소.</summary>
    public const string MenuPath = "#menu";

    /// <summary>내 정보(프로필)를 여는 가짜 주소.</summary>
    public const string ProfilePath = "#profile";

    /// <summary>
    /// 고른 적이 없을 때의 다섯. <b>지금 화면에 떠 있는 그대로다.</b>
    /// </summary>
    public static readonly IReadOnlyList<BottomNavItem> Defaults =
    [
        new() { Path = HomePath, Title = "홈", Icon = "jsini-icon-home" },
        new()
        {
            Path = "/projmng/ai/ask",
            RouteKey = "projmng.ai.ask",
            Title = "빠른지시",
            Icon = "jsini-icon-bolt",
        },
        new() { Path = "/admin/push/history", Title = "알림", Icon = "jsini-icon-bell" },
        new() { Path = ThemePath, Title = "설정", Icon = "jsini-icon-palette" },
        new() { Path = "/admin/profile", Title = "내 정보", Icon = "jsini-icon-user" },
    ];

    /// <summary>
    /// 메뉴에 아이콘이 없을 때 대신 붙일 그림들. <b>고정된 하나로 두지
    /// 않는다</b> — 아이콘 없는 메뉴를 둘 이상 놓으면 띠에 똑같은 그림이
    /// 나란히 서서 어느 칸이 무엇인지 그림으로는 못 가린다.
    /// </summary>
    /// <remarks>
    /// 이름은 <c>menu-icons.css</c> 에 실제로 있는 것들이다. 없는 이름을 적으면
    /// <see cref="MenuIcons"/> 의 기본값(동그라미)이 나오므로 조용히 다 같아진다.
    /// </remarks>
    private static readonly string[] FallbackIcons =
    [
        "lucide:layout-dashboard",
        "lucide:list",
        "lucide:folder",
        "lucide:file-text",
        "lucide:calendar",
        "lucide:users",
        "lucide:tags",
        "lucide:settings",
    ];

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// 저장해 둔 글자를 칸 목록으로. 비었거나 읽을 수 없으면
    /// <see cref="Defaults"/> 다.
    /// </summary>
    /// <remarks>
    /// <b>「칸이 없다」를 돌려주지 않는다.</b> 띠를 쓰기로 해 놓고 칸이 하나도
    /// 없으면 화면 아래에 빈 띠만 남고, 그 자리에서 사람은 고장으로 읽는다.
    /// 칸을 다 지우는 것은 「쓰지 않기」로 말해야 한다.
    /// </remarks>
    public static IReadOnlyList<BottomNavItem> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Defaults;
        }

        BottomNavItem[]? items;

        try
        {
            items = JsonSerializer.Deserialize<BottomNavItem[]>(json, Json);
        }
        catch (JsonException)
        {
            // 사람이 저장소를 고쳤거나 옛 모양이 남아 있다. 없던 것으로 본다.
            return Defaults;
        }

        if (items is null)
        {
            return Defaults;
        }

        var cleaned = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Path)
                && !string.IsNullOrWhiteSpace(item.Title))
            .Take(MaxItems)
            .ToArray();

        return cleaned.Length == 0 ? Defaults : cleaned;
    }

    /// <summary>칸 목록을 저장할 글자로. 다섯을 넘으면 앞에서 자른다.</summary>
    public static string Serialize(IEnumerable<BottomNavItem> items) =>
        JsonSerializer.Serialize(items.Take(MaxItems).ToArray(), Json);

    /// <summary>
    /// 이 칸이 실제로 갈 주소. 못 찾으면 적어 둔 값 그대로다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>메뉴를 한 번 거친다.</b> DB 의 <c>path</c> 와 Blazor 라우트가 아직
    /// 다를 수 있어서(<see cref="MenuNode.Href"/> 의 머리말), 적어 둔 값으로
    /// 곧장 가면 이관이 끝나지 않은 화면에서 "준비 중" 이 뜬다.
    /// </para>
    /// <para>
    /// 화면 열쇠(<see cref="BottomNavItem.RouteKey"/>)를 먼저 본다 — 경로는
    /// 옮겨 가도 그 값은 그대로다.
    /// </para>
    /// </remarks>
    public static string Resolve(BottomNavItem item, IReadOnlyList<MenuNode> menus) =>
        Find(item, menus)?.LinkTarget ?? item.Path;

    /// <summary>이 칸이 가리키는 메뉴. 없으면 <c>null</c>.</summary>
    public static MenuNode? Find(BottomNavItem item, IReadOnlyList<MenuNode> menus)
    {
        if (!string.IsNullOrWhiteSpace(item.RouteKey)
            && FindBy(menus, node => Same(node.RouteKey, item.RouteKey)) is { } byKey)
        {
            return byKey;
        }

        return FindBy(menus, node =>
            Same(node.LinkTarget, item.Path) || Same(node.Path, item.Path));
    }

    /// <summary>
    /// 이 칸에 붙일 아이콘 CSS 클래스.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 순서가 있다 — <b>박아 둔 것 → 메뉴의 것 → 임의의 것</b>.
    /// </para>
    /// <list type="number">
    ///   <item>기본 다섯은 아이콘을 직접 들고 있다(<see cref="Defaults"/>).</item>
    ///   <item>
    ///     사람이 고른 칸은 <b>메뉴에 달린 아이콘</b>을 따라간다. 관리자가 메뉴
    ///     아이콘을 바꾸면 띠도 따라 바뀐다 — 그러라고 여기서 이름을 굳히지
    ///     않고 그릴 때마다 찾는다.
    ///   </item>
    ///   <item>
    ///     아이콘이 없는 메뉴면 경로로 정한 아무 그림 하나를 쓴다. 같은 칸은
    ///     늘 같은 그림이다(<see cref="FallbackIcons"/>).
    ///   </item>
    /// </list>
    /// </remarks>
    public static string IconClass(BottomNavItem item, IReadOnlyList<MenuNode> menus)
    {
        if (Sanitize(item.Icon) is { } fixedIcon)
        {
            return fixedIcon;
        }

        if (Same(item.Path, HomePath))
        {
            return "jsini-icon-home";
        }

        if (Same(item.Path, ThemePath))
        {
            return "jsini-icon-palette";
        }

        if (Same(item.Path, MenuPath))
        {
            return "jsini-icon-menu";
        }

        if (Same(item.Path, ProfilePath))
        {
            return "jsini-icon-user";
        }

        var icon = Find(item, menus)?.Icon;

        return MenuIcons.CssClass(string.IsNullOrWhiteSpace(icon) ? Fallback(item.Path) : icon);
    }

    /// <summary>
    /// 환경설정의 고르개에 놓을 것들. <b>묶음(CATALOG)은 뺀다</b> — 제 화면이
    /// 없어서 골라 봐야 "준비 중" 이 뜬다.
    /// </summary>
    /// <param name="menus">거른 뒤의 메뉴 트리(<see cref="IMenuProvider.VisibleMenus"/>).</param>
    /// <remarks>
    /// 이름은 <b>줄기를 붙여</b> 적는다(「포털관리 › 알림 이력」). 붙이지 않으면
    /// 「목록」·「현황」 같은 이름이 업무마다 있어서 어느 것을 고르는지 알 수 없다.
    /// </remarks>
    public static IReadOnlyList<BottomNavChoice> Choices(IReadOnlyList<MenuNode> menus)
    {
        var choices = new List<BottomNavChoice>
        {
            new(HomePath, null, "홈", "jsini-icon-home"),
            new(ThemePath, null, "설정 (테마 서랍 열기)", "jsini-icon-palette"),
            new(MenuPath, null, "전체 메뉴 (사이드바 열기)", "jsini-icon-menu"),
            new(ProfilePath, null, "내 정보 (프로필 열기)", "jsini-icon-user"),
        };

        Walk(menus, null);

        return choices;

        void Walk(IReadOnlyList<MenuNode> nodes, string? trail)
        {
            foreach (var node in nodes)
            {
                var label = trail is null ? node.Title : $"{trail} › {node.Title}";

                if (!node.IsCatalog)
                {
                    choices.Add(new BottomNavChoice(
                        node.LinkTarget,
                        node.RouteKey,
                        label,
                        MenuIcons.CssClass(string.IsNullOrWhiteSpace(node.Icon)
                            ? Fallback(node.LinkTarget)
                            : node.Icon)));
                }

                Walk(node.Children, label);
            }
        }
    }

    /// <summary>
    /// 경로로 정하는 아무 그림 하나. <b>같은 경로는 늘 같은 그림</b>이다.
    /// </summary>
    /// <remarks>
    /// <c>string.GetHashCode</c> 를 쓰지 않는다 — .NET 의 그 값은 <b>프로세스마다
    /// 다르다.</b> 그러면 서버를 다시 띄울 때마다 띠의 그림이 바뀌고, 프리렌더와
    /// 회로가 다른 프로세스면 같은 화면에서 한 번 깜빡인다. FNV-1a 로 굳힌다.
    /// </remarks>
    private static string Fallback(string path)
    {
        unchecked
        {
            var hash = 2166136261u;

            foreach (var c in path)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return FallbackIcons[(int)(hash % (uint)FallbackIcons.Length)];
        }
    }

    /// <summary>
    /// 박아 둔 아이콘 클래스를 검사한다. 이름 꼴이 아니면 <c>null</c> 이고,
    /// 그때는 메뉴에서 찾는 길로 떨어진다.
    /// </summary>
    /// <remarks>
    /// 값이 브라우저 저장소에서 온다. 클래스 이름 자리에 그대로 붙이는
    /// 자리라서, 검사하지 않으면 남의 클래스를 하나 더 켜는 글자를 넣을 수 있다
    /// (<see cref="MenuIcons.CssClass"/> 가 같은 이유로 같은 일을 한다).
    /// </remarks>
    private static string? Sanitize(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        var name = icon.Trim();

        foreach (var c in name)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return null;
            }
        }

        return name.StartsWith("jsini-icon-", StringComparison.Ordinal) ? name : null;
    }

    private static MenuNode? FindBy(IReadOnlyList<MenuNode> nodes, Func<MenuNode, bool> match)
    {
        foreach (var node in nodes)
        {
            if (match(node))
            {
                return node;
            }

            if (FindBy(node.Children, match) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    private static bool Same(string? left, string? right) =>
        !string.IsNullOrEmpty(left)
        && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 띠의 칸 하나. <b>글자 열쇠가 짧다</b>(<c>p</c>·<c>t</c>·<c>i</c>·<c>k</c>) —
/// 브라우저 저장소에 그대로 들어가는 글자라 이름이 길면 칸 다섯에 그만큼이
/// 다섯 번 붙는다.
/// </summary>
public sealed record BottomNavItem
{
    /// <summary>
    /// 갈 곳. 메뉴의 링크 주소이거나 <see cref="BottomNav.HomePath"/> ·
    /// <see cref="BottomNav.ThemePath"/> 다.
    /// </summary>
    [JsonPropertyName("p")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// 띠에 적을 이름. <b>사람이 정한다</b> — 메뉴 이름이 「알림 이력 조회」
    /// 처럼 길면 72px 칸에서 끊기므로, 「알림」 처럼 줄여 적을 수 있어야 한다.
    /// </summary>
    [JsonPropertyName("t")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 박아 둔 아이콘 클래스. <b>기본 다섯만 갖는다</b> — 사람이 고른 칸은
    /// 비어 있고, 그때 아이콘은 메뉴에서 찾는다
    /// (<see cref="BottomNav.IconClass"/>).
    /// </summary>
    [JsonPropertyName("i")]
    public string? Icon { get; init; }

    /// <summary>
    /// 가리키는 화면의 열쇠(<see cref="MenuNode.RouteKey"/>). 경로가 옮겨
    /// 가도 이 값은 그대로라 <b>먼저 이것으로 찾는다.</b>
    /// </summary>
    [JsonPropertyName("k")]
    public string? RouteKey { get; init; }
}

/// <summary>환경설정의 고르개 한 줄.</summary>
/// <param name="Path">고르면 칸에 적힐 주소.</param>
/// <param name="RouteKey">그 화면의 열쇠. 메뉴가 아닌 것(홈·설정)은 <c>null</c>.</param>
/// <param name="Label">줄기를 붙인 이름 — 「포털관리 › 알림 이력」.</param>
/// <param name="Icon">미리 보여 줄 아이콘 클래스.</param>
public sealed record BottomNavChoice(
    string Path, string? RouteKey, string Label, string Icon);
