using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class FmsLog
{
    [Inject] private OadrApi Oadr { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var json = await Oadr.GetServerReportAsync<JsonElement>("FMS_LOG");
        _rows = JsonTable.From(json);
        return _rows.Rows.Count;
    }, "조회 결과가 없습니다.", "FMS 연동 로그를 읽지 못했습니다");
}
