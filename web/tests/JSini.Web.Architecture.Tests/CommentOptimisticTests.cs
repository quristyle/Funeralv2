using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>댓글 「남기기」는 저장을 기다리지 않는다. 서버가 알림까지 다 보내고 답하므로 기다리면 누른 뒤 한참 화면이 멈춘 것처럼 보인다.</summary>
public sealed class CommentOptimisticTests
{
    [Fact]
    public void 남기기는_보내는_중_댓글을_먼저_끼우고_저장은_뒤에서_한다()
    {
        var detail = RazorSource.Read(Path.Combine(
            Root(), "Apps", "JSini.Web.HelpDesk", "Components", "Pages", "RequestDetail.razor"));

        Assert.Contains("_pending.Add(pending);", detail);
        Assert.Contains("_ = SendAsync(pending);", detail);

        // 요청 본문·신원까지 통째로 다시 읽으면(ReloadAsync) 그 왕복만큼 또 멈춘다.
        var add = detail[detail.IndexOf("private Task<bool> AddAsync(", StringComparison.Ordinal)..];
        add = add[..add.IndexOf("private sealed class PendingComment", StringComparison.Ordinal)];
        Assert.DoesNotContain("await ", add);
        Assert.DoesNotContain("ReloadAsync", add);

        // 못 보낸 댓글은 사라지지 않고 그 자리에서 다시 보낼 수 있어야 한다 — 편집기는 이미 비었다.
        Assert.Contains("OnRetry=\"@RetryCommentAsync\"", detail);
        Assert.Contains("OnDiscard=\"@DiscardComment\"", detail);
    }

    [Fact]
    public void 보내는_중인_댓글에는_답글과_삭제를_세우지_않는다()
    {
        var thread = RazorSource.Read(Path.Combine(
            Root(), "Apps", "JSini.Web.HelpDesk", "Components", "Shared", "CommentThread.razor"));

        Assert.Contains("!IsPending(node) && !node.Comment.IsDel", thread);
        Assert.Contains("OnRetry=\"@OnRetry\"", thread);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "web")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "web", "src");
    }
}
