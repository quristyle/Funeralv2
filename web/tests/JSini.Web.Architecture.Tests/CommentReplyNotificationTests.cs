using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class CommentReplyNotificationTests
{
    [Fact]
    public void 댓글_답글시_부모댓글_작성자에게_답글_알림을_보낸다()
    {
        var code = StripComments(NotifierSource());

        Assert.Contains("comment.ParentCommentId", code);
        Assert.Contains("CommentAuthorLoginIdAsync", code);
        Assert.Contains("SendPushAsync", code);
        Assert.Contains("SendEmailAsync", code);
        Assert.Contains("isReply: true", code);
    }

    [Fact]
    public void 글쓴이와_부모댓글_작성자가_같으면_답글_알림만_한번_보낸다()
    {
        var code = StripComments(NotifierSource());

        // 동일인일 때의 중복 발송 방지 조건 검증
        Assert.Contains("!string.Equals(ownerLoginId, parentAuthorLoginId", code);
    }

    [Fact]
    public void 댓글_알림_이메일의_포털_확인_링크는_버튼으로_구성된다()
    {
        var code = NotifierSource();

        // 메일 본문에 포털에서 확인 단추가 버튼 스타일로 들어있는지 검증
        Assert.Contains("포털에서 확인", code);
        Assert.Contains("background-color:#007bff", code);
        Assert.Contains("border-radius:4px", code);
        Assert.Contains("color:#ffffff", code);
        Assert.Contains("text-decoration:none", code);
    }

    [Fact]
    public void 댓글_작성자_해석기는_연결표_관리자_고객_CreatedBy를_확인한다()
    {
        var code = StripComments(ResolverSource());

        Assert.Contains("CommentAuthorLoginIdAsync", code);
        Assert.Contains("LinkedLoginIdAsync", code);
        Assert.Contains("Admins", code);
        Assert.Contains("Customers", code);
        Assert.Contains("comment.CreatedBy", code);
    }

    private static string StripComments(string code)
    {
        var withoutBlocks = System.Text.RegularExpressions.Regex.Replace(code, @"/\*[\s\S]*?\*/", " ");
        return System.Text.RegularExpressions.Regex.Replace(withoutBlocks, @"//[^\n]*", " ");
    }

    private static string NotifierSource() => File.ReadAllText(Path.Combine(
        RepoRoot(), "microservices", "HelpDeskServer", "Services", "CommentNotifier.cs"));

    private static string ResolverSource() => File.ReadAllText(Path.Combine(
        RepoRoot(), "microservices", "HelpDeskServer", "Services", "RequestOwnerResolver.cs"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "microservices")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
