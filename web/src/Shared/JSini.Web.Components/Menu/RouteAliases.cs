namespace JSini.Web.Components.Menu;

/// <summary>
/// DB 메뉴의 <b>옛 경로</b>를 Blazor 의 정규 경로로 옮긴다.
///
/// [왜 필요한가]
///
/// Vue 포털에서는 업무마다 경로 접두사가 없어도 됐다. 라우트를 DB 의
/// <c>component</c> 컬럼이 만들었으니 <c>/room_status</c> 하나로 충분했다.
/// Blazor 는 모든 모듈이 한 라우터에 들어가므로 <b>업무 접두사가 있어야</b>
/// 어느 모듈 소관인지 URL 만 보고 알 수 있다(<c>/funeral/room-status</c>).
///
/// 그래서 DB 의 <c>path</c> 69건이 Blazor 라우트와 어긋난다. 헬프데스크·
/// 프로젝트관리·생활과환경은 원래 접두사가 있어서 그대로고, 장례식장·포털관리·
/// 소개사이트만 어긋난다.
///
/// [DB 를 고치지 않고 코드로 흡수하는 이유]
///
/// 고치는 SQL 은 준비돼 있다(<c>web/docs/menu-path-cutover.sql</c>). 다만
/// <b>운영 DB 를 한 번 바꾸면 되돌리는 동안 메뉴가 깨진다</b>. 코드가 양쪽을
/// 다 받아 주면 그 순서 제약이 사라진다 — 배포를 먼저 하든 SQL 을 먼저 돌리든
/// 상관없고, 되돌릴 때도 마찬가지다.
///
/// SQL 을 돌린 뒤에도 이 표는 그대로 두면 된다. 새 경로는 여기 열쇠에 없으므로
/// 그냥 통과한다(멱등). 이행이 완전히 끝나면 표째로 지운다.
///
/// [경로만 옮기고 <c>MenuNode.Path</c> 는 건드리지 않는다]
///
/// 권한표(<c>/auth/menu/permissions</c>)와 즐겨찾기의 열쇠는 여전히 DB 의
/// <c>path</c> 다. 그것까지 바꿔 버리면 권한이 조용히 안 걸린다 —
/// "메뉴는 보이는데 열면 403" 이 아니라 그 반대, <b>권한이 없는데 보인다</b>
/// 쪽으로 틀리므로 특히 위험하다. 그래서 링크 주소(<c>Href</c>)만 옮긴다.
/// </summary>
public static class RouteAliases
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── 장례식장 ────────────────────────────────────────────
        // 대부분 접두사만 붙으면 되지만, 다섯 개는 이름도 바뀐다.
        // 규칙으로 자동 처리하지 않고 전부 적는 이유는, 규칙에서 새는 것이
        // 생겼을 때 "왜 이 메뉴만 안 열리지" 로 나타나기 때문이다.
        ["/funerals"] = "/funeral",
        ["/building"] = "/funeral/building",
        ["/building/info"] = "/funeral/building/info",
        ["/building/music-build"] = "/funeral/building/music-build",
        ["/building/floor"] = "/funeral/building/floor",
        ["/building/room"] = "/funeral/building/room",
        ["/building/device"] = "/funeral/building/device",
        ["/building/video"] = "/funeral/building/video",
        ["/building/audio"] = "/funeral/building/audio",
        ["/building/source"] = "/funeral/building/source",
        ["/device/background"] = "/funeral/building/background",
        ["/decoration"] = "/funeral/building/decoration",
        ["/room_status"] = "/funeral/room-status",
        ["/deceased"] = "/funeral/deceased-group",
        ["/building/deceased"] = "/funeral/deceased",
        ["/info"] = "/funeral/info",
        ["/info/room-history"] = "/funeral/info/room-history",
        ["/info/deceased-search"] = "/funeral/info/deceased-search",
        ["/info/my-info"] = "/funeral/info/my-info",
        ["/info/preview"] = "/funeral/info/preview",
        ["/stat"] = "/funeral/stat",
        ["/stat/billing"] = "/funeral/stat/billing",
        ["/stat/room-usage"] = "/funeral/stat/room-usage",
        ["/status"] = "/funeral/status",
        ["/status/funeral-info"] = "/funeral/status/funeral-info",
        ["/status/funeral-status"] = "/funeral/status/funeral-status",
        ["/status/deceased-status"] = "/funeral/status/deceased-status",
        ["/status/simple"] = "/funeral/status/simple",
        ["/status/mobile"] = "/funeral/status/mobile",
        ["/help"] = "/funeral/help",
        ["/help/qna"] = "/funeral/help/qna",
        ["/help/faq"] = "/funeral/help/faq",
        ["/help/archive"] = "/funeral/help/archive",
        ["/setting"] = "/funeral/setting",
        ["/setting/environment"] = "/funeral/setting/environment",
        ["/setting/work-options"] = "/funeral/setting/work-options",
        ["/system/player-download"] = "/funeral/player-download",

        // ── 포털관리 ────────────────────────────────────────────
        // 옛 경로가 /system 아래에 잡다하게 섞여 있었다. 새 경로는
        // system(설정) · auth(권한) · company(조직) · push(알림) · status(상태)
        // 다섯으로 갈랐다.
        ["/system"] = "/admin",
        ["/common"] = "/admin/common",
        ["/system/common-code"] = "/admin/system/common-code",
        ["/system/metadata_manager"] = "/admin/system/metadata",
        ["/system/i18n"] = "/admin/system/i18n",
        ["/system/account"] = "/admin/system/account",
        ["/system/menu"] = "/admin/system/menu",
        ["/portal/notice"] = "/admin/notice",
        ["/portal/release"] = "/admin/release",
        ["/auth"] = "/admin/auth",
        ["/system/role-map"] = "/admin/auth/role",
        ["/auth/user-role"] = "/admin/auth/user-role",
        ["/auth/menu-role"] = "/admin/auth/menu-role",
        ["/company"] = "/admin/company",
        ["/company/org-chart"] = "/admin/company/org-chart",
        ["/system/company"] = "/admin/company/list",
        ["/system/dept"] = "/admin/company/dept",
        ["/company/user"] = "/admin/company/user",
        ["/system/push"] = "/admin/push",
        ["/system/push/dashboard"] = "/admin/push/dashboard",
        ["/system/push/logs"] = "/admin/push/logs",
        ["/system/push/history"] = "/admin/push/history",
        ["/system/push/setting"] = "/admin/push/setting",
        ["/system/status"] = "/admin/status",
        ["/system/server-status"] = "/admin/status/server",
        ["/system/server-status/jin114"] = "/admin/status/jin114",
        ["/system/deploy-status"] = "/admin/status/deploy",
        ["/system/player-release"] = "/admin/status/player-release",
        ["/profile"] = "/admin/profile",

        // ── 소개사이트·AI ───────────────────────────────────────
        ["/devs"] = "/site",
        ["/ai/chat"] = "/site/ai/chat",
        ["/company/site-inquiries"] = "/site/inquiries",
    };

    /// <summary>표에 담긴 DB 메뉴 경로 전부. 검사용이다.</summary>
    public static IEnumerable<string> MenuPaths => Map.Keys;

    /// <summary>
    /// DB 경로를 Blazor 라우트로 옮긴다. 표에 없으면 그대로 돌려준다.
    ///
    /// vben 표기의 매개변수(<c>:id</c>)는 Blazor 표기(<c>{id}</c>)로 바꾼다.
    /// 링크로 쓸 값이 아니라 대조용이지만, 안 바꾸면 매개변수 화면 두 개가
    /// 늘 "메뉴에는 있는데 화면이 없다" 로 잘못 보고된다.
    /// </summary>
    public static string Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var resolved = Map.TryGetValue(path, out var mapped) ? mapped : path;

        return resolved.Contains("/:", StringComparison.Ordinal)
            ? ToBlazorParameters(resolved)
            : resolved;
    }

    /// <summary>
    /// <see cref="Resolve"/> 의 <b>반대</b> — Blazor 라우트를 DB 메뉴 경로로 되돌린다.
    ///
    /// <para>
    /// [왜 필요한가 — 화면 안 단추가 전부 사라져 있었다]
    /// </para>
    ///
    /// <para>
    /// 권한표의 열쇠는 DB 의 <c>path</c> 다(<c>/system/account</c>). 그런데
    /// <c>PermissionView</c> 는 <b>지금 열려 있는 주소</b>로 묻는다
    /// (<c>/admin/system/account</c>). 둘이 다르니 찾지 못하고, 찾지 못하면
    /// 「권한 없음」이라 등록·수정·삭제·엑셀 단추가 <b>말없이 전부</b> 사라진다.
    /// </para>
    ///
    /// <para>
    /// 사이드바는 멀쩡했다 — 그쪽은 <c>MenuNode.Path</c>(= DB 경로)로 묻기
    /// 때문이다. 그래서 「메뉴는 보이는데 화면 안 단추만 없다」로 나타났고,
    /// 옛 경로를 쓰는 69개 화면이 다 그랬다.
    /// </para>
    ///
    /// <para>
    /// 표에 없으면 그대로 돌려준다. 이미 DB 경로이거나(헬프데스크·프로젝트관리),
    /// 컷오버 SQL 을 돌린 뒤라면 옮길 것이 없다(멱등).
    /// </para>
    /// </summary>
    public static string ToMenuPath(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return string.Empty;
        }

        return Reverse.TryGetValue(route, out var menuPath) ? menuPath : route;
    }

    /// <summary>
    /// <see cref="Map"/> 을 뒤집은 것. 같은 라우트로 가는 옛 경로가 둘 이상이면
    /// <b>먼저 적힌 것</b>을 쓴다 — 뒤엣것으로 덮으면 표의 줄 순서를 바꿨을 뿐인데
    /// 권한이 달라진다.
    /// </summary>
    private static readonly Dictionary<string, string> Reverse = BuildReverse();

    private static Dictionary<string, string> BuildReverse()
    {
        var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (menuPath, route) in Map)
        {
            reverse.TryAdd(route, menuPath);
        }

        return reverse;
    }

    private static string ToBlazorParameters(string path) =>
        string.Join('/', path.Split('/').Select(segment =>
            segment.StartsWith(':') ? $"{{{segment[1..]}}}" : segment));
}
