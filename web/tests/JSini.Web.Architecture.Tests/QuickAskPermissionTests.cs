using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 상단 헤더의 빠른 지시(⚡) 아이콘은 데스크탑의 서랍 등록 여부와 무관하게
/// 메뉴 사용 권한이 있는 사용자에게만 노출되고 동작해야 한다.
/// </summary>
public sealed class QuickAskPermissionTests
{
    [Fact]
    public void 빠른지시_아이콘_표시_조건은_서랍_등록_여부로_메뉴_권한을_우회하지_않는다()
    {
        var source = HeaderToolsSource();

        // 1. CanShowAsk 에서 서랍 등록 여부(AskDocks)를 OR 조건으로 사용해선 안 된다.
        Assert.DoesNotContain("AskMenu is not null || AskDocks", source);
        Assert.DoesNotContain("AskDocks ||", source);

        // 2. CanShowAsk 는 메뉴 권한(HasAskPermission) 검사를 필수로 해야 한다.
        Assert.Contains("private bool HasAskPermission =>", source);
        Assert.Contains("CanShowAsk => HasAskPermission && HeaderIcons.IsIconEnabled(\"quick_ask\")", source);

        // 3. HasAskPermission 은 메뉴 트리 또는 권한 컨텍스트(AskPath)를 검사해야 한다.
        Assert.Contains("AskMenu is not null || Permissions.CanView(AskPath)", source);

        // 4. OpenAsk 에서도 권한이 없는 경우 조기 반환(방어 로직)해야 한다.
        Assert.Contains("if (!HasAskPermission)", source);
    }

    private static string HeaderToolsSource() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "HeaderTools.razor.cs"));

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
