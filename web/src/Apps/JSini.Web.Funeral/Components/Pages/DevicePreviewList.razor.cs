using Microsoft.AspNetCore.Components;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class DevicePreviewList
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private string? _buildingId;
    private string? _roomId;
    private IReadOnlyList<DevicePreview> _rows = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetDevicePreviewsAsync(_buildingId, _roomId);
        return _rows.Count;
    }, "조건에 맞는 장비가 없습니다.", "미리보기를 읽지 못했습니다");
}
