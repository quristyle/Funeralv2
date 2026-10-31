using System.Reflection;
using JSini.Web.Admin.Components.Pages;
using JSini.Web.Components.Data;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// /admin/system/menu 화면의 트리 그리드 모두 펼치기/접기 및 선택 하위 펼치기/접기 기능 검증.
/// </summary>
public sealed class MenuListTreeExpandTests
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

    private static string MenuListRazorPath => Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "MenuList.razor");

    private static string AppCssPath => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css");

    [Fact]
    public void MenuList_화면에_트리_펼침_접기_아이콘_단추가_정의되어_있다()
    {
        var text = File.ReadAllText(MenuListRazorPath);

        Assert.Contains("jsini-icon-unfold", text);
        Assert.Contains("jsini-icon-fold", text);
        Assert.Contains("jsini-icon-child-unfold", text);
        Assert.Contains("jsini-icon-child-fold", text);

        Assert.Contains("Title=\"모두 펼치기\"", text);
        Assert.Contains("Title=\"모두 접기\"", text);
        Assert.Contains("Title=\"선택 하위 모두 펼치기\"", text);
        Assert.Contains("Title=\"선택 하위 모두 접기\"", text);
    }

    [Fact]
    public void MenuList_코드비하인드에_트리_펼침_접기_메서드가_존재한다()
    {
        var type = typeof(MenuList);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        Assert.NotNull(type.GetMethod("ExpandAll", flags));
        Assert.NotNull(type.GetMethod("CollapseAll", flags));
        Assert.NotNull(type.GetMethod("ExpandSelectedSubtree", flags));
        Assert.NotNull(type.GetMethod("CollapseSelectedSubtree", flags));
    }

    [Fact]
    public void CommTree에_공개_ExpandAll_CollapseAll_메서드가_존재한다()
    {
        var type = typeof(CommTree<object>);
        Assert.NotNull(type.GetMethod(nameof(CommTree<object>.ExpandAll), BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetMethod(nameof(CommTree<object>.CollapseAll), BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void AppCss에_선택_하위_펼치기_접기_아이콘_클래스가_정의되어_있다()
    {
        var css = File.ReadAllText(AppCssPath);

        Assert.Contains(".jsini-icon-child-unfold", css);
        Assert.Contains(".jsini-icon-child-fold", css);
    }
}
