using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class WbsDiagramPage
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>칸 이름표. 여기 적은 순서가 표의 앞쪽 순서다.</summary>
    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["name"] = "WBS 항목",
        ["projectName"] = "프로젝트",
        ["startDate"] = "시작",
        ["endDate"] = "종료",
        ["progress"] = "진행률",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 다이어그램 목록을 주는 주소가 없다(위 주석 참고). WBS 항목을 보여 주고
        // 그림은 항목별로 `wbs-diagram/{wbsRid}` 에서 꺼낸다.
        var json = await Api.GetAsync<JsonElement>("wbs");
        _rows = JsonTable.From(json);
        return _rows.Rows.Count;
    }, "등록된 WBS 항목이 없습니다.", "WBS 항목을 읽지 못했습니다");
}
