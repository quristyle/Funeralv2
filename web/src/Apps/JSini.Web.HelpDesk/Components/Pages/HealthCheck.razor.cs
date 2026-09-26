using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class HealthCheck
{
    [Inject] private OadrApi Oadr { get; set; } = default!;

    private DataTable _rows = JsonTable.Empty;

    /// <summary>옛 화면과 같은 60초. 설비가 살아 있는지 지켜보는 자리다.</summary>
    protected override TimeSpan RefreshInterval => TimeSpan.FromSeconds(60);

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var json = await Oadr.GetServerReportAsync<JsonElement>("HEALTH_CHECK");
        _rows = JsonTable.From(json);
        return _rows.Rows.Count;
    }, "조회 결과가 없습니다.", "헬스체크를 읽지 못했습니다");

    /// <summary>자동 조회. 안내 줄을 건드리지 않는다.</summary>
    protected override async Task RefreshAsync()
    {
        var json = await Oadr.GetServerReportAsync<JsonElement>("HEALTH_CHECK");
        _rows = JsonTable.From(json);
    }
}
