using System.Reflection;
using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 공통트리그리드(CommTree)의 페이지네이션 표현 영역(하단 아래 띠)에
/// 확장, 축소, 선택확장 아이콘이 공통적으로 표현되는지 검증.
/// </summary>
public sealed class CommTreeExpandCommonTests
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

    private static string CommTreeRazorPath => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "CommTree.razor");

    private static string AppCssPath => Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css");

    [Fact]
    public void CommTree_아래띠_페이지네이션영역에_확장_축소_선택확장_아이콘단추가_정의되어_있다()
    {
        var text = File.ReadAllText(CommTreeRazorPath);

        Assert.Contains("jsini-icon-unfold", text);
        Assert.Contains("jsini-icon-fold", text);
        Assert.Contains("jsini-icon-child-unfold", text);

        Assert.Contains("Title=\"모두 확장\"", text);
        Assert.Contains("Title=\"모두 축소\"", text);
        Assert.Contains("Title=\"선택 하위 모두 열기\"", text);

        Assert.Contains("aria-label=\"확장\"", text);
        Assert.Contains("aria-label=\"축소\"", text);
        Assert.Contains("aria-label=\"선택확장\"", text);
    }

    [Fact]
    public void CommTree에_확장_축소_선택확장_공개메서드가_존재한다()
    {
        var type = typeof(CommTree<object>);
        var flags = BindingFlags.Instance | BindingFlags.Public;

        Assert.NotNull(type.GetMethod("ExpandAll", flags));
        Assert.NotNull(type.GetMethod("CollapseAll", flags));
        Assert.NotNull(type.GetMethod("ExpandSelectedSubtree", flags));
    }

    [Fact]
    public void CommTree에_ShowExpandButtons_파라미터가_참으로_기본정의되어_있다()
    {
        var prop = typeof(CommTree<object>).GetProperty("ShowExpandButtons", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(prop);
        Assert.True(prop!.IsDefined(typeof(ParameterAttribute), inherit: true));

        var instance = new CommTree<object>();
        var value = prop.GetValue(instance);
        Assert.Equal(true, value);
    }

    [Fact]
    public void AppCss에_트리그리드_페이저영역_아이콘배치_스타일이_정의되어_있다()
    {
        var css = File.ReadAllText(AppCssPath);

        Assert.Contains(".commtree:has(.dxbl-grid-bottom-panel)", css);
        Assert.Contains(".commtree__footer", css);
    }
}
