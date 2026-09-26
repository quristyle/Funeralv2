using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class ReportListPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    private string ConditionSummary =>
        SchSummary.NameOf(CargoCodes.ReportStatusOptions, o => o.Value, o => o.Text, _status);

    private string? _status;
    private IReadOnlyList<ReportInfo> _all = [];

    private IReadOnlyList<ReportInfo> Shown =>
        string.IsNullOrEmpty(_status) ? _all : [.. _all.Where(r => r.Status == _status)];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetMyReportsAsync();
        return Shown.Count;
    }, "신고한 것이 없습니다.", "내 신고를 읽지 못했습니다");
}
