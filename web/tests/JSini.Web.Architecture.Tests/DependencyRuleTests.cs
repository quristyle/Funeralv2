using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>MFE 구조를 지탱하는 규칙. 셸의 기대 목록(appsettings)과 모듈의 선언이 일치하는지는 컴파일러가 대조해 주지 않으므로 여기서 본다.</summary>
public sealed class DependencyRuleTests
{
    /// <summary>규칙 1. 업무 앱은 셸을 참조하지 않는다 — 앱이 아는 셸은 JSini.Web.Abstractions 의 계약뿐이다.</summary>
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

    /// <summary>규칙 2. 업무 앱은 다른 업무 앱을 참조하지 않는다 — 참조가 생기면 배포가 묶인다. 공유는 Blazor Common 이나 Shared Models 로 올린다.</summary>
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

    /// <summary>규칙 3. @page 는 자기 모듈의 라우트 접두사(RoutePrefix)로 시작한다 — 단일 SPA 라우터에서 충돌 없이 라우팅되려면 그래야 한다.</summary>
    [Fact]
    public void 앱의_라우트는_자기_접두사로_시작한다()
    {
        var offenders = new List<string>();

        foreach (var descriptor in PortalApps.Descriptors)
        {
            foreach (var type in descriptor.Assembly.GetTypes())
            {
                foreach (var route in type.GetCustomAttributes(typeof(RouteAttribute), false)
                             .Cast<RouteAttribute>())
                {
                    if (!route.Template.StartsWith(descriptor.RoutePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        offenders.Add(
                            $"{descriptor.Key}: @page \"{route.Template}\" 는 "
                            + $"접두사 '{descriptor.RoutePrefix}' 로 시작해야 한다.");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(" / ", offenders));
    }

    /// <summary>규칙 4. Blazor Common 은 어떤 업무 앱도 참조하지 않는다 — 참조하면 모든 앱이 그 앱에 간접 의존한다.</summary>
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

    /// <summary>규칙 5. 앱 식별자와 라우트 접두사는 겹치지 않는다 — 겹치면 셸의 YARP 라우트 순서에 따라 어느 앱이 URL 을 가져갈지 갈린다.</summary>
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

    /// <summary>규칙 6. 셸의 PortalApps 설정과 앱의 선언이 일치한다. 어긋나면 기동 대조와 진단 화면이 틀린 기대 목록을 가리킨다.</summary>
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

    /// <summary>앱이 실제로 실려 있는지. 앱이 0개여도 위의 검사들이 모두 통과하므로 그것을 막는다.</summary>
    [Fact]
    public void 업무앱이_실려_있다()
    {
        Assert.NotEmpty(PortalApps.Assemblies);
        Assert.Equal(PortalApps.Assemblies.Count, PortalApps.Descriptors.Count);
    }
}
