using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class MyComments
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["requestTitle"] = "요청",
        ["content"] = "내용",
        ["createdAt"] = "작성일",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var json = await Api.GetAsync<JsonElement>("comments/my");
        _rows = JsonTable.From(json);
        return _rows.Rows.Count;
    }, "작성한 댓글이 없습니다.", "댓글을 읽지 못했습니다");
}
