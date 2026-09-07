using Microsoft.AspNetCore.Components;
using Xunit;
using JSini.Web.Abstractions;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <see cref="RouteKeyAttribute"/> 규칙을 빌드 때 검사한다.
///
/// [왜 테스트로 막나]
///
/// 열쇠가 빠진 화면은 <b>주소로는 멀쩡히 열린다.</b> 그래서 만든 사람은 아무
/// 이상을 못 느끼고, 나중에 그 화면을 메뉴에 걸려는 사람이 목록에서 못 찾는다.
/// 이 저장소가 크게 밟은 실패("빌드는 통과하는데 화면이 안 열린다")와 같은
/// 종류라 같은 방식으로 막는다.
///
/// 어트리뷰트를 리플렉션으로 본다 — <c>@page</c> 나 <c>@attribute</c> 글자를
/// 읽지 않으므로 어떻게 적든 상관없다.
/// </summary>
public class RouteKeyTests
{
    /// <summary>
    /// 화면 하나에 열쇠가 정확히 하나.
    ///
    /// 포괄 라우트(<c>_Pending.razor</c>)는 제외한다 — 화면이 아니라
    /// "아직 화면이 없다" 는 안내다. 열쇠를 주면 메뉴가 그것을 가리킬 수 있게
    /// 되는데, 그러면 그 메뉴는 영영 「준비 중」만 띄운다.
    /// </summary>
    [Fact]
    public void 모든_화면에_RouteKey_가_있다()
    {
        var missing = new List<string>();

        foreach (var (descriptor, type, _) in RoutablePages())
        {
            var keys = type.GetCustomAttributes(typeof(RouteKeyAttribute), false);

            if (keys.Length != 1)
            {
                missing.Add($"{descriptor.Key}: {type.Name} 의 RouteKey 가 {keys.Length}개 — "
                            + "하나여야 한다. @page 아래에 "
                            + "@attribute [RouteKey(\"...\")] 를 적는다");
            }
        }

        Assert.True(missing.Count == 0, string.Join(" / ", missing));
    }

    /// <summary>
    /// 열쇠는 저장소 전체에서 유일하다.
    ///
    /// 겹치면 <c>RouteInventory</c> 의 사전에서 <b>먼저 훑힌 쪽이 이긴다</b>.
    /// 어느 쪽이 먼저인지는 어셈블리 순서에 달려 있어 사실상 무작위고,
    /// 그래서 그 메뉴가 엉뚱한 화면을 여는 것으로 나타난다.
    /// </summary>
    [Fact]
    public void RouteKey_는_유일하다()
    {
        var duplicates = RoutablePages()
            .Select(p => (p.Descriptor, p.Type, Key: KeyOf(p.Type)))
            .Where(p => p.Key is not null)
            .GroupBy(p => p.Key!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' 를 {g.Count()}개가 쓴다 "
                         + $"({string.Join(", ", g.Select(x => $"{x.Descriptor.Key}/{x.Type.Name}"))})")
            .ToList();

        Assert.True(duplicates.Count == 0, string.Join(" / ", duplicates));
    }

    /// <summary>
    /// 열쇠는 자기 모듈 이름으로 시작한다 (<c>funeral.</c>).
    ///
    /// 접두사 규칙이 <c>@page</c> 에 이미 있는 것과 같은 이유다 — 열쇠만 보고
    /// 어느 업무 소관인지 알 수 있어야 메뉴 관리 화면이 업무별로 묶어 보여 줄 수
    /// 있고, 겹칠 위험도 모듈 안으로 갇힌다.
    /// </summary>
    [Fact]
    public void RouteKey_는_모듈_이름으로_시작한다()
    {
        var offenders = new List<string>();

        foreach (var (descriptor, type, _) in RoutablePages())
        {
            var key = KeyOf(type);
            if (key is null)
            {
                continue;   // 빠뜨린 것은 위 테스트가 잡는다
            }

            if (!key.StartsWith(descriptor.Key + ".", StringComparison.OrdinalIgnoreCase)
                && !key.Equals(descriptor.Key, StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{descriptor.Key}: {type.Name} 의 RouteKey '{key}' 는 "
                              + $"'{descriptor.Key}.' 로 시작해야 한다");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(" / ", offenders));
    }

    /// <summary>
    /// 포괄 라우트에는 열쇠를 붙이지 않는다.
    /// </summary>
    [Fact]
    public void 포괄_라우트에는_RouteKey_가_없다()
    {
        var offenders = new List<string>();

        foreach (var descriptor in PortalApps.Descriptors)
        {
            foreach (var type in descriptor.Assembly.GetTypes())
            {
                var isCatchAll = type.GetCustomAttributes(typeof(RouteAttribute), false)
                    .Cast<RouteAttribute>()
                    .Any(r => r.Template.Contains("{*", StringComparison.Ordinal));

                if (isCatchAll && KeyOf(type) is not null)
                {
                    offenders.Add($"{descriptor.Key}: 포괄 라우트 {type.Name} 에 "
                                  + "RouteKey 가 붙어 있다 — 메뉴가 이것을 가리키면 "
                                  + "영영 「준비 중」만 뜬다");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(" / ", offenders));
    }

    /// <summary>
    /// 실제 화면(포괄 라우트가 아닌 것)만 골라 낸다.
    /// </summary>
    private static IEnumerable<(IPortalModule Descriptor, Type Type, RouteAttribute[] Routes)> RoutablePages()
    {
        foreach (var descriptor in PortalApps.Descriptors)
        {
            foreach (var type in descriptor.Assembly.GetTypes())
            {
                var routes = type.GetCustomAttributes(typeof(RouteAttribute), false)
                    .Cast<RouteAttribute>()
                    .ToArray();

                if (routes.Length == 0
                    || routes.Any(r => r.Template.Contains("{*", StringComparison.Ordinal)))
                {
                    continue;
                }

                yield return (descriptor, type, routes);
            }
        }
    }

    private static string? KeyOf(Type type) => type
        .GetCustomAttributes(typeof(RouteKeyAttribute), false)
        .Cast<RouteKeyAttribute>()
        .FirstOrDefault()?.Key;
}
