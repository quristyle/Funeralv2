namespace JSini.Web.HelpDesk.Api;

/// <summary>평평하게 받아 온 댓글을 부모-자식 나무로 세운다. 서버가 주는 것은 ParentCommentId 하나뿐이고 깊이 제한이 없다.
/// 부모가 목록에 없는 줄은 버리지 않고 뿌리로 올린다(버리면 그 아래 답글이 통째로 사라진다). 고리는 이미 본 줄을 만난 자리에서 끊어 뿌리로 세운다.</summary>
public static class CommentTree
{
    /// <summary>댓글 목록을 나무로 세운다. 돌려주는 것은 뿌리 댓글들이고, 같은 부모를 둔 것끼리는 쓴 순서(오래된 것이 먼저)다.</summary>
    public static IReadOnlyList<CommentNode> Build(IEnumerable<ImprovementComment>? comments)
    {
        if (comments is null)
        {
            return [];
        }

        // 서버는 createdAt 순으로 주지만 같은 초의 앞뒤가 정해지지 않아 번호로 한 번 더 가른다.
        var ordered = comments
            .Where(c => c is not null)
            .OrderBy(c => c.CreatedAt ?? DateTime.MinValue)
            .ThenBy(c => c.Id)
            .ToList();

        var nodes = new Dictionary<int, CommentNode>();

        foreach (var comment in ordered)
        {
            // 같은 번호가 두 줄 오면 앞엣것을 쓴다 — 뒤엣것으로 덮으면 그 아래 답글이 갈 곳을 잃는다.
            nodes.TryAdd(comment.Id, new CommentNode(comment));
        }

        var roots = new List<CommentNode>();

        foreach (var comment in ordered)
        {
            var node = nodes[comment.Id];

            if (ParentOf(nodes, comment) is { } parent)
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        Depths(roots, 0);

        return roots;
    }

    /// <summary>이 댓글을 매달 부모. 없거나(뿌리) 못 찾거나 고리면 null — 부르는 쪽이 뿌리로 세운다.</summary>
    private static CommentNode? ParentOf(
        IReadOnlyDictionary<int, CommentNode> nodes, ImprovementComment comment)
    {
        if (comment.ParentCommentId is not { } parentId || parentId == comment.Id)
        {
            return null;
        }

        if (!nodes.TryGetValue(parentId, out var parent))
        {
            // 부모가 목록에 없다. 이 줄을 버리지 않고 뿌리로 올린다.
            return null;
        }

        // 부모를 따라 위로 올라가다 자기 자신을 다시 만나면 고리다.
        var seen = new HashSet<int> { comment.Id };
        var walker = parent.Comment;

        while (walker.ParentCommentId is { } up)
        {
            if (!seen.Add(walker.Id) || up == walker.Id)
            {
                return null;
            }

            if (!nodes.TryGetValue(up, out var next))
            {
                break;
            }

            walker = next.Comment;
        }

        return parent;
    }

    /// <summary>깊이를 적어 둔다. 화면이 들여쓰기 폭을 그것으로 정한다.</summary>
    private static void Depths(IEnumerable<CommentNode> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            node.Depth = depth;
            Depths(node.Children, depth + 1);
        }
    }
}

/// <summary>나무의 줄 하나 — 댓글 하나와 거기 달린 답글들.</summary>
public sealed class CommentNode(ImprovementComment comment)
{
    /// <summary>이 줄의 댓글.</summary>
    public ImprovementComment Comment { get; } = comment;

    /// <summary>이 댓글에 달린 답글들. 쓴 순서다.</summary>
    public List<CommentNode> Children { get; } = [];

    /// <summary>뿌리에서 몇 칸 들어왔는가(뿌리가 0). 깊이를 제한하지 않는다 — 제한하면 아래 답글이 사라지고 원인이 안 보인다. 들여쓰는 폭에 상한을 둔다(helpdesk.css).</summary>
    public int Depth { get; internal set; }

    /// <summary>이 줄을 포함해 아래로 몇 건인가. 「답글 n」에 쓴다.</summary>
    public int Total => 1 + Children.Sum(c => c.Total);
}
