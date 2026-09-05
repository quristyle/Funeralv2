using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 셸이 업무 모듈을 실제로 싣고 있는가.
///
/// [이 파일이 생긴 이유]
///
/// 한동안 셸의 출력 폴더에 업무 모듈 DLL 이 <b>하나도 없었다</b>. 모듈이 각자
/// 프로세스이던 시절에 참조를 뗐고, 단일 셸로 합치면서 다시 붙이지 않았다.
/// <c>PortalModuleRegistry</c> 가 훑을 대상이 없으니 라우터에 아무것도 안
/// 들어가고, 업무 화면이 전부 404 가 됐다.
///
/// <b>그런데 빌드도 테스트도 전부 통과했다.</b> 이 테스트 프로젝트는 여섯
/// 모듈을 직접 참조하므로 자기 출력 폴더에는 다 있었기 때문이다. 규칙을
/// 검사하는 자리가 정작 "셸이 모듈을 볼 수 있는가" 만 못 보고 있었다.
///
/// [어셈블리 참조로 검사할 수 없는 이유]
///
/// 셸은 모듈 타입을 코드에서 한 번도 쓰지 않는다(그게 설계다). 그러면 C#
/// 컴파일러가 어셈블리 참조를 메타데이터에 남기지 않으므로,
/// <c>GetReferencedAssemblies()</c> 로는 보이지 않는다. 그래서 csproj 를 읽는다.
/// </summary>
public sealed class ShellCompositionTests
{
    /// <summary>
    /// 셸이 모든 업무 모듈을 <c>ProjectReference</c> 로 참조한다.
    ///
    /// 이것이 모듈 DLL 을 셸 출력 폴더로 나르는 유일한 통로다.
    /// 빠지면 그 업무만 통째로 404 가 된다.
    /// </summary>
    [Fact]
    public void 셸이_모든_업무_모듈을_참조한다()
    {
        var referenced = ShellProject.ProjectReferenceNames();

        var missing = PortalApps.Assemblies
            .Select(a => a.GetName().Name!)
            .Where(name => !referenced.Contains(name))
            .ToList();

        Assert.True(missing.Count == 0,
            $"셸 csproj 가 참조하지 않는 업무 모듈: {string.Join(", ", missing)}. "
            + "참조가 없으면 DLL 이 셸 출력 폴더에 실리지 않고, 그 업무 화면이 전부 404 가 된다.");
    }

    /// <summary>
    /// 반대 방향도 본다 — 셸이 참조하는데 <c>PortalApps</c> 설정에 없는 모듈.
    ///
    /// 그 모듈의 화면은 열리기는 하지만, 기동 대조와 진단 화면에서 빠진다.
    /// </summary>
    [Fact]
    public void 셸이_참조하는_모듈은_모두_설정에_있다()
    {
        var referenced = ShellProject.ProjectReferenceNames()
            .Where(n => n.StartsWith("JSini.Web.", StringComparison.Ordinal)
                        && !PortalApps.Shared.Contains(n))
            .ToList();

        var declaredKeys = PortalApps.ShellRegistry.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphans = referenced
            .Where(name => !PortalApps.Descriptors.Any(d =>
                d.Assembly.GetName().Name == name && declaredKeys.Contains(d.Key)))
            .ToList();

        Assert.True(orphans.Count == 0,
            $"셸이 참조하지만 PortalApps 설정에 없는 모듈: {string.Join(", ", orphans)}");
    }

    /// <summary>
    /// 모듈마다 포괄 라우트(<c>/funeral/{{*rest}}</c>)가 <b>정확히 하나</b> 있다.
    ///
    /// 아직 옮기지 않은 화면이 빈 404 로 끝나지 않게 하는 안내다
    /// (<c>Components/Pages/_Pending.razor</c>).
    ///
    /// 둘 이상이면 어느 쪽이 이길지 순서에 달리고, 없으면 그 업무만 이행 중에
    /// 안내 없이 404 가 난다 — 사용자가 "안 옮긴 화면" 과 "오타" 를 구분할 수 없다.
    /// </summary>
    [Fact]
    public void 모듈마다_포괄_라우트가_하나씩_있다()
    {
        var problems = new List<string>();

        foreach (var descriptor in PortalApps.Descriptors)
        {
            var catchAlls = descriptor.Assembly.GetTypes()
                .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>())
                .Select(r => r.Template)
                .Where(t => t.Contains("{*", StringComparison.Ordinal))
                .ToList();

            if (catchAlls.Count != 1)
            {
                problems.Add($"{descriptor.Key}: 포괄 라우트가 {catchAlls.Count}개 "
                             + $"({string.Join(", ", catchAlls)}) — 하나여야 한다");
                continue;
            }

            var expected = $"{descriptor.RoutePrefix}/{{*rest}}";
            if (catchAlls[0] != expected)
            {
                problems.Add($"{descriptor.Key}: 포괄 라우트가 '{catchAlls[0]}' 인데 '{expected}' 여야 한다");
            }
        }

        Assert.True(problems.Count == 0, string.Join(" / ", problems));
    }

    /// <summary>
    /// <c>@page</c> 에 적은 <b>모든 라우트 매개변수</b>를 받을 속성이 화면에 있다.
    ///
    /// [실제로 밟은 것 — 포괄 라우트 여섯이 전부 500 이었다]
    ///
    /// <c>@page "/admin/{{*rest}}"</c> 라고 써 놓고 <c>rest</c> 를 받는
    /// <c>[Parameter]</c> 를 만들지 않았다. Blazor 는 라우트가 준 값을 넣을 곳이
    /// 없으면 예외를 던진다 —
    ///
    /// <code>
    /// Object of type '…_Pending' does not have a property matching the name 'rest'.
    /// </code>
    ///
    /// 그래서 「준비 중」 안내는 <b>한 번도 뜬 적이 없다.</b> 아직 안 옮긴
    /// 주소를 열면 안내 대신 오류 화면이 나왔고, 그러면 「안 옮긴 화면」과
    /// 「주소를 잘못 친 것」이 다시 구별되지 않아 포괄 라우트를 둔 이유가
    /// 사라진다.
    ///
    /// 빌드도 아키텍처 테스트도 통과했다. 라우트 값 채우기는 실행 시점 일이라
    /// 컴파일이 잡을 수 없어서, 여기서 잡는다.
    /// </summary>
    [Fact]
    public void 라우트_매개변수를_받을_속성이_있다()
    {
        var problems = new List<string>();

        foreach (var assembly in PortalApps.Descriptors.Select(d => d.Assembly).Distinct())
        {
            foreach (var type in assembly.GetTypes())
            {
                var templates = type.GetCustomAttributes(typeof(RouteAttribute), false)
                    .Cast<RouteAttribute>()
                    .Select(r => r.Template)
                    .ToList();

                if (templates.Count == 0)
                {
                    continue;
                }

                // 화면이 받을 수 있는 이름들. 대소문자를 가리지 않는다 —
                // 라우트는 `rest`, 속성은 `Rest` 로 쓰는 것이 보통이다.
                var accepted = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttributes(typeof(ParameterAttribute), true).Length > 0)
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var name in templates.SelectMany(RouteParameterNames))
                {
                    if (!accepted.Contains(name))
                    {
                        problems.Add($"{type.FullName}: 라우트가 '{name}' 을 넘기는데 "
                                     + "그 이름의 [Parameter] 속성이 없다 — 열면 500 이 난다");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(" / ", problems));
    }

    /// <summary>
    /// 라우트 틀에서 매개변수 이름만 뽑는다.
    ///
    /// <c>{id:int}</c> 의 제약(<c>:int</c>)과 <c>{*rest}</c> 의 별표,
    /// <c>{id?}</c> 의 물음표를 뗀다 — 셋 다 이름이 아니다.
    /// </summary>
    private static IEnumerable<string> RouteParameterNames(string template)
    {
        foreach (Match match in Regex.Matches(template, @"\{([^}]+)\}"))
        {
            var name = match.Groups[1].Value.TrimStart('*');
            var cut = name.IndexOf(':');

            if (cut >= 0)
            {
                name = name[..cut];
            }

            name = name.TrimEnd('?');

            if (name.Length > 0)
            {
                yield return name;
            }
        }
    }
}

/// <summary>셸의 csproj 를 읽는다. 어셈블리 메타데이터로는 보이지 않는 것이 있어서다.</summary>
internal static class ShellProject
{
    /// <summary>
    /// 셸 csproj 의 <c>ProjectReference</c> 들. 프로젝트 파일 이름에서 확장자를
    /// 뗀 값이 곧 어셈블리 이름이다(이 저장소는 그 규칙을 지킨다).
    /// </summary>
    internal static HashSet<string> ProjectReferenceNames()
    {
        var document = XDocument.Load(Locate());

        return document.Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Path.GetFileNameWithoutExtension(v!.Replace('\\', Path.DirectorySeparatorChar)))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// 셸 csproj 를 찾는다. 출력 폴더에서 위로 올라가며 <c>web/</c> 를 찾는다 —
    /// 상대 경로를 몇 칸 올라가라고 박아 두면 대상 프레임워크나 구성이 바뀔 때
    /// 조용히 어긋난다.
    /// </summary>
    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "src", "Shell", "JSini.Web.Shell", "JSini.Web.Shell.csproj");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "셸 csproj 를 찾지 못했다. 저장소 구조가 바뀌었으면 이 탐색을 고쳐야 한다.");
    }
}
