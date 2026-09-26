using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Shared;

public partial class DeviceRibbonEditor
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>어느 기기의 장식인가. 바뀌면 다시 읽는다.</summary>
    [Parameter, EditorRequired] public string DeviceId { get; set; } = string.Empty;

    private string? _loadedFor;

    private List<DeviceRibbon> _ribbons = [];
    private IReadOnlyList<MediaSource> _images = [];

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
            _ribbons = [];
            return -1;
        }

        // 이미지 목록은 기기와 무관하다. 한 번만 읽고, 읽어야 할 때는 장식과
        // 나란히 받는다 — 서로 기다릴 이유가 없다.
        var images = _images.Count == 0 ? Api.GetMediaSourcesAsync("IMAGE") : null;
        var ribbons = Api.GetDeviceRibbonsAsync(DeviceId);

        await Task.WhenAll(new Task?[] { images, ribbons }.OfType<Task>());

        if (images is not null) _images = images.Result;
        _ribbons = [.. ribbons.Result];

        return -1;
    }, string.Empty, "장식을 읽지 못했습니다");

    private void Add() => _ribbons.Add(new DeviceRibbon
    {
        DeviceId = DeviceId,
        MediaSourceId = _images.Count > 0 ? _images[0].Id : string.Empty,

        // 가운데 아래쯤에 적당한 크기로 놓는다. 0,0 에 0 크기로 두면
        // 저장해도 화면에 아무것도 안 보여 「저장이 안 됐다」로 읽힌다.
        PositionLeft = 10,
        PositionTop = 70,
        Width = 80,
        Height = 20,
        SortOrder = _ribbons.Count,
    });

    private void Remove(DeviceRibbon ribbon) => _ribbons.Remove(ribbon);

    private async Task SaveAsync()
    {
        if (_ribbons.Any(r => string.IsNullOrWhiteSpace(r.MediaSourceId)))
        {
            Say("이미지를 고르지 않은 줄이 있습니다.", NoticeTone.Warning);
            return;
        }

        var payload = _ribbons
            .Select(r => new DeviceRibbonUpsert
            {
                DeviceId = DeviceId,
                MediaSourceId = r.MediaSourceId,
                PositionLeft = r.PositionLeft,
                PositionTop = r.PositionTop,
                Width = r.Width,
                Height = r.Height,
                SortOrder = r.SortOrder,
                Remark = r.Remark,
            })
            .ToList();

        if (await RunAsync(
                () => Api.BulkSaveDeviceRibbonsAsync(DeviceId, payload),
                "저장했습니다.", "장식을 저장하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
