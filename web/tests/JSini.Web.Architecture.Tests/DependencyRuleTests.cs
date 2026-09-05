using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// MFE 구조를 지탱하는 규칙.
///
/// 업무 앱이 각자 독립 프로세스가 되면서 규칙이 하나 늘었다 — <b>셸의 설정과
/// 앱의 선언이 일치하는가</b>. 컴파일러가 잡아 주던 것을 프로세스 경계가
/// 가져가 버렸기 때문이다. 이제 어긋나도 빌드는 통과하고, 운영에서 그 업무만
/// 404 가 난다.
/// </summary>
public sealed class DependencyRuleTests
{
    /// <summary>
    /// 규칙 1. 업무 앱은 셸을 참조하지 않는다.
    ///
    /// 참조하는 순간 의존이 양방향이 되고, 셸을 고칠 때마다 앱 여섯 개를 다시
    /// 내보내야 한다. 배포를 나눈 의미가 사라진다. 앱이 아는 셸은
    /// JSini.Web.Abstractions 의 계약뿐이다.
    /// </summary>
    [Fact]
    public void 업무앱은_셸을_참조하지_않는다()
    {
        var offenders = PortalApps.Assemblies
            .Where(a => a.GetReferencedAssemblies()
                .Any(r => r.Name == PortalApps.ShellAssembly))
            .Select(a => a.GetName().Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"셸을 참조하는 앱: {string.Join(", ", offenders)}. "
            + "필요한 것이 있으면 JSini.Web.Abstractions 의 계약으로 뽑아라.");
    }

    /// <summary>
    /// 규칙 2. 업무 앱은 다른 업무 앱을 참조하지 않는다.
    ///
    /// 별도 프로세스라 런타임에 부를 수도 없지만, 빌드 참조가 생기면 배포가
    /// 묶인다 — 헬프데스크를 고칠 때 장례식장도 함께 내보내야 한다.
    /// 공유가 필요하면 Blazor Common 이나 Shared Models 로 올린다.
    /// </summary>
    [Fact]
    public void 업무앱은_다른_업무앱을_참조하지_않는다()
    {
        var names = PortalApps.Assemblies.Select(a => a.GetName().Name!)
            .ToHashSet(StringComparer.Ordinal);

        var offenders = new List<string>();

        foreach (var app in PortalApps.Assemblies)
        {
            var self = app.GetName().Name!;
            var bad = app.GetReferencedAssemblies()
                .Select(r => r.Name!)
                .Where(n => n != self && names.Contains(n))
                .ToList();

            if (bad.Count > 0)
            {
                offenders.Add($"{self} → {string.Join(", ", bad)}");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(" / ", offenders));
    }

    /// <summary>
    /// 규칙 3. <b><c>@page</c> 에 자기 접두사를 적지 않는다.</b>
    ///
    /// 독립 앱이 되면서 규칙이 뒤집혔다. RCL 이던 시절에는 모두 한 라우터에
    /// 들어가서 <c>@page "/funeral/status"</c> 라고 적어야 했지만, 지금은
    /// <c>UsePathBase("/funeral")</c> 가 접두사를 <b>떼고</b> 넘긴다.
    /// 여기에 접두사를 또 적으면 실제 주소가 <c>/funeral/funeral/status</c> 가
    /// 되어 아무도 열 수 없는 화면이 된다.
    ///
    /// 빌드는 통과하고, 그 화면만 조용히 404 다. 화면을 250개 옮기는 동안
    /// 반드시 몇 번은 이렇게 적게 되므로 테스트로 막는다.
    /// </summary>
    [Fact]
    public void 앱의_라우트에_자기_접두사를_중복해서_적지_않는다()
    {
        var offenders = new List<string>();

        foreach (var descriptor in PortalApps.Descriptors)
        {
            foreach (var type in descriptor.Assembly.GetTypes())
            {
                foreach (var route in type.GetCustomAttributes(typeof(RouteAttribute), false)
                             .Cast<RouteAttribute>())
                {
                    if (route.Template.StartsWith(descriptor.RoutePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        offenders.Add(
                            $"{descriptor.Key}: @page \"{route.Template}\" 는 "
                            + $"{descriptor.RoutePrefix}{route.Template} 가 된다. "
                            + "접두사를 빼라 (UsePathBase 가 붙여 준다).");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(" / ", offenders));
    }

    /// <summary>
    /// 규칙 4. Blazor Common 은 어떤 업무 앱도 참조하지 않는다.
    ///
    /// 참조하는 순간 모든 앱이 그 앱에 간접 의존한다 — 가장 알아채기 어려운
    /// 형태의 결합이다. 빌드는 통과하고, 문제는 "왜 헬프데스크를 고쳤는데
    /// 장례식장이 깨지지" 로 나타난다.
    /// </summary>
    [Fact]
    public void 공통라이브러리는_업무앱을_참조하지_않는다()
    {
        var names = PortalApps.Assemblies.Select(a => a.GetName().Name!)
            .ToHashSet(StringComparer.Ordinal);

        var shared = typeof(JSini.Web.Components.JSiniWebApp).Assembly;

        var bad = shared.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(names.Contains)
            .ToList();

        Assert.True(bad.Count == 0,
            $"JSini.Web.Components 가 업무 앱을 참조한다: {string.Join(", ", bad)}");
    }

    /// <summary>
    /// 규칙 5. 앱 식별자와 라우트 접두사는 겹치지 않는다.
    ///
    /// 겹치면 셸의 YARP 라우트가 겹쳐서 어느 앱이 그 URL 을 가져갈지 순서에
    /// 달리게 된다.
    /// </summary>
    [Fact]
    public void 앱_식별자와_접두사는_겹치지_않는다()
    {
        Assert.Empty(PortalApps.Descriptors
            .GroupBy(d => d.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key));

        Assert.Empty(PortalApps.Descriptors
            .GroupBy(d => d.RoutePrefix, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key));
    }

    /// <summary>
    /// 규칙 6. <b>셸의 PortalApps 설정과 앱의 선언이 일치한다.</b>
    ///
    /// 프로세스가 갈라지면서 새로 생긴 규칙이자, 가장 흔할 사고다.
    /// 셸은 appsettings 의 <c>RoutePrefix</c> 로 넘기고 앱은
    /// <c>IPortalModule.RoutePrefix</c> 로 <c>UsePathBase</c> 를 잡는다.
    /// 둘이 어긋나면 셸은 넘기는데 앱이 "내 경로가 아니다" 며 404 를 낸다 —
    /// 빌드도 통과하고 각 파일만 보면 둘 다 정상으로 보인다.
    /// </summary>
    [Fact]
    public void 셸_설정과_앱_선언이_일치한다()
    {
        Assert.NotEmpty(PortalApps.ShellRegistry);

        var declared = PortalApps.Descriptors.ToDictionary(
            d => d.Key, d => d.RoutePrefix, StringComparer.OrdinalIgnoreCase);

        var problems = new List<string>();

        foreach (var entry in PortalApps.ShellRegistry)
        {
            if (!declared.TryGetValue(entry.Key, out var prefix))
            {
                problems.Add($"셸에는 '{entry.Key}' 가 있는데 그런 앱이 없다");
                continue;
            }

            if (!string.Equals(prefix, entry.RoutePrefix, StringComparison.Ordinal))
            {
                problems.Add($"'{entry.Key}' 접두사가 다르다: 셸={entry.RoutePrefix}, 앱={prefix}");
            }
        }

        foreach (var key in declared.Keys.Except(
                     PortalApps.ShellRegistry.Select(e => e.Key), StringComparer.OrdinalIgnoreCase))
        {
            problems.Add($"앱 '{key}' 가 셸의 PortalApps 에 없다 — 아무도 그 화면에 갈 수 없다");
        }

        Assert.True(problems.Count == 0, string.Join(" / ", problems));
    }

    /// <summary>
    /// 앱이 실제로 실려 있는지. 위의 검사들은 앱이 0개여도 모두 통과한다 —
    /// 어느 날 참조가 빠져서 아무것도 검사하지 않게 되는 것을 막는다.
    /// </summary>
    [Fact]
    public void 업무앱이_실려_있다()
    {
        Assert.NotEmpty(PortalApps.Assemblies);
        Assert.Equal(PortalApps.Assemblies.Count, PortalApps.Descriptors.Count);
    }
}
