namespace JSini.Web.Components.Menu;

/// <summary>DB 메뉴의 옛 경로를 Blazor 의 정규 경로로 옮긴다. Vue 는 경로 접두사가 없었지만 Blazor 는 모듈이 한 라우터에 들어가 업무 접두사가 있어야 소관을 URL 로 안다. DB 의 path 69건이 어긋나 있는데 DB 를 고치지 않고 코드로 흡수한다 — 운영 DB 를 바꾸면 되돌리는 동안 메뉴가 깨지고, 코드가 양쪽을 받으면 SQL 과 배포의 순서 제약이 사라진다.
/// 경로만 옮기고 MenuNode.Path 는 건드리지 않는다 — 권한표와 즐겨찾기의 열쇠가 DB 의 path 라 바꾸면 권한이 조용히 안 걸린다(권한이 없는데 보이는 쪽으로 틀린다). 이행이 끝나면 표째로 지운다.</summary>
public static class RouteAliases
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // 대부분 접두사만 붙으면 되지만 다섯은 이름도 바뀐다. 규칙으로 처리하지 않고 전부 적는 것은 규칙에서 새는 것이 생겼을 때 왜 이 메뉴만 안 열리는지 알게 하려서다.
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

        // 옛 경로가 /system 아래에 잡다하게 섞여 있어 새 경로는 system(설정) · auth(권한) · company(조직) · push(알림) · status(상태) 다섯으로 갈랐다.
        ["/system"] = "/admin",
        ["/common"] = "/admin/common",
        ["/system/common-code"] = "/admin/system/common-code",
        ["/system/metadata_manager"] = "/admin/system/metadata",
        ["/system/i18n"] = "/admin/system/i18n",
        ["/system/account"] = "/admin/system/account",
        ["/system/menu"] = "/admin/system/menu",
        ["/portal/notice"] = "/admin/notice",
        ["/auth"] = "/admin/auth",
        // 유형이 EMBEDDED 라 앞으로 가는 길로는 이 줄을 안 탄다(MenuProvider.HrefOf 가 먼저 /embed 로 보낸다). 남겨 두는 것은 띄워 둔 탭 때문이다 — MigrationPending 이 이 표를 거꾸로 읽어 그 주소를 새 자리로 보낸다.
        ["/system/server-status/jin114"] = "/admin/status/jin114",

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
        // 이 줄이 가리키는 화면은 장례식장 환경설정과 같은 판이라 그쪽 하나로 합쳤다. 줄을 남기는 것은 띄워 둔 탭 때문이다 — MigrationPending.Moved 가 거기서 환경설정으로 보낸다. DB 메뉴는 함께 지웠으므로 앞으로 가는 길로는 이 줄을 타지 않는다.
        ["/system/push/setting"] = "/admin/push/setting",
        ["/system/status"] = "/admin/status",
        ["/system/server-status"] = "/admin/status/server",
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

    /// <summary>DB 경로를 Blazor 라우트로 옮긴다. 표에 없으면 그대로 돌려준다. vben 표기의 매개변수(:id)는 Blazor 표기({id})로 바꾼다 — 대조용이지만 안 바꾸면 매개변수 화면 둘이 늘 「메뉴에는 있는데 화면이 없다」로 잘못 보고된다.</summary>
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

    /// <summary>Resolve 의 반대 — Blazor 라우트를 DB 메뉴 경로로 되돌린다. 권한표의 열쇠는 DB 의 path 인데 PermissionView 는 지금 열린 주소로 묻는다 — 둘이 다르니 찾지 못해 등록·수정·삭제·엑셀 단추가 말없이 전부 사라졌다. 사이드바는 MenuNode.Path 로 묻어서 멀쩡했다. 표에 없으면 그대로 돌려준다 — 이미 DB 경로이거나 컷오버 SQL 을 돌린 뒤라 옮길 것이 없다(멱등).</summary>
    public static string ToMenuPath(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return string.Empty;
        }

        return Reverse.TryGetValue(route, out var menuPath) ? menuPath : route;
    }

    /// <summary>Map 을 뒤집은 것. 같은 라우트로 가는 옛 경로가 둘 이상이면 먼저 적힌 것을 쓴다 — 뒤엣것으로 덮으면 줄 순서를 바꿨을 뿐인데 권한이 달라진다.</summary>
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
