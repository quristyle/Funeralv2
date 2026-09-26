using Microsoft.AspNetCore.Components;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class MobileStatus
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private string? _buildingId;
    private IReadOnlyList<FuneralStatus> _rows = [];

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartAutoRefresh();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetFuneralStatusesAsync(_buildingId);
        return _rows.Count;
    }, "조건에 맞는 빈소가 없습니다.", "빈소 현황을 읽지 못했습니다");

    /// <summary>자동 조회. 손에 들고 보는 화면이라 더 자주 맞아야 한다.</summary>
    protected override async Task RefreshAsync() =>
        _rows = await Api.GetFuneralStatusesAsync(_buildingId);
}
