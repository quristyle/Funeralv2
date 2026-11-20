using System.Reflection;
using JSini.Web.Admin.Components.Pages;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// /admin/auth/user-role 화면의 볼 수 있는 메뉴 트리 그리드 확장, 축소, 선택확장 기능 검증.
/// </summary>
public sealed class UserRoleMapTreeExpandTests
{
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

    private static string UserRoleMapRazorPath => Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "UserRoleMap.razor");

    [Fact]
    public void UserRoleMap_화면에_볼수있는메뉴_트리그리드용_펼침_접기_아이콘_단추가_정의되어_있다()
    {
        var text = File.ReadAllText(UserRoleMapRazorPath);

        Assert.Contains("jsini-icon-unfold", text);
        Assert.Contains("jsini-icon-fold", text);
        Assert.Contains("jsini-icon-child-unfold", text);

        Assert.Contains("확장", text);
        Assert.Contains("축소", text);
        Assert.Contains("선택확장", text);
        Assert.Contains("모두 확장", text);
        Assert.Contains("모두 축소", text);
        Assert.Contains("선택 하위 모두 열기", text);
    }

    [Fact]
    public void UserRoleMap_코드비하인드에_트리_펼침_접기_메서드가_존재한다()
    {
        var type = typeof(UserRoleMap);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        Assert.NotNull(type.GetMethod("ExpandAll", flags));
        Assert.NotNull(type.GetMethod("CollapseAll", flags));
        Assert.NotNull(type.GetMethod("ExpandSelectedSubtree", flags));
    }
}
