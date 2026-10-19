using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>사람의 얼굴은 공용 부품 UserAvatar 하나로만 그린다. 화면마다 갈라지면 사진 칸만 보는 댓글이 사진 있는 사람을 첫 글자로 띄운다.</summary>
public sealed class CommentAvatarTests
{
    [Fact]
    public void 댓글_작성자_얼굴은_포털_계정에서_로그인_아이디로_찾는다()
    {
        var razor = Read("Apps", "JSini.Web.HelpDesk", "Components", "Shared", "CommentThread.razor");

        Assert.Contains("<UserAvatar LoginId=\"@node.Comment.CreatedBy\"", razor);

        // 업무 DB 의 사진 칸으로 되돌아가면 사진이 있는 사람이 다시 첫 글자로 뜬다.
        Assert.DoesNotContain("Author?.Photo", RazorSource.Read(Path.Combine(
            Root(), "Apps", "JSini.Web.HelpDesk", "Components", "Shared", "CommentThread.razor")));
    }

    [Fact]
    public void 담당자_카드도_로그인_아이디로_얼굴을_찾는다()
    {
        var razor = Read("Apps", "JSini.Web.HelpDesk", "Components", "Pages", "HelpDeskDashboard.razor");

        Assert.Contains("<UserAvatar LoginId=\"@a.LoginId\"", razor);
    }

    [Fact]
    public void 얼굴_부품은_사진을_안_받으면_스스로_묻고_묶어서_보낸다()
    {
        var cs = Read("Shared", "JSini.Web.Components", "Data", "UserAvatar.razor.cs");
        var client = Read("Shared", "JSini.Web.Components", "Data", "UserFaceClient.cs");

        Assert.Contains("Faces.WhenKnownAsync(LoginId)", cs);

        // 한 번 양보한 뒤 모아서 보낸다 — 없으면 목록 스무 줄이 왕복 스물이 된다.
        Assert.Contains("await Task.Yield();", client);

        // 남이 묻고 있는 아이디는 그 답을 함께 기다린다.
        Assert.Contains("_inflight", client);
    }

    [Fact]
    public void 얼굴_부품은_사진을_못_받으면_첫_글자로_돌아간다()
    {
        var razor = Read("Shared", "JSini.Web.Components", "Data", "UserAvatar.razor");

        Assert.Contains("@onerror=\"@(() => OnImageErrorAsync(photo))\"", razor);
        Assert.Contains("jsini-face__initial", razor);
    }

    /// <summary>사람 얼굴을 img 로 손수 그리는 자리가 다시 생기지 않게 한다 — 클래스 이름에 얼굴·아바타가 든 그림이 있으면 그것이 그 자리다.</summary>
    [Fact]
    public void 사람의_얼굴을_손수_그리는_화면이_없다()
    {
        // 사람이 아닌 것의 그림 — 알림에 떴던 아이콘(날씨 · 앱 로고일 수 있다).
        string[] allowed = ["jsini-note-drawer__face"];

        var offenders = Directory
            .EnumerateFiles(Root(), "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith("UserAvatar.razor", StringComparison.Ordinal))
            .SelectMany(f => Regex
                .Matches(File.ReadAllText(f), "<img[^>]*class=\"([^\"]*(?:avatar|face)[^\"]*)\"", RegexOptions.IgnoreCase)
                .Select(m => (File: Path.GetFileName(f), Class: m.Groups[1].Value)))
            .Where(x => !allowed.Any(a => x.Class.Contains(a, StringComparison.Ordinal)))
            .Select(x => $"{x.File}: {x.Class}")
            .ToList();

        Assert.True(offenders.Count == 0, "UserAvatar 로 바꿀 자리: " + string.Join(", ", offenders));
    }

    [Fact]
    public void 댓글과_삭제_링크는_모두_아이콘으로_표현된다()
    {
        var razor = Read("Apps", "JSini.Web.HelpDesk", "Components", "Shared", "CommentThread.razor");

        Assert.Contains("IconCssClass=\"jsini-icon-comment-plus\"", razor);
        Assert.Contains("IconCssClass=\"jsini-icon-close\"", razor);

        Assert.DoesNotContain("Text=\"삭제\"", razor);
        Assert.DoesNotContain("Text=\"답글\"", razor);

        Assert.Contains("Title=\"삭제\"", razor);
        Assert.Contains("aria-label=\"삭제\"", razor);

        var css = Read("Shared", "JSini.Web.Components", "wwwroot", "app.css");
        Assert.Matches(@"\.jsini-icon-comment-plus\s*\{[^}]*mask-image", css);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "web", "src");
    }
}
