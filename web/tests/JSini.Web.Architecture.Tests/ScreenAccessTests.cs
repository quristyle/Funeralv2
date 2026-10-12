using System.Text.RegularExpressions;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 화면마다 <b>권한의 출처</b>가 분명한지 빌드 때 검사한다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 테스트로 막나 — 틀림이 조용하다]
/// </para>
///
/// <para>
/// 2026-10-13 부터 권한관리 밖의 주소는 <b>기본으로 막힌다</b>
/// (<c>ScreenAccess</c>). 틀리는 방향을 「안 열리는」 쪽으로 두었으므로,
/// 신고를 빠뜨린 화면은 <b>오류 한 줄 없이 「권한이 없습니다」만 띄운다.</b>
/// 만든 사람은 자기 역할에 권한이 있어서 못 느끼고, 나중에 다른 사람이
/// 열었을 때 「권한을 줬는데 왜 안 되지」로 나타난다 — 아무리 줘도 안 열린다.
/// </para>
///
/// <para>
/// DB 를 볼 수 없으므로 「이 열쇠의 메뉴가 실제로 있나」는 못 따진다. 그건
/// 운영에서 <b>차단 접근기록</b>이 말해 준다(<c>UNMANAGED</c> 줄). 여기서는
/// <b>코드만으로 알 수 있는 것</b>을 막는다 — 가리킨 부모가 저장소에 없다거나,
/// 까닭 없이 열어 두었다거나.
/// </para>
/// </remarks>
public sealed class ScreenAccessTests
{
    [Fact]
    public void 모든_화면이_권한의_출처를_갖는다()
    {
        var catalog = ScreenAccessCatalog.Build([.. PortalApps.Assemblies]);

        var offenders = RoutableTypes()
            .Where(t => catalog.For(t) is null)
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "ScreenAccessCatalog 가 못 찾은 화면이 있다:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <c>[ScreenAccess("부모 열쇠")]</c> 의 부모가 <b>저장소에 실제로 있어야</b> 한다.
    /// </summary>
    /// <remarks>
    /// 오타 하나면 그 화면이 영영 막힌다. 그런데 열쇠는 글자라서 컴파일러가
    /// 아무 말도 안 해 준다 — <c>RouteKey</c> 를 바꿨을 때 가리키던 쪽이 같이
    /// 안 고쳐지는 것도 같은 모양이다.
    /// </remarks>
    [Fact]
    public void 물려받을_부모가_실제로_있다()
    {
        var keys = RoutableTypes()
            .Select(KeyOf)
            .Where(k => k is not null)
            .Select(k => k!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var offenders = new List<string>();

        foreach (var type in RoutableTypes())
        {
            if (DeclarationOf(type) is not { InheritsFrom: { } parent }) continue;

            if (!keys.Contains(parent))
            {
                offenders.Add($"{type.Name} 가 가리키는 부모 '{parent}' 를 가진 화면이 없다");
            }
            else if (string.Equals(parent, KeyOf(type), StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{type.Name} 가 자기 자신을 가리킨다 ('{parent}')");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(" / ", offenders));
    }

    /// <summary>
    /// <c>Open</c> 으로 열어 두었으면 <b>까닭을 적는다.</b>
    /// </summary>
    /// <remarks>
    /// 기본 차단을 통째로 비켜 가는 선언이다. 까닭이 코드 주석에만 있으면
    /// 나중에 「이건 왜 열려 있지」를 되짚을 길이 없고, 되짚지 못하면 아무도
    /// 못 걷어낸다. <c>Reason</c> 은 진단 화면에도 그대로 나온다.
    /// </remarks>
    [Fact]
    public void 열어_둔_화면은_까닭을_적는다()
    {
        var offenders = RoutableTypes()
            .Where(t => DeclarationOf(t) is { Kind: ScreenAccessKind.Open, Reason: null or "" })
            .Select(t => t.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "ScreenAccessKind.Open 에는 Reason 을 함께 적는다: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// 포괄 라우트(<c>_Pending.razor</c>)에는 신고를 붙이지 않는다.
    /// </summary>
    /// <remarks>
    /// 그건 화면이 아니라 「아직 화면이 없다」는 안내라 보여 줄 자료가 없고,
    /// 기본값이 이미 열림이다(<c>ScreenAccessCatalog</c> 머리말). 붙이면
    /// 「이 업무는 권한을 안 따지기로 했다」로 읽힌다.
    /// </remarks>
    [Fact]
    public void 포괄_라우트에는_신고를_붙이지_않는다()
    {
        var offenders = PortalApps.Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(IsCatchAll)
            .Where(t => DeclarationOf(t) is not null)
            .Select(t => t.Name)
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(", ", offenders));
    }

    /// <summary>
    /// 셸의 화면은 <b>반드시</b> 신고한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 셸의 화면은 <c>RouteKey</c> 가 없다 — 메뉴가 가리키는 업무 화면이 아니라
    /// 로그인·오류·첫 화면처럼 <b>메뉴에 올릴 수 없는</b> 자리이기 때문이다.
    /// 그래서 신고가 없으면 가리킬 메뉴를 못 찾아 <b>로그인한 사람에게는 전부
    /// 막힌다</b> — 「로그아웃하면 로그인 화면이 뜨는데 로그인하면 안 뜬다」 같은
    /// 모양이 된다.
    /// </para>
    /// <para>
    /// 테스트 프로젝트는 셸을 참조하지 않으므로(<c>PortalApps</c> 머리말)
    /// 리플렉션으로는 볼 수 없다. 소스 글자로 검사한다.
    /// </para>
    /// </remarks>
    [Fact]
    public void 셸의_화면은_ScreenAccess_를_적는다()
    {
        var shell = Path.Combine(SolutionRoot(), "src", "Shell");

        var offenders = Directory
            .EnumerateFiles(shell, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"^@page\s", RegexOptions.Multiline))
            .Where(f => !File.ReadAllText(f).Contains("[ScreenAccess", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(SolutionRoot(), f))
            .ToList();

        Assert.True(offenders.Count == 0,
            "@attribute [ScreenAccess(...)] 를 적는다 — 없으면 로그인한 사람에게 막힌다:\n  "
            + string.Join("\n  ", offenders));
    }

    private static IEnumerable<Type> RoutableTypes() => PortalApps.Assemblies
        .SelectMany(a => a.GetTypes())
        .Where(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Length > 0)
        .Where(t => !IsCatchAll(t));

    private static bool IsCatchAll(Type type) => type
        .GetCustomAttributes(typeof(RouteAttribute), false)
        .Cast<RouteAttribute>()
        .Any(r => r.Template.Contains("{*", StringComparison.Ordinal));

    private static ScreenAccessAttribute? DeclarationOf(Type type) => type
        .GetCustomAttributes(typeof(ScreenAccessAttribute), false)
        .Cast<ScreenAccessAttribute>()
        .FirstOrDefault();

    private static string? KeyOf(Type type) => type
        .GetCustomAttributes(typeof(RouteKeyAttribute), false)
        .Cast<RouteKeyAttribute>()
        .FirstOrDefault()?.Key;

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
