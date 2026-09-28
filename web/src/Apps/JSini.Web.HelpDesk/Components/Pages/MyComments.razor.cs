using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class MyComments
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private List<MyCommentItem> _comments = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _comments = await Api.GetListAsync<MyCommentItem>("comments/my");
        return _comments.Count;
    }, "작성한 댓글이 없습니다.", "댓글을 읽지 못했습니다");

    private static MarkupString Body(MyCommentItem comment) =>
        new(NoticeHtml.Sanitize(comment.CommentText));

    private static string RequestTitle(MyCommentItem comment) =>
        string.IsNullOrWhiteSpace(comment.RequestTitle)
            ? $"요청 #{comment.RequestId}"
            : comment.RequestTitle;

    private static string When(DateTime createdAt)
    {
        var local = createdAt.Kind == DateTimeKind.Utc ? createdAt.ToLocalTime() : createdAt;
        return local.ToString("yyyy-MM-dd HH:mm");
    }
}
