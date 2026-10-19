using System.Text.Json;
using System.Text.Json.Serialization;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

/// <summary>휴대폰 아래 띠(MobileBottomNav)에 무엇을 놓을지. 칸 수는 MinItems 와 MaxItems 사이에서 사람이 고른다.
/// 고르지 않았으면 Defaults(홈 · 알림 · 설정 · 프로필 · 메뉴) — 업무에 매인 화면은 기본에 두지 않는다.</summary>
public static class BottomNav
{
    /// <summary>띠에 놓을 수 있는 칸 수의 바닥(3). 둘 이하면 띠로 안 보여 고장으로 읽힌다. 띠를 끄는 길은 BottomNavHiddenKey 이고 칸을 빼서 없애는 길이 아니다.</summary>
    public const int MinItems = 3;

    /// <summary>띠에 놓을 수 있는 칸 수의 천장(8). 320px 에서 버티는 마지막 수다. 이름이 길면 말줄임으로 잘린다 — 칸이 줄지 않으면 띠가 화면보다 넓어져 끝 칸(메뉴)이 밀려 못 눌린다.</summary>
    public const int MaxItems = 8;

    /// <summary>홈. 메뉴가 아니라 주소 하나라 따로 둔다.</summary>
    public const string HomePath = "/";

    /// <summary>테마 서랍을 여는 칸. 주소가 아니라 표시(#로 시작)이므로 메뉴 경로와 겹치지 않는다.</summary>
    public const string ThemePath = "#theme";

    /// <summary>전체 메뉴(사이드바)를 여는 가짜 주소.</summary>
    public const string MenuPath = "#menu";

    /// <summary>내 정보(프로필)를 여는 가짜 주소.</summary>
    public const string ProfilePath = "#profile";

    /// <summary>고른 적이 없을 때의 다섯 — 홈 · 알림 · 설정 · 프로필 · 메뉴. 업무에 매인 화면은 기본에 두지 않는다. 메뉴는 못 닿는 화면을 여는 칸이라 끝에 둔다. 다섯 모두 아이콘을 직접 들고 있어 메뉴 아이콘이 바뀌어도 기본 띠는 그대로다.</summary>
    public static readonly IReadOnlyList<BottomNavItem> Defaults =
    [
        new() { Path = HomePath, Title = "홈", Icon = "jsini-icon-home" },
        new() { Path = "/admin/push/history", Title = "알림", Icon = "jsini-icon-bell" },
        new() { Path = ThemePath, Title = "설정", Icon = "jsini-icon-palette" },
        new() { Path = ProfilePath, Title = "프로필", Icon = "jsini-icon-user" },
        new() { Path = MenuPath, Title = "메뉴", Icon = "jsini-icon-menu" },
    ];

    /// <summary>메뉴에 아이콘이 없을 때 대신 붙일 그림들. 하나로 두지 않는다 — 같은 그림이 나란히 서면 칸을 가릴 수 없다. 이름은 menu-icons.css 에 실제로 있는 것만 적는다(없으면 기본 동그라미가 나온다).</summary>
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

    /// <summary>저장해 둔 글자를 칸 목록으로. 비었거나 못 읽거나 MinItems 에 못 미치면 Defaults. 「칸 없음」을 돌려주지 않는다 — 빈 띠는 고장으로 읽힌다. 모자란 옛 값도 기본으로 떨어뜨린다.</summary>
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

        return cleaned.Length < MinItems ? Defaults : cleaned;
    }

    /// <summary>칸 목록을 저장할 글자로. MaxItems 를 넘으면 앞에서 자른다. 바닥은 여기서 보지 않는다 — 화면이 빼기를 막고 Parse 가 읽을 때 기본으로 떨어뜨린다.</summary>
    public static string Serialize(IEnumerable<BottomNavItem> items) =>
        JsonSerializer.Serialize(items.Take(MaxItems).ToArray(), Json);

    /// <summary>칸 하나를 delta 만큼 옮긴다. 옮겼으면 true. 목록의 위가 띠의 왼쪽이다. 맞바꾸지 않고 뽑아서 끼워 두 칸 넘게 옮겨도 나머지 차례가 흐트러지지 않는다. 목록 밖으로는 나가지 않는다.</summary>
    /// <param name="items">고칠 목록. 이 자리에서 바뀐다.</param>
    /// <param name="index">옮길 칸의 지금 자리.</param>
    /// <param name="delta">몇 칸을 옮길지. 음수면 앞으로(띠에서 왼쪽으로).</param>
    public static bool Move(IList<BottomNavItem> items, int index, int delta)
    {
        if (delta == 0 || index < 0 || index >= items.Count)
        {
            return false;
        }

        var target = index + delta;

        if (target < 0 || target >= items.Count)
        {
            return false;
        }

        var item = items[index];

        items.RemoveAt(index);
        items.Insert(target, item);

        return true;
    }

    /// <summary>이 칸이 실제로 갈 주소. 못 찾으면 적어 둔 값 그대로다. 메뉴를 한 번 거친다 — DB 경로와 라우트가 다를 수 있어 적어 둔 값으로 곧장 가면 준비 중이 뜬다. 화면 열쇠(RouteKey)를 먼저 본다.</summary>
    public static string Resolve(BottomNavItem item, IReadOnlyList<MenuNode> menus) =>
        Find(item, menus)?.LinkTarget ?? item.Path;

    /// <summary>칸을 눌렀을 때 일어나는 일. 띠와 넓은 화면 헤더가 같은 칸 목록을 그리므로 여기 하나에 둔다. 앞 셋은 그 자리에서 여는 표시(Fixed), 나머지는 메뉴를 거친다(Resolve). 메뉴 표시는 여닫이여서 펴져 있으면 판을 접는다 — 접는 판정은 상태를 든 MainLayout 에 있다.</summary>
    public static void Open(
        BottomNavItem item,
        IReadOnlyList<MenuNode> menus,
        NavigationManager navigation,
        ThemeDrawer theme,
        MenuReveal reveal,
        UserMenuDrawer userDrawer)
    {
        if (Same(item.Path, ThemePath))
        {
            theme.Open();
            return;
        }

        if (Same(item.Path, MenuPath))
        {
            reveal.Request(MenuPath);
            return;
        }

        if (Same(item.Path, ProfilePath))
        {
            userDrawer.Toggle();
            return;
        }

        navigation.NavigateTo(Resolve(item, menus));
    }

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

    /// <summary>이 화면이 띠에 이미 서 있는가. 있으면 그 자리, 없으면 -1. 열쇠를 먼저 본다 — 옛 경로로 맞대면 이미 놓은 칸을 없다고 읽어 같은 화면이 두 번 선다.</summary>
    /// <param name="items">지금 띠에 선 칸들.</param>
    /// <param name="path">찾을 화면의 링크 주소.</param>
    /// <param name="routeKey">그 화면의 열쇠(MenuNode.RouteKey). 없으면 null.</param>
    public static int IndexOf(IReadOnlyList<BottomNavItem> items, string path, string? routeKey)
    {
        if (!string.IsNullOrWhiteSpace(routeKey))
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (Same(items[i].RouteKey, routeKey))
                {
                    return i;
                }
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (Same(items[i].Path, path))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>이 칸에 붙일 아이콘 CSS 클래스. 순서는 박아 둔 것 → 메뉴의 것 → 임의의 것.</summary>
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

    /// <summary>메뉴가 아닌 넷(홈 · 메뉴 등). 고르개 맨 위에 선다. 셋은 그 자리에서 여는 표시라 주소가 없고 #로 시작해 메뉴 경로와 겹치지 않는다. 이름의 괄호는 메뉴 이름과 섞이지 않게 누르면 무슨 일이 나는지를 적은 것이다.</summary>
    public static readonly IReadOnlyList<BottomNavChoice> Fixed =
    [
        new(HomePath, null, "홈", "jsini-icon-home", "홈"),
        new(ThemePath, null, "테마 · 크기 (설정 서랍 열기)", "jsini-icon-palette", "설정"),
        new(MenuPath, null, "메뉴 단추 (햄버거 — 전체 메뉴 열기)", "jsini-icon-menu", "메뉴"),
        new(ProfilePath, null, "사용자 프로필 아바타 (내 정보 열기)", "jsini-icon-user", "프로필"),
    ];

    /// <summary>환경설정의 고르개에 놓을 것들. 묶음(CATALOG)은 뺀다 — 제 화면이 없어 골라 봐야 준비 중이 뜬다.</summary>
    /// <param name="menus">거른 뒤의 메뉴 트리(IMenuProvider.VisibleMenus).</param>
    public static IReadOnlyList<BottomNavChoice> Choices(IReadOnlyList<MenuNode> menus)
    {
        var choices = new List<BottomNavChoice>(Fixed);

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
                            : node.Icon),
                        node.Title));
                }

                Walk(node.Children, label);
            }
        }
    }

    /// <summary>경로로 정하는 아무 그림 하나. 같은 경로는 늘 같은 그림이다. string.GetHashCode 는 프로세스마다 달라 쓰지 않고 FNV-1a 로 굳힌다.</summary>
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

    /// <summary>박아 둔 아이콘 클래스를 검사한다. 이름 꼴이 아니면 null — 메뉴에서 찾는 길로 떨어진다. 값이 저장소에서 와서 검사하지 않으면 남의 클래스를 켜는 글자가 들어갈 수 있다.</summary>
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

/// <summary>띠의 칸 하나. 글자 열쇠가 짧은 것은(p·t·i·k) 브라우저 저장소에 그대로 들어가 칸 다섯에 이름이 그만큼 붙기 때문이다.</summary>
public sealed record BottomNavItem
{
    /// <summary>갈 곳. 메뉴의 링크 주소이거나 HomePath · ThemePath 다.</summary>
    [JsonPropertyName("p")]
    public string Path { get; init; } = string.Empty;

    /// <summary>띠에 적을 이름. 사람이 정한다 — 메뉴 이름이 길면 72px 칸에서 끊기므로 줄여 적을 수 있어야 한다.</summary>
    [JsonPropertyName("t")]
    public string Title { get; init; } = string.Empty;

    /// <summary>박아 둔 아이콘 클래스. 기본 다섯만 갖는다 — 사람이 고른 칸은 비어 있고 아이콘은 메뉴에서 찾는다.</summary>
    [JsonPropertyName("i")]
    public string? Icon { get; init; }

    /// <summary>가리키는 화면의 열쇠(MenuNode.RouteKey). 경로가 옮겨 가도 이 값은 그대로라 먼저 이것으로 찾는다.</summary>
    [JsonPropertyName("k")]
    public string? RouteKey { get; init; }
}

/// <summary>환경설정의 고르개 한 줄.</summary>
/// <param name="Path">고르면 칸에 적힐 주소.</param>
/// <param name="RouteKey">그 화면의 열쇠. 메뉴가 아닌 것(홈·설정)은 null.</param>
/// <param name="Label">줄기를 붙인 이름 — 「포털관리 › 알림 이력」.</param>
/// <param name="Icon">미리 보여 줄 아이콘 클래스.</param>
/// <param name="Title">고르면 띠에 적힐 이름. Label 과 따로 둔다 — 고르개는 줄기와 설명이 붙고 띠는 72px 라 짧아야 한다.</param>
public sealed record BottomNavChoice(
    string Path, string? RouteKey, string Label, string Icon, string Title);
