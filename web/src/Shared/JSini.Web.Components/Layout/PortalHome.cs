using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

/// <summary>
/// <b>로그인한 뒤 처음 열리는 화면</b>이 어디인가.
///
/// <para>
/// [왜 필요했나]
/// </para>
///
/// <para>
/// 로그인하면 <c>/</c> 가 열리고 그 화면은 <b>일부러 비어 있다</b>(D12 — 무엇을
/// 얹을지 아직 안 정했다). 그래서 업무가 여덟인 포털에서 <b>누가 로그인해도
/// 첫 화면이 빈 판</b>이었고, 하루 종일 한 화면만 보는 사람도 메뉴를 열어
/// 거기까지 내려가는 것으로 하루를 시작했다.
/// </para>
///
/// <para>
/// 고를 수 있게 한다. 고르는 자리는 환경설정이고, 고를 수 있는 것은
/// <b>그 사람이 볼 권한이 있는 메뉴</b>뿐이다(<see cref="Choices"/>).
/// </para>
///
/// <para>
/// [고르지 않았으면 환경설정이 열린다]
/// </para>
///
/// <para>
/// 빈 판보다 낫다 — <b>첫 화면에 「첫 화면을 고르는 곳」을 띄우는 것</b>이라,
/// 이 기능이 있다는 것 자체를 따로 알릴 필요가 없다. 한 번 고르고 나면
/// 다시 안 보인다.
/// </para>
///
/// <para>
/// [계정에 담긴다 — 브라우저가 아니다]
/// </para>
///
/// <para>
/// 아래 띠(<see cref="BottomNav"/>)·토스트 위치와 갈래가 다르다. 저것들은
/// <b>휴대폰에서만</b> 또는 <b>이 화면에서만</b> 뜻이 있어서 브라우저에 남지만
/// (<see cref="PortalBoot"/>), 「내 첫 화면」은 회사 컴퓨터에서 고른 것이
/// 휴대폰에서도 같아야 한다. 담기는 곳은 계정의 <c>HomePath</c> 이고
/// <b>부트스트랩이 이미 내려보내고 있던 값</b>이라 왕복이 늘지 않는다
/// (<see cref="CurrentUser.HomePath"/>).
/// </para>
/// </summary>
public static class PortalHome
{
    /// <summary>
    /// 고르지 않았을 때 열리는 화면 — <b>환경설정</b>. 열쇠로 찾는다.
    /// </summary>
    /// <remarks>
    /// 주소(<c>/funeral/setting/environment</c>)를 박아 두지 않는다.
    /// <c>@page</c> 는 코드가 소유한 값이라 바뀔 수 있고, 그때 여기 사본이
    /// 조용히 어긋나면 <b>로그인한 사람 전부가 「준비 중」을 본다.</b>
    /// 메뉴 트리에서 열쇠로 찾으면 주소는 항상 지금 것이다(web/CLAUDE.md 의
    /// 「연결 고리는 URL 이 아니라 열쇠다」).
    /// </remarks>
    public const string SettingsRouteKey = "funeral.setting.environment";

    /// <summary>
    /// 「고르지 않음」으로 읽는 경로들.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 계정마다 <c>HomePath</c> 가 이미 하나씩 있고 그 값이 <b>전원
    /// <c>/workspace</c></b> 다(2026-09-25 운영 DB 65건 전부). 그것은 사람이
    /// 고른 값이 아니라 vben 템플릿이 딸려 보낸 데모 대시보드의 주소이고,
    /// 계정을 만들 때 서버가 박아 넣는 값이다(<c>SignupService</c>).
    /// </para>
    /// <para>
    /// 그래서 <b>이 셋은 「아직 안 골랐다」로 읽는다.</b> 65줄을 손대는
    /// 마이그레이션보다 낫다 — 셋 다 셸의 빈 홈 하나가 받는 주소라
    /// (<c>Home.razor</c>) 골라 봐야 같은 빈 판이 열리고, 골랐다고 읽으면
    /// 이 기능이 <b>아무에게도 켜지지 않는다.</b>
    /// </para>
    /// </remarks>
    private static readonly string[] Unset = ["/", "/workspace", "/analytics"];

    /// <summary>아직 고르지 않았는가.</summary>
    public static bool IsUnset(string? homePath) =>
        string.IsNullOrWhiteSpace(homePath)
        || Unset.Contains(homePath.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 고르개에 놓을 것들. <b>묶음(CATALOG)·바깥 링크는 뺀다</b> — 앞엣것은
    /// 제 화면이 없어서 골라 봐야 "준비 중" 이 뜨고, 뒤엣것은 새 창으로
    /// 나가는 주소라 첫 화면이 될 수 없다.
    /// </summary>
    /// <param name="menus">거른 뒤의 메뉴 트리(<see cref="IMenuProvider.VisibleMenus"/>).</param>
    /// <remarks>
    /// 이름은 <b>줄기를 붙여</b> 적는다(「포털관리 › 알림 이력」).
    /// <see cref="BottomNav.Choices"/> 와 같은 규칙이다 — 붙이지 않으면
    /// 「목록」·「현황」 같은 이름이 업무마다 있어서 어느 것을 고르는지 알 수 없다.
    /// </remarks>
    public static IReadOnlyList<PortalHomeChoice> Choices(IReadOnlyList<MenuNode> menus)
    {
        var choices = new List<PortalHomeChoice>
        {
            // 맨 위 한 줄이 「지정 안 함」이다. 값이 빈 글자면 DevExpress
            // 고르개가 **아무것도 안 고른 것**으로 그려서 무엇이 켜져 있는지
            // 안 보인다 — 그래서 자리를 차지하는 값 하나를 준다.
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

    /// <summary>
    /// 「지정 안 함」 줄의 값. <b>경로가 아니다</b> — <c>#</c> 로 시작하므로
    /// 메뉴 경로와 절대 겹치지 않는다(<see cref="BottomNav.ThemePath"/> 와
    /// 같은 수법).
    /// </summary>
    public const string NoneValue = "#none";

    /// <summary>
    /// 고른 값을 <b>서버에 담을 글자</b>로 바꾼다. 「지정 안 함」은 빈 글자다.
    /// </summary>
    public static string? ToStored(string? choice) =>
        string.IsNullOrWhiteSpace(choice) || choice == NoneValue ? null : choice;

    /// <summary>
    /// 담아 둔 값을 <b>고르개가 켤 줄</b>로 바꾼다. 안 골랐으면 맨 윗줄이다.
    /// </summary>
    public static string ToChoice(string? homePath) =>
        IsUnset(homePath) ? NoneValue : homePath!.Trim();

    /// <summary>
    /// 지금 이 사람을 실제로 보낼 곳. 보낼 데가 없으면 <c>null</c> 이고,
    /// 그때는 있던 자리(빈 홈)에 그대로 둔다.
    /// </summary>
    /// <param name="homePath">계정에 담긴 값(<see cref="CurrentUser.HomePath"/>).</param>
    /// <param name="menus">거른 뒤의 메뉴 트리(<see cref="IMenuProvider.VisibleMenus"/>).</param>
    /// <remarks>
    /// <para>
    /// <b>담긴 경로를 그대로 쓰지 않는다.</b> 담기는 것은 DB 의 메뉴 경로
    /// (<see cref="MenuNode.Path"/> — <c>/setting/environment</c>)이고 브라우저가
    /// 갈 곳은 링크 주소(<see cref="MenuNode.LinkTarget"/> —
    /// <c>/funeral/setting/environment</c>)다. 둘은 이행이 끝날 때까지 다르다.
    /// </para>
    /// <para>
    /// <b>못 찾으면 환경설정으로 떨어진다.</b> 고른 뒤에 권한이 끊겼거나
    /// 메뉴가 없어진 경우다 — 그대로 보내면 「권한 없음」이 뜨고, 그 화면에는
    /// 다시 고를 길이 없다. 고치는 자리로 보내는 편이 낫다.
    /// </para>
    /// <para>
    /// 환경설정마저 목록에 없으면(그 메뉴의 권한이 없는 계정) <c>null</c> 이다.
    /// 못 여는 화면으로 보내느니 빈 홈이 낫다.
    /// </para>
    /// </remarks>
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

/// <summary>
/// 홈 화면 고르개의 한 줄.
/// </summary>
/// <param name="Path">
/// 담길 값 — DB 의 메뉴 경로(<see cref="MenuNode.Path"/>)다.
/// <b>링크 주소가 아니다</b>: 그쪽은 코드의 <c>@page</c> 를 따라 바뀌지만
/// 이쪽은 권한표·즐겨찾기가 열쇠로 쓰는 값이라 바뀌지 않는다
/// (web/CLAUDE.md 의 표 — 「<c>path</c> 는 DB 소유, 안 바꾼다」).
/// </param>
/// <param name="Label">줄기를 붙인 이름 — 「포털관리 › 알림 이력」.</param>
public sealed record PortalHomeChoice(string Path, string Label);
