using System.Reflection;
using JSini.Web.Abstractions;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Menu;

/// <summary>실려 있는 화면 하나. 열쇠와 주소를 짝지어 들고 있다.</summary>
/// <param name="Key">RouteKeyAttribute 로 화면이 신고한 열쇠(funeral.room-status).</param>
/// <param name="Path">브라우저에 보이는 전체 경로(/funeral/room-status). 매개변수 자리({id})는 그대로 둔다.</param>
/// <param name="Module">열쇠의 첫 마디로 본 소속 모듈(funeral). 메뉴 관리 화면이 목록을 업무별로 묶는 데 쓴다.</param>
public sealed record RouteEntry(string Key, string Path, string Module);

/// <summary>이 앱이 가진 화면을 모아 둔다. 기동 때 한 번 만들고 그 뒤로는 읽기만 한다. 라우트가 컴파일 시점에 고정이라 가능해진 일이다.
/// Paths 는 주소만이라 메뉴와 라우트가 어긋났는지 대조하는 데 쓰고, Catalog 는 열쇠에서 주소로 가는 길이라 DB 메뉴가 화면을 찾는 길이다. DB 는 URL 을 모르고 열쇠만 들고 있어 사전 조회 한 번으로 링크를 푼다 — DB 를 타지 않는다.</summary>
public sealed class RouteInventory
{
    private readonly Dictionary<string, RouteEntry> _byKey;

    private RouteInventory(IReadOnlySet<string> paths, Dictionary<string, RouteEntry> byKey)
    {
        Paths = paths;
        _byKey = byKey;
    }

    /// <summary>이 앱의 라우트를 브라우저에 보이는 전체 경로로 담는다(/projmng/status). 매개변수 자리는 그대로 둔다. 접두사가 없는 상대 @page 구성은 여기서 접두사를 붙여 맞춘다.</summary>
    public IReadOnlySet<string> Paths { get; }

    /// <summary>열쇠로 찾는 화면 목록. 열쇠는 대소문자를 가리지 않는다.</summary>
    public IReadOnlyDictionary<string, RouteEntry> Catalog => _byKey;

    /// <summary>
    /// 메뉴 관리 화면이 고르게 내놓을 목록. 업무별로 묶어 열쇠 순으로.
    /// </summary>
    public IReadOnlyList<RouteEntry> Entries { get; private set; } = [];

    /// <summary>열쇠를 주소로 푼다. 모르는 열쇠면 null — 열쇠를 아직 안 채운 DB 메뉴는 정상적으로 null 이 나오므로 부르는 쪽이 옛 방식(path)으로 떨어진다. 이행이 끝나면 null 이 없어야 한다.</summary>
    public string? Resolve(string? routeKey) =>
        !string.IsNullOrWhiteSpace(routeKey) && _byKey.TryGetValue(routeKey, out var entry)
            ? entry.Path
            : null;

    /// <summary>어셈블리를 훑어 @page 와 RouteKeyAttribute 를 모은다.</summary>
    /// <param name="routePrefix">이 앱의 접두사(/projmng). 셸은 빈 문자열.</param>
    /// <param name="assemblies">라우트를 담고 있는 어셈블리들.</param>
    public static RouteInventory Build(string routePrefix, params Assembly[] assemblies)
    {
        var prefix = routePrefix.TrimEnd('/');
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var byKey = new Dictionary<string, RouteEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var templates = type
                    .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                    .Cast<RouteAttribute>()
                    .Select(r => r.Template)
                    // 포괄 라우트(/funeral/{*rest})는 세지 않는다 — 화면이 아니라 아직 화면이 없다는 안내라서 세면 그 업무의 모든 메뉴가 화면이 있는 것으로 보인다.
                    .Where(t => !t.Contains("{*", StringComparison.Ordinal))
                    .Select(t => Combine(prefix, t))
                    .ToList();

                if (templates.Count == 0)
                {
                    continue;
                }

                foreach (var path in templates)
                {
                    paths.Add(path);
                }

                var key = type
                    .GetCustomAttributes(typeof(RouteKeyAttribute), inherit: false)
                    .Cast<RouteKeyAttribute>()
                    .FirstOrDefault()?.Key;

                if (string.IsNullOrWhiteSpace(key))
                {
                    // 여기서 던지지 않는다. 열쇠가 빠진 화면은 주소로는 멀쩡히
                    // 열리므로 기동을 세울 일이 아니고, 빠뜨림은 아키텍처
                    // 테스트가 빌드 때 잡는다(RouteKeyTests).
                    continue;
                }

                // @page 가 둘 이상이면 **첫째가 정본**이다. 나머지는 옛 주소를
                // 받아 주는 별칭이고, 메뉴가 걸 링크는 정본이어야 한다.
                byKey.TryAdd(key, new RouteEntry(key, templates[0], ModuleOf(key)));
            }
        }

        return new RouteInventory(paths, byKey)
        {
            Entries = [.. byKey.Values.OrderBy(e => e.Module, StringComparer.Ordinal)
                                      .ThenBy(e => e.Key, StringComparer.Ordinal)],
        };
    }

    /// <summary>열쇠의 첫 마디가 모듈이다 (<c>funeral.building.room</c> → <c>funeral</c>).</summary>
    private static string ModuleOf(string key)
    {
        var dot = key.IndexOf('.');
        return dot > 0 ? key[..dot] : key;
    }

    /// <summary>접두사와 @page 를 이어 붙인다. 앱의 첫 화면은 @page "/" 이고 전체 경로는 /projmng 다 — /projmng/ 로 끝에 슬래시가 남으면 DB path(/projmng)와 안 맞아 메뉴는 있는데 화면이 없다고 잘못 보고된다.</summary>
    private static string Combine(string prefix, string template)
    {
        if (template is "/" or "")
        {
            return prefix.Length > 0 ? prefix : "/";
        }

        return prefix + (template.StartsWith('/') ? template : "/" + template);
    }
}
