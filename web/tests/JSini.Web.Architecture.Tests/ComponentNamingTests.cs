using System.Reflection;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 화면 이름이 자료 타입 이름을 가리지 않는지 본다.
///
/// [이 테스트가 생긴 이유 — 같은 함정을 세 번 밟았다]
///
/// Razor 가 만드는 클래스는 폴더를 따라 <c>{모듈}.Components.Pages</c> 에 들어간다.
/// 자료 타입은 <c>{모듈}.Api</c> 에 있다. 화면 이름과 자료 이름이 같으면
/// <b>화면 안에서는 화면 자신이 이긴다</b> — 같은 네임스페이스라 더 가깝기 때문이다.
///
/// 그러면 이런 코드가 컴파일되지 않는다.
/// <code>
///     @* MyInfo.razor 안 *@
///     private MyInfo? _info;                    // ← 자료가 아니라 화면 자신
///     _info = await Api.GetMyInfoAsync();       // ← 형식이 안 맞는다
/// </code>
///
/// 오류 문구가 <c>'MyInfo' 에는 'UserId' 에 대한 정의가 없습니다</c> 라서
/// <b>자료 타입을 잘못 만든 것처럼 읽힌다.</b> 실제 원인은 이름 충돌이고,
/// 세 화면(<c>FuneralStatus</c> · <c>MyInfo</c> · <c>EnvironmentSetting</c>)에서
/// 같은 길로 헤맸다.
///
/// 규칙은 하나다 — <b>자료 타입과 같은 이름의 화면을 만들지 않는다.</b>
/// 화면 쪽에 <c>Page</c> · <c>List</c> · <c>Board</c> 를 붙인다.
/// </summary>
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
