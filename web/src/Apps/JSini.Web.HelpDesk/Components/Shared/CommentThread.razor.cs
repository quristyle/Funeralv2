using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class CommentThread
{
    /// <summary>이 층에 늘어놓을 댓글들. 쓴 순서다.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<CommentNode> Nodes { get; set; } = [];

    /// <summary>답글을 달 수 있는가. 계정 연결이 없으면 거짓이다.</summary>
    [Parameter] public bool CanReply { get; set; }

    /// <summary>지금 답글 칸이 열려 있는 댓글 번호. 없으면 <c>null</c>.</summary>
    [Parameter] public int? ReplyTo { get; set; }

    /// <summary>답글 칸을 열고 닫아 달라는 부탁. 값은 열 댓글 번호(<c>null</c> 이면 닫기).</summary>
    [Parameter] public EventCallback<int?> OnToggle { get; set; }

    /// <summary>
    /// 답글을 남긴다. 들어갔으면 참 — 그때만 편집기가 제 칸을 비운다.
    /// </summary>
    [Parameter] public Func<int, string, Task<bool>>? OnReply { get; set; }

    private bool IsOpen(CommentNode node) => CanReply && ReplyTo == node.Comment.Id;

    private Task ToggleAsync(CommentNode node) =>
        OnToggle.InvokeAsync(IsOpen(node) ? null : node.Comment.Id);

    private Task<bool> Submit(int parentId, string html) =>
        OnReply is null ? Task.FromResult(false) : OnReply(parentId, html);

    /// <summary>
    /// 작성자 이름. 서버가 담당자·고객을 <c>author</c> 하나로 풀어 준다.
    /// 지워진 계정이면 그 칸이 비어 오므로 번호라도 보여 준다.
    /// </summary>
    private static string AuthorName(CommentNode node) =>
        node.Comment.Author?.UserName is { Length: > 0 } name
            ? name
            : node.Comment.CreatedBy is { Length: > 0 } login
                ? login
                : $"#{node.Comment.AuthorId}";

    private static bool IsAdmin(CommentNode node) =>
        string.Equals(node.Comment.AuthorType, "admin", StringComparison.OrdinalIgnoreCase);

    private static string AuthorKind(CommentNode node) => IsAdmin(node) ? "담당자" : "고객";

    private static string AuthorClass(CommentNode node) =>
        IsAdmin(node) ? "jsini-badge--on" : "";

    /// <summary>
    /// 쓴 때. <b>UTC 로 오면 이 자리에서 옮긴다</b> — 서버는
    /// <c>DateTime.UtcNow</c> 로 적고 DB 칸도 <c>timestamptz</c> 라,
    /// 그대로 찍으면 아홉 시간 전에 쓴 것으로 보인다.
    /// </summary>
    private static string When(CommentNode node)
    {
        if (node.Comment.CreatedAt is not { } at)
        {
            return "-";
        }

        var local = at.Kind == DateTimeKind.Utc ? at.ToLocalTime() : at;
        return local.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>
    /// 본문을 화면에 넣을 수 있는 HTML 로 만든다.
    /// </summary>
    /// <remarks>
    /// <c>MarkupString</c> 은 <b>거른 다음에만</b> 쓴다. 댓글을 쓰는 사람에는
    /// 고객이 들어 있으므로, 거르지 않으면 그 글이 담당자 브라우저에서 그대로
    /// 실행된다. 요청 본문과 같은 한 벌을 쓴다 — 거기에
    /// <c>/api/file/…</c> 주소를 셸 중계 경로로 옮기는 일까지 들어 있어,
    /// 붙여넣은 그림이 포털(:5557)에서도 그대로 보인다.
    /// </remarks>
    private static MarkupString Body(CommentNode node) =>
        new(NoticeHtml.Sanitize(node.Comment.CommentText));
}
