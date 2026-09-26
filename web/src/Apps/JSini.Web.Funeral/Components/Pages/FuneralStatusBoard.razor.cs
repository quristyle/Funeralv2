using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class FuneralStatusBoard
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>건물·층·호실 뒤에 이 화면만의 조건을 더 적는다(<c>BuildingFilter.SummaryExtra</c>).</summary>
    private string? ConditionSummary => SchSummary.On(_onlyInUse, "사용 중만");

    private string? _buildingId;
    private string? _floorId;
    private bool _onlyInUse;

    private StatusBoard? _board;
    private IReadOnlyList<JSini.Web.Funeral.Api.FuneralStatus> _rows = [];

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 요약과 목록을 한 번에 받는다. 따로 부르면 두 왕복 사이에 자료가 바뀌어
        // "사용 중 5" 인데 목록은 4줄인 상태가 생긴다.
        _board = await Api.GetFuneralStatusBoardAsync(_buildingId, _floorId);
        var rooms = _board?.Rooms ?? [];

        _rows = _onlyInUse ? [.. rooms.Where(r => r.Occupied)] : rooms;
        return _rows.Count;
    }, "조건에 맞는 빈소가 없습니다.", "빈소 현황을 읽지 못했습니다");

    /// <summary>자동 조회. 사무실에 띄워 두는 화면이라 스스로 맞아야 한다.</summary>
    protected override async Task RefreshAsync()
    {
        _board = await Api.GetFuneralStatusBoardAsync(_buildingId, _floorId);
        var rooms = _board?.Rooms ?? [];
        _rows = _onlyInUse ? [.. rooms.Where(r => r.Occupied)] : rooms;
    }
}
