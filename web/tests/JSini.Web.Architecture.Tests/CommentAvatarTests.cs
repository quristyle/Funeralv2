using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class CommentAvatarTests
{
    [Fact]
    public void 프로필_사진이_없는_댓글_작성자는_이니셜_첫글자를_표시한다()
    {
        var razor = CommentThreadRazor();
        var cs = CommentThreadCs();

        // 프로필 이미지가 없을 때 이니셜 첫글자를 표현하는 태그 확인
        Assert.Contains("AuthorInitial(node)", razor);
        Assert.Contains("hd-cmt__avatar--initial", razor);

        // 코드 비하인드에서 이니셜 계산 함수 존재 확인
        Assert.Contains("string AuthorInitial(CommentNode node)", cs);
        Assert.Contains("ToUpperInvariant()", cs);
    }

    [Fact]
    public void 프로필_사진이_있는_댓글_작성자는_프로필_사진을_표시한다()
    {
        var razor = CommentThreadRazor();
        var cs = CommentThreadCs();

        // 사진이 있을 때 img 태그로 프로필 사진 표시
        Assert.Contains("<img class=\"hd-cmt__avatar\"", razor);
        Assert.Contains("src=\"@avatar\"", razor);

        // 사진이 비어있을 때는 null 반환하여 이니셜로 폴백
        Assert.Contains("return null;", cs);
    }

    [Fact]
    public void 담당자_카드에서도_프로필_사진과_이니셜_표현이_함께_동작한다()
    {
        var razor = DashboardRazor();
        var cs = DashboardCs();

        // 담당자 카드에서 프로필 사진 및 이니셜 분기 확인
        Assert.Contains("AvatarUrl(a.Photo)", razor);
        Assert.Contains("hd-card__avatar--img", razor);
        Assert.Contains("Initial(a.AdminName)", razor);
        Assert.Contains("string? AvatarUrl(string? photo)", cs);
    }

    [Fact]
    public void 댓글과_삭제_링크는_모두_아이콘으로_표현된다()
    {
        var razor = CommentThreadRazor();

        // 아이콘 클래스로 표현
        Assert.Contains("IconCssClass=\"jsini-icon-comment\"", razor);
        Assert.Contains("IconCssClass=\"jsini-icon-trash\"", razor);

        // 텍스트 속성은 사용하지 않음
        Assert.DoesNotContain("Text=\"삭제\"", razor);
        Assert.DoesNotContain("Text=\"답글\"", razor);

        // 접근성 및 툴팁
        Assert.Contains("Title=\"삭제\"", razor);
        Assert.Contains("aria-label=\"삭제\"", razor);

        // CSS 마스크 정의 존재 확인
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "web", "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));
        Assert.Matches(@"\.jsini-icon-comment\s*\{[^}]*mask-image", css);
    }

    private static string CommentThreadRazor() => File.ReadAllText(Path.Combine(
        RepoRoot(), "web", "src", "Apps", "JSini.Web.HelpDesk", "Components", "Shared", "CommentThread.razor"));

    private static string CommentThreadCs() => File.ReadAllText(Path.Combine(
        RepoRoot(), "web", "src", "Apps", "JSini.Web.HelpDesk", "Components", "Shared", "CommentThread.razor.cs"));

    private static string DashboardRazor() => File.ReadAllText(Path.Combine(
        RepoRoot(), "web", "src", "Apps", "JSini.Web.HelpDesk", "Components", "Pages", "HelpDeskDashboard.razor"));

    private static string DashboardCs() => File.ReadAllText(Path.Combine(
        RepoRoot(), "web", "src", "Apps", "JSini.Web.HelpDesk", "Components", "Pages", "HelpDeskDashboard.razor.cs"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
