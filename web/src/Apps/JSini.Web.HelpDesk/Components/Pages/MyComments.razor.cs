using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class MyComments
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "company")] public string? CompanyQuery { get; set; }
    [SupplyParameterFromQuery(Name = "all")] public bool? AllQuery { get; set; }

    private List<MyCommentItem> _comments = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var parameters = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(CompanyQuery)) parameters["companyId"] = CompanyQuery;
        if (AllQuery == true) parameters["all"] = true;

        _comments = await Api.GetListAsync<MyCommentItem>("comments/my", parameters);
        return _comments.Count;
    }, "작성한 댓글이 없습니다.", "댓글을 읽지 못했습니다");

    private void GoToDetail(MyCommentItem comment)
    {
        Navigation.NavigateTo($"/helpdesk/request/detail/{comment.RequestId}#comment-{comment.CommentId}");
    }

    private static MarkupString Body(MyCommentItem comment) =>
        new(NoticeHtml.Sanitize(comment.CommentText, ThumbnailImage, lazyLoadImages: true));

    private static string ThumbnailImage(string source)
    {
        if (!source.StartsWith($"{FileDownload.Path}/", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        var fileId = FileDownload.FileIdOf(source);
        return fileId is null ? source : FileDownload.ThumbnailUrlFor(fileId);
    }

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
