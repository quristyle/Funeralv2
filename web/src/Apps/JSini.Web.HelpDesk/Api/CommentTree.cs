namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 평평하게 받아 온 댓글 목록을 <b>부모-자식 나무</b>로 세운다.
/// </summary>
/// <remarks>
/// <para>
/// 서버는 한 요청의 댓글을 <c>createdAt</c> 순으로 한 줄씩 준다
/// (<c>GET requests/{id}/comments</c>). 이어 주는 값은
/// <see cref="ImprovementComment.ParentCommentId"/> 하나뿐이고, 깊이 제한이
/// 없다 — 댓글의 대댓글, 그 대댓글의 대댓글이 같은 칸으로 이어진다.
/// </para>
///
/// <para>
/// [고아를 버리지 않는다]
/// </para>
///
/// <para>
/// 부모를 가리키는데 그 부모가 목록에 없을 수 있다. 버리면 <b>그 아래 가지가
/// 통째로 화면에서 사라지고</b>, 쓴 사람에게는 「내 답글이 안 올라갔다」로
/// 보인다. 실제로는 들어가 있으므로 같은 말을 한 번 더 쓰게 된다.
/// 그래서 부모를 못 찾은 줄은 <b>뿌리로 올려서라도 보여 준다.</b>
/// </para>
///
/// <para>
/// [고리를 만나면 끊는다]
/// </para>
///
/// <para>
/// 서버가 막고는 있지만 자료가 스스로를 가리키거나(<c>parentCommentId = id</c>)
/// 두 줄이 서로를 가리키면 나무가 아니라 고리가 된다. 그 줄들은 어느 뿌리에도
/// 닿지 않으므로 <b>말없이 화면에서 빠지고</b>, 최악의 경우 그리는 쪽에서
/// 무한히 돈다. 위로 거슬러 올라가며 이미 본 줄을 다시 만나면 그 자리에서
/// 끊어 뿌리로 세운다.
/// </para>
/// </remarks>
public static class CommentTree
{
    /// <summary>
    /// 댓글 목록을 나무로 세운다. 돌려주는 것은 <b>뿌리 댓글</b>들이고,
    /// 같은 부모를 둔 것끼리는 쓴 순서(오래된 것이 먼저)다.
    /// </summary>
    public static IReadOnlyList<CommentNode> Build(IEnumerable<ImprovementComment>? comments)
    {
        if (comments is null)
        {
            return [];
        }

        // 순서를 여기서 한 번 정한다. 서버가 이미 `createdAt` 순으로 주지만
        // 같은 초에 들어간 둘의 앞뒤가 정해지지 않는다 — 번호로 갈라 둔다.
        var ordered = comments
            .Where(c => c is not null)
            .OrderBy(c => c.CreatedAt ?? DateTime.MinValue)
            .ThenBy(c => c.Id)
            .ToList();

        var nodes = new Dictionary<int, CommentNode>();

        foreach (var comment in ordered)
        {
            // 같은 번호가 두 줄 오면 앞엣것을 쓴다. 뒤엣것으로 덮으면 이미
            // 그 아래 붙여 둔 답글이 갈 곳을 잃는다.
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

    /// <summary>
    /// 이 댓글을 매달 부모. 없거나(뿌리) 못 찾거나 고리면 <c>null</c> —
    /// 그때는 부르는 쪽이 뿌리로 세운다.
    /// </summary>
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

    /// <summary>
    /// 뿌리에서 몇 칸 들어왔는가(뿌리가 0).
    ///
    /// <para>
    /// 화면이 들여쓰기에 쓴다. <b>깊이를 제한하지는 않는다</b> — 제한하면
    /// 그 아래 답글이 화면에서 사라지는데, 자료에는 남아 있어서 원인이
    /// 보이지 않는다. 대신 들여쓰는 폭에 상한을 둔다(<c>helpdesk.css</c>).
    /// </para>
    /// </summary>
    public int Depth { get; internal set; }

    /// <summary>이 줄을 포함해 아래로 몇 건인가. 「답글 n」에 쓴다.</summary>
    public int Total => 1 + Children.Sum(c => c.Total);
}
