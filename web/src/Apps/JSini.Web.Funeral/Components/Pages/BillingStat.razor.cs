using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class BillingStat
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>건물·층·호실 뒤에 이 화면만의 조건을 더 적는다(<c>BuildingFilter.SummaryExtra</c>).</summary>
    private string ConditionSummary => SchSummary.Of(SchSummary.Period(_from, _to));

    private string? _buildingId;
    private DateTime? _from;
    private DateTime? _to;

    private IReadOnlyList<Billing> _rows = [];
    private StatSummary? _summary;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 요약과 목록을 나란히 부른다. 순서에 의미가 없다.
        var rows = Api.GetBillingStatsAsync(_buildingId, _from, _to);
        var summary = Api.GetStatSummaryAsync(_buildingId, _from, _to);

        await Task.WhenAll(rows, summary);

        _rows = rows.Result;
        _summary = summary.Result;
        return _rows.Count;
    }, "조건에 맞는 자료가 없습니다.", "요금 내역을 읽지 못했습니다");
}
