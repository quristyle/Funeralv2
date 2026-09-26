using Microsoft.AspNetCore.Components;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Shared;

public partial class DeviceConfigEditor
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>어느 기기의 설정인가. 바뀌면 다시 읽는다.</summary>
    [Parameter, EditorRequired] public string DeviceId { get; set; } = string.Empty;

    private string? _loadedFor;
    private DeviceConfig? _form;
    private bool _isNew;

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
            _form = null;
            return -1;
        }

        var saved = await Api.GetDeviceConfigAsync(DeviceId);

        // 한 번도 설정하지 않은 기기는 행이 없다. 빈 화면 대신 기본값을
        // 보여 준다 — 어차피 저장하면 만들어질 값이다.
        _isNew = saved is null;

        _form = saved ?? new DeviceConfig
        {
            DeviceId = DeviceId,
            Volume = 50,
            Brightness = 80,
        };

        return -1;
    }, string.Empty, "기기 동작 설정을 읽지 못했습니다");

    private async Task SaveAsync()
    {
        if (_form is null)
        {
            return;
        }

        _form.DeviceId = DeviceId;

        if (await RunAsync(
                () => Api.UpsertDeviceConfigAsync(_form),
                "저장했습니다.", "기기 동작 설정을 저장하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
