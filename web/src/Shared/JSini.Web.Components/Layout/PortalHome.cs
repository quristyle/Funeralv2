using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

/// <summary>로그인한 뒤 처음 열리는 화면을 고르게 한다 — 업무가 여덟인 포털에서 첫 화면이 빈 판이면 하루 종일 한 화면만 보는 사람도 메뉴를 열어 내려간다. 고르는 자리는 환경설정이고 고를 수 있는 것은 볼 권한이 있는 메뉴뿐이다. 값은 계정에 담는다 — 회사 컴퓨터에서 고른 것이 휴대폰에서도 같아야 하고, 부트스트랩이 이미 내려보내는 값이라 왕복이 늘지 않는다.</summary>
public static class PortalHome
{
    /// <summary>고르지 않았을 때 열리는 화면 — 환경설정. 열쇠로 찾는다. 주소를 박아 두면 @page 가 바뀔 때 사본이 어긋나 로그인한 사람 전부가 준비 중을 본다.</summary>
    public const string SettingsRouteKey = "funeral.setting.environment";

    /// <summary>「고르지 않음」으로 읽는 경로들. 계정마다 HomePath 가 이미 있고 전원 /workspace 다(vben 템플릿의 데모 주소를 계정 생성 때 서버가 박은 값) — 사람이 고른 값이 아니므로 이 셋은 아직 안 고른 것으로 읽는다. 65줄을 고치는 마이그레이션보다 낫다. 골랐다고 읽으면 이 기능이 아무에게도 켜지지 않는다.</summary>
    private static readonly string[] Unset = ["/", "/workspace", "/analytics"];

    /// <summary>아직 고르지 않았는가.</summary>
    public static bool IsUnset(string? homePath) =>
        string.IsNullOrWhiteSpace(homePath)
        || Unset.Contains(homePath.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>고르개에 놓을 것들. 묶음(CATALOG)과 바깥 링크는 뺀다 — 묶음은 제 화면이 없고 바깥 링크는 새 창이라 첫 화면이 될 수 없다.</summary>
    /// <param name="menus">거른 뒤의 메뉴 트리(IMenuProvider.VisibleMenus).</param>
    /// <remarks>이름은 줄기를 붙여 적는다(「포털관리 › 알림 이력」) — 안 붙이면 「목록」·「현황」이 업무마다 있어 고를 수 없다. BottomNav.Choices 와 같은 규칙이다.</remarks>
    public static IReadOnlyList<PortalHomeChoice> Choices(IReadOnlyList<MenuNode> menus)
    {
        var choices = new List<PortalHomeChoice>
        {
            // 맨 위 한 줄은 지정 안 함이다 — 빈 글자 값이면 DevExpress 고르개가 아무것도 안 고른 것으로 그려 무엇이 켜졌는지 안 보인다.
            new(NoneValue, "지정 안 함 (환경설정 화면이 열립니다)"),
        };

        Walk(menus, null);

        return choices;

        void Walk(IReadOnlyList<MenuNode> nodes, string? trail)
        {
            foreach (var node in nodes)
            {
                var label = trail is null ? node.Title : $"{trail} › {node.Title}";

                if (!node.IsCatalog && !node.IsExternalLink && !IsUnset(node.Path))
                {
                    choices.Add(new PortalHomeChoice(node.Path, label));
                }

                Walk(node.Children, label);
            }
        }
    }

    /// <summary>「지정 안 함」 줄의 값. 경로가 아니다 — # 로 시작해 메뉴 경로와 겹치지 않는다(BottomNav.ThemePath 와 같은 수법).</summary>
    public const string NoneValue = "#none";

    /// <summary>고른 값을 서버에 담을 글자로 바꾼다. 지정 안 함은 빈 글자다.</summary>
    public static string? ToStored(string? choice) =>
        string.IsNullOrWhiteSpace(choice) || choice == NoneValue ? null : choice;

    /// <summary>담아 둔 값을 고르개가 켤 줄로 바꾼다. 안 골랐으면 맨 윗줄이다.</summary>
    public static string ToChoice(string? homePath) =>
        IsUnset(homePath) ? NoneValue : homePath!.Trim();

    /// <summary>지금 이 사람을 실제로 보낼 곳. 보낼 데가 없으면 null — 그때는 빈 홈에 그대로 둔다. 담긴 경로를 그대로 쓰지 않는다: 담기는 것은 DB 메뉴 경로이고 브라우저가 갈 곳은 링크 주소라 이행이 끝날 때까지 다르다. 못 찾으면 환경설정으로 떨어진다(고른 뒤 권한이 끊긴 경우 — 권한 없음 화면에는 다시 고를 길이 없다). 환경설정마저 없으면 못 여는 곳으로 보내느니 빈 홈이 낫다.</summary>
    /// <param name="homePath">계정에 담긴 값(CurrentUser.HomePath).</param>
    /// <param name="menus">거른 뒤의 메뉴 트리(IMenuProvider.VisibleMenus).</param>
    public static string? Resolve(string? homePath, IReadOnlyList<MenuNode> menus)
    {
        if (!IsUnset(homePath) && Find(menus, homePath!.Trim()) is { } picked)
        {
            return IsUnset(picked) ? null : picked;
        }

        var settings = FindByKey(menus, SettingsRouteKey);

        return IsUnset(settings) ? null : settings;
    }

    /// <summary>이 경로를 가리키는 메뉴의 링크 주소. 못 찾으면 <c>null</c>.</summary>
    private static string? Find(IReadOnlyList<MenuNode> menus, string path) =>
        FindBy(menus, node =>
            !node.IsCatalog
            && (Same(node.Path, path) || Same(node.LinkTarget, path)))?.LinkTarget;

    private static string? FindByKey(IReadOnlyList<MenuNode> menus, string routeKey) =>
        FindBy(menus, node => Same(node.RouteKey, routeKey))?.LinkTarget;

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

/// <summary>홈 화면 고르개의 한 줄.</summary>
/// <param name="Path">담길 값 — DB 의 메뉴 경로다. 링크 주소가 아니다: 그쪽은 @page 를 따라 바뀌지만 이쪽은 권한표·즐겨찾기가 열쇠로 쓰는 값이라 바뀌지 않는다.</param>
/// <param name="Label">줄기를 붙인 이름 — 「포털관리 › 알림 이력」.</param>
public sealed record PortalHomeChoice(string Path, string Label);
