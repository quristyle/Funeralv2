using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class AsciiParser
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private string? _raw;
    private DataTable _rows = JsonTable.Empty;

    private Task ParseAsync()
    {
        if (string.IsNullOrWhiteSpace(_raw))
        {
            Say("풀 원문을 넣으십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var json = await Api.PostAsync<JsonElement>("utils/parse-ascii", new { content = _raw });
            _rows = JsonTable.From(json);
            return _rows.Rows.Count;
        }, "푼 결과가 없습니다. 원문 모양을 확인하십시오.", "풀지 못했습니다");
    }
}
