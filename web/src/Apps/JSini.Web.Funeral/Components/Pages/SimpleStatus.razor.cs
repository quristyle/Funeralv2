using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class SimpleStatus
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>건물·층·호실 뒤에 이 화면만의 조건을 더 적는다(<c>BuildingFilter.SummaryExtra</c>).</summary>
    private string? ConditionSummary => SchSummary.On(_onlyInUse, "사용 중만");

    private string? _buildingId;
    private string? _floorId;
    private bool _onlyInUse = true;
    private IReadOnlyList<FuneralStatus> _rows = [];

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetFuneralStatusesAsync(_buildingId, _floorId, _onlyInUse ? true : null);
        return _rows.Count;
    }, "조건에 맞는 빈소가 없습니다.", "빈소 현황을 읽지 못했습니다");

    /// <summary>자동 조회. 안내 줄을 건드리지 않는다 — 까닭은 `AutoRefreshPage` 에 있다.</summary>
    protected override async Task RefreshAsync() =>
        _rows = await Api.GetFuneralStatusesAsync(_buildingId, _floorId, _onlyInUse ? true : null);
}
