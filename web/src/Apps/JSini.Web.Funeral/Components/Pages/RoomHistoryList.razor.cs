using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class RoomHistoryList
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>건물·층·호실 뒤에 이 화면만의 조건을 더 적는다(<c>BuildingFilter.SummaryExtra</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _keyword,
        SchSummary.Period(_from, _to),
        SchSummary.On(_inUse, "사용 중만"));

    private string? _buildingId;
    private string? _roomId;
    private string? _keyword;
    private DateTime? _from;
    private DateTime? _to;
    private bool _inUse;

    private IReadOnlyList<RoomHistory> _rows = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetRoomHistoriesAsync(
            _buildingId, _roomId, _keyword, _from, _to, _inUse ? true : null);
        return _rows.Count;
    }, "조건에 맞는 이력이 없습니다.", "사용 이력을 읽지 못했습니다");
}
