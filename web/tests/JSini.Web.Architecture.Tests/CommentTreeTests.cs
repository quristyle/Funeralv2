using JSini.Web.HelpDesk.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 헬프데스크 댓글을 나무로 세우는 규칙(<see cref="CommentTree"/>).
///
/// <para>
/// 여기서 막는 것은 <b>말없이 사라지는 댓글</b>이다. 부모를 못 찾은 줄을
/// 버리거나 고리를 만난 자리에서 멈추면, 쓴 사람에게는 「내 답글이 안
/// 올라갔다」로 보이는데 자료에는 멀쩡히 들어 있어 원인이 보이지 않는다.
/// </para>
/// </summary>
public sealed class CommentTreeTests
{
    private static ImprovementComment Row(int id, int? parent = null, int minute = 0) => new()
    {
        Id = id,
        ParentCommentId = parent,
        CommentText = $"#{id}",
        CreatedAt = new DateTime(2026, 9, 28, 9, minute, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void 부모가_없는_댓글은_뿌리가_된다()
    {
        var roots = CommentTree.Build([Row(1), Row(2, minute: 1)]);

        Assert.Equal([1, 2], roots.Select(r => r.Comment.Id));
        Assert.All(roots, r => Assert.Empty(r.Children));
    }

    [Fact]
    public void 대댓글의_대댓글까지_이어진다()
    {
        var roots = CommentTree.Build(
        [
            Row(1),
            Row(2, parent: 1, minute: 1),
            Row(3, parent: 2, minute: 2),
            Row(4, parent: 3, minute: 3),
        ]);

        var node = Assert.Single(roots);

        Assert.Equal(0, node.Depth);
        Assert.Equal(4, node.Total);

        for (var depth = 1; depth <= 3; depth++)
        {
            node = Assert.Single(node.Children);
            Assert.Equal(depth, node.Depth);
        }
    }

    [Fact]
    public void 같은_부모의_답글은_쓴_순서다()
    {
        var roots = CommentTree.Build(
        [
            Row(1),
            Row(3, parent: 1, minute: 5),
            Row(2, parent: 1, minute: 2),
        ]);

        var node = Assert.Single(roots);
        Assert.Equal([2, 3], node.Children.Select(c => c.Comment.Id));
    }

    [Fact]
    public void 부모를_못_찾은_댓글은_버리지_않고_뿌리로_올린다()
    {
        // 99 번은 목록에 없다. 버리면 그 아래 가지가 통째로 사라진다.
        var roots = CommentTree.Build([Row(1), Row(2, parent: 99, minute: 1), Row(3, parent: 2, minute: 2)]);

        Assert.Equal([1, 2], roots.Select(r => r.Comment.Id));
        Assert.Equal(3, Assert.Single(roots[1].Children).Comment.Id);
    }

    [Fact]
    public void 스스로를_가리키면_뿌리로_세운다()
    {
        var roots = CommentTree.Build([Row(1, parent: 1)]);

        var node = Assert.Single(roots);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void 서로를_가리켜도_모두_보인다()
    {
        // 둘이 고리를 이룬다. 끊지 않으면 어느 뿌리에도 안 닿아 화면에서 빠진다.
        var roots = CommentTree.Build([Row(1, parent: 2), Row(2, parent: 1, minute: 1)]);

        var seen = new List<int>();
        Walk(roots, seen);

        Assert.Equal(2, seen.Count);
        Assert.Contains(1, seen);
        Assert.Contains(2, seen);
    }

    [Fact]
    public void 빈_목록과_null_을_받아_넘긴다()
    {
        Assert.Empty(CommentTree.Build(null));
        Assert.Empty(CommentTree.Build([]));
    }

    private static void Walk(IEnumerable<CommentNode> nodes, List<int> into)
    {
        foreach (var node in nodes)
        {
            Assert.DoesNotContain(node.Comment.Id, into);
            into.Add(node.Comment.Id);
            Walk(node.Children, into);
        }
    }
}
