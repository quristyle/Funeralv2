using System.Reflection;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>화면 이름이 자료 타입 이름을 가리지 않는지 본다. 같은 이름이면 같은 네임스페이스에서 화면이 이겨 「자료가 아니라 화면 자신」으로 잡히고, 오류 문구가 자료 타입을 잘못 만든 것처럼 읽힌다.
/// 규칙: 자료 타입과 같은 이름의 화면을 만들지 않는다 — 화면 쪽에 Page·List·Board 를 붙인다.</summary>
public sealed class ComponentNamingTests
{
    [Fact]
    public void 화면_이름이_자료_타입_이름을_가리지_않는다()
    {
        var problems = new List<string>();

        foreach (var assembly in PortalApps.Assemblies)
        {
            var moduleName = assembly.GetName().Name!;

            // 이 모듈의 자료 타입. Api 네임스페이스에 있는 것만 본다 —
            // 화면끼리 이름이 겹치는 것은 폴더가 갈라 주므로 문제가 아니다.
            var dataTypes = assembly.GetTypes()
                .Where(t => t.IsPublic && t.Namespace?.EndsWith(".Api", StringComparison.Ordinal) is true)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.Ordinal);

            // 라우트를 가진 컴포넌트 = 화면.
            var pages = assembly.GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Length > 0);

            foreach (var page in pages)
            {
                if (dataTypes.Contains(page.Name))
                {
                    problems.Add(
                        $"{moduleName}: 화면 '{page.Name}' 이 같은 이름의 자료 타입을 가린다. "
                        + $"파일 이름을 '{page.Name}Page' 처럼 바꿔라.");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(" / ", problems));
    }
}
