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
