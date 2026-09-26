using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ReportCapacity
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["name"] = "담당자",
        ["openCount"] = "진행 중",
        ["totalCount"] = "전체",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var json = await Api.GetAsync<JsonElement>("dashboard/admins/workload");
        _rows = JsonTable.From(json);
        return _rows.Rows.Count;
    }, "집계할 자료가 없습니다.", "용량 계획를 읽지 못했습니다");
}
