using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 업무 비서 화면(<c>/admin/ai/assistant</c>)의 본문 대조. <c>AiAskLayoutTests.cs</c> 의
/// 본문 대조 방식을 따른다.
/// </summary>
public sealed class AssistantPageTests
{
    [Fact]
    public void 화면_경로와_라우트키가_짝이다()
    {
        var razor = PageRazor();
        Assert.Contains("@page \"/admin/ai/assistant\"", razor, StringComparison.Ordinal);
        Assert.Contains("[RouteKey(\"admin.ai.assistant\")]", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void 도구_진행_안내를_그리는_자리가_있다()
        => Assert.Contains("\"tool\"", PageCode(), StringComparison.Ordinal);

    [Fact]
    public void 자료_범위_고르개가_있다()
        => Assert.Contains("assistant__scope", PageRazor(), StringComparison.Ordinal);

    private static string PageRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "Assistant.razor"));

    private static string PageCode() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "Assistant.razor.cs"));

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
