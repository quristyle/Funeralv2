using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Shared;

public partial class DeviceOverlayEditor
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private sealed record Option(string Code, string Name);

    private static readonly Option[] Aligns =
    [
        new("left", "왼쪽"),
        new("center", "가운데"),
        new("right", "오른쪽"),
    ];

    private static readonly Option[] Weights =
    [
        new("normal", "보통"),
        new("bold", "굵게"),
    ];

    /// <summary>어느 기기의 문구인가. 바뀌면 다시 읽는다.</summary>
    [Parameter, EditorRequired] public string DeviceId { get; set; } = string.Empty;

    private string? _loadedFor;
    private List<DeviceTextOverlay> _overlays = [];

    protected override async Task OnParametersSetAsync()
    {
        if (string.Equals(_loadedFor, DeviceId, StringComparison.Ordinal))
        {
            return;
        }

        _loadedFor = DeviceId;
        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        if (string.IsNullOrEmpty(DeviceId))
        {
            _overlays = [];
            return -1;
        }

        var loaded = await Api.GetDeviceTextOverlaysAsync(DeviceId);
        _overlays = [.. loaded];

        return -1;
    }, string.Empty, "문구를 읽지 못했습니다");

    private void Add() => _overlays.Add(new DeviceTextOverlay
    {
        DeviceId = DeviceId,

        // 가운데 아래쯤. 0 크기로 두면 저장해도 화면에 안 보여
        // 「저장이 안 됐다」로 읽힌다.
        PositionLeft = 10,
        PositionTop = 80,
        Width = 80,
        Height = 10,
        SortOrder = _overlays.Count,
    });

    private void Remove(DeviceTextOverlay overlay) => _overlays.Remove(overlay);

    private async Task SaveAsync()
    {
        if (_overlays.Any(o => string.IsNullOrWhiteSpace(o.TextContent)))
        {
            Say("내용이 비어 있는 문구가 있습니다.", NoticeTone.Warning);
            return;
        }

        foreach (var overlay in _overlays)
        {
            overlay.DeviceId = DeviceId;
        }

        if (await RunAsync(
                () => Api.BulkSaveDeviceTextOverlaysAsync(DeviceId, _overlays),
                "저장했습니다.", "문구를 저장하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
