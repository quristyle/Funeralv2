using Microsoft.AspNetCore.Components;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class DeviceList
{
    [Inject] private FuneralApi Api { get; set; } = default!;
    [Inject] private DeviceStatusRelay Status { get; set; } = default!;

    /// <summary>속성 묶음 하나. <c>Relevant</c> 는 이 장비 유형이 쓰는 묶음인가.</summary>
    private sealed record AttributeSection(string Key, string Label, bool Relevant);

    private sealed record Option(string Code, string Name);

    /// <summary>
    /// 묶음별로 어느 장비 유형이 쓰는가. 비우면 모든 유형이 쓴다.
    ///
    /// <b>배경 이미지는 영정사진 장비도 쓴다</b> — 그래서 media 묶음에 유형이 둘이다.
    /// </summary>
    private static readonly (string Key, string Label, string[] Types)[] SectionDefs =
    [
        ("layout", "화면 배치", []),
        ("memorial", "영정사진 · 추모", ["FUNERAL_PORTRAIT"]),
        ("media", "사진 · 영상 · 음악", ["MULTIMEDIA", "FUNERAL_PORTRAIT", "DID"]),
        ("floorGuide", "층별 안내판", ["ROOM_GUIDE"]),
        ("kiosk", "입구 정보 · 키오스크", ["KIOSK", "ENTRANCE_GUIDE"]),
        ("remark", "비고", []),
    ];

    private static readonly Option[] Orientations =
    [
        new("LANDSCAPE", "가로"),
        new("PORTRAIT", "세로"),
    ];

    private static readonly Option[] Alignments =
    [
        new("HORIZONTAL", "가로"),
        new("VERTICAL", "세로"),
    ];

    private static readonly Option[] PhotoEffects =
    [
        new("FADE", "페이드"),
        new("SLIDE", "슬라이드"),
        new("NONE", "없음"),
    ];

    private static readonly Option[] VerticalAligns =
    [
        new("TOP", "위"),
        new("CENTER", "가운데"),
        new("BOTTOM", "아래"),
    ];

    private static readonly Option[] HorizontalAligns =
    [
        new("LEFT", "왼쪽"),
        new("CENTER", "가운데"),
        new("RIGHT", "오른쪽"),
    ];

    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.Ordinal)
    {
        ["FUNERAL_PORTRAIT"] = "영정사진",
        ["MULTIMEDIA"] = "멀티미디어",
        ["ROOM_GUIDE"] = "층별 안내",
        ["ENTRANCE_GUIDE"] = "입구 안내",
        ["KIOSK"] = "키오스크",
        ["DID"] = "DID",
    };

    private string? _buildingId;
    private string? _floorId;
    private string? _roomId;
    private IReadOnlyList<Device> _devices = [];

    private bool _editing;
    private string? _editingId;

    private Device? _selected;
    private DeviceAttribute? _attribute;
    private bool _isNewAttribute;

    private IReadOnlyList<MediaSource> _videos = [];
    private IReadOnlyList<MediaSource> _audios = [];
    private IReadOnlyList<MediaSource> _backgrounds = [];

    /// <summary>장비 상태 방송 구독. 화면이 사라질 때 놓는다.</summary>
    private IDisposable? _statusWatch;

    protected override async Task OnInitializedAsync()
    {
        // 목록을 읽기 전에 듣기 시작한다. 읽는 사이에 온 방송도 받는다 —
        // 방송이 목록보다 먼저 오면 그 장비는 다음 방송까지 옛 상태로 남는다.
        _statusWatch = Status.Subscribe(OnStatusChanged);

        await ReloadAsync();
    }

    /// <summary>
    /// 장비가 붙거나 떨어졌다.
    ///
    /// <para>
    /// <b>목록을 다시 읽지 않는다.</b> 원본(Vue)은 방송을 받을 때마다 목록을
    /// 다시 조회했다(0.5초로 묶어서). 정전 복구처럼 한 건물이 동시에 켜지면
    /// 그 한 번이 수십 장비의 조회가 된다. 바뀐 것은 상태 한 칸뿐이라
    /// 들고 있는 자료에서 그 칸만 고친다.
    /// </para>
    ///
    /// <para>
    /// 허브 스레드에서 불린다 — <c>InvokeAsync</c> 로 회로에 넘겨야 한다.
    /// </para>
    /// </summary>
    private void OnStatusChanged(string deviceCode, string status)
    {
        var device = _devices.FirstOrDefault(d =>
            string.Equals(d.Code, deviceCode, StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            return;
        }

        device.Status = status;

        if (string.Equals(status, "ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            device.LastSeenAt = DateTime.Now;
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _selected = null;
        _attribute = null;

        // **이름을 붙여 넘긴다.** 첫 인자가 `companyId` 라, 자리로 넘기면
        // 건물이 회사 자리에 들어가 조건이 통째로 어긋난다. 실제로 그랬다 —
        // 건물·층·호실을 골라도 목록이 줄지 않았고, 화면만 보면 알 수 없다.
        _devices = await Api.GetDevicesAsync(
            buildingId: _buildingId, floorId: _floorId, roomId: _roomId);
        return _devices.Count;
    }, "조건에 맞는 기기가 없습니다.", "기기 목록을 읽지 못했습니다");

    /// <summary>
    /// 고른 기기의 화면 표시 설정.
    ///
    /// 자료 목록은 <b>처음 한 번만</b> 읽는다 — 기기를 바꿀 때마다 다시 읽으면
    /// 목록을 훑는 동안 같은 세 요청이 계속 나간다.
    /// </summary>
    private Task SelectAsync(Device? device)
    {
        _selected = device;
        _attribute = null;

        if (device is null)
        {
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            if (_videos.Count == 0 && _audios.Count == 0 && _backgrounds.Count == 0)
            {
                var videos = Api.GetMediaSourcesAsync("VIDEO");
                var audios = Api.GetMediaSourcesAsync("AUDIO");
                var backgrounds = Api.GetMediaSourcesAsync("BACKGROUND");

                await Task.WhenAll(videos, audios, backgrounds);

                _videos = videos.Result;
                _audios = audios.Result;
                _backgrounds = backgrounds.Result;
            }

            var saved = await Api.GetDeviceAttributeAsync(device.Id);

            // 한 번도 설정하지 않은 기기는 속성 행이 없다. 빈 화면 대신
            // 기본값을 채운 새 속성을 보여 준다 — 어차피 저장하면 만들어진다.
            _isNewAttribute = saved is null;
            _attribute = saved ?? Defaults(device.Id);

            return 1;
        }, "설정을 읽지 못했습니다.", "설정을 읽지 못했습니다");
    }

    private static DeviceAttribute Defaults(string deviceId) => new()
    {
        DeviceId = deviceId,
        DisplayOrientation = "LANDSCAPE",
        PortraitOrientation = "HORIZONTAL",
        VideoOrientation = "HORIZONTAL",
        BackgroundOrientation = "HORIZONTAL",
        ContentIntervalSec = 10,
        ScreensaverTimeoutSec = 300,
        MemorialPhotoEffect = "FADE",
        PhotoVerticalAlignment = "TOP",
        PhotoHorizontalAlignment = "CENTER",
        IsDeceasedNameVisible = true,
        IsMediaLoop = true,
        FloorGuideRefreshSec = 60,
        MusicVolume = 50,
    };

    private async Task SaveAttributeAsync()
    {
        if (_attribute is null)
        {
            return;
        }

        var saved = await RunAsync(
            () => Api.UpsertDeviceAttributeAsync(_attribute),
            "저장했습니다. 플레이어가 다음 갱신 때 반영합니다.",
            "설정을 저장하지 못했습니다");

        if (saved)
        {
            await SelectAsync(_selected);
        }
    }

    // ── 자료를 고르면 켜고, 지우면 끈다 ─────────────────────
    //
    // 「켬」 스위치와 「무엇을」이 따로 놀면 켜 둔 채 빈 화면이 나온다.
    // 실제로 옛 화면이 그 짝을 손으로 맞추고 있었다.

    private void PickVideo(string? id)
    {
        _attribute!.VideoId = id;
        _attribute.IsVideoEnabled = !string.IsNullOrEmpty(id);
    }

    private void PickMusic(string? id)
    {
        _attribute!.MusicId = id;
        _attribute.IsMusicEnabled = !string.IsNullOrEmpty(id);
    }

    private void PickBackground(string? id)
    {
        _attribute!.BackgroundImageId = id;
        _attribute.IsBackgroundImageEnabled = !string.IsNullOrEmpty(id);
    }

    /// <summary>유형에 해당하는 묶음이 앞, 무관한 묶음이 뒤.</summary>
    private static List<AttributeSection> Sections(string? deviceType)
    {
        var relevant = new List<AttributeSection>();
        var others = new List<AttributeSection>();

        foreach (var (key, label, types) in SectionDefs)
        {
            var hit = types.Length == 0 || types.Contains(deviceType ?? string.Empty, StringComparer.Ordinal);
            (hit ? relevant : others).Add(new AttributeSection(key, label, hit));
        }

        return [.. relevant, .. others];
    }

    private static string TypeName(string? code) =>
        code is not null && TypeNames.TryGetValue(code, out var name) ? name : code ?? string.Empty;

    private void OpenNew()
    {
        _editingId = null;
        _editing = true;
    }

    private void OpenEdit(Device device)
    {
        _editingId = device.Id;
        _editing = true;
    }

    /// <summary>
    /// 기기를 지운다.
    ///
    /// 화면 표시 설정·장식·문구가 이 기기에 딸려 있다. 서버가 함께 정리하거나
    /// 남은 것이 있으면 막는다 — 화면이 미리 지우려 들지 않는다.
    /// </summary>
    private async Task DeleteAsync(Device device)
    {
        if (await RunAsync(() => Api.DeleteDeviceAsync(device.Id),
                $"{device.Name} 을 지웠습니다.", "지우지 못했습니다"))
        {
            if (string.Equals(_selected?.Id, device.Id, StringComparison.Ordinal))
            {
                _selected = null;
                _attribute = null;
            }

            await ReloadAsync();
        }
    }

    private Task PowerAsync(string code, string state) => RunAsync(
        () => Api.SetDeviceScreenPowerAsync(code, state),
        $"{code} 화면을 {(state == "ON" ? "켜" : "꺼")}도록 보냈습니다.",
        "명령을 보내지 못했습니다");

    private Task RestartAsync(string code) => RunAsync(
        () => Api.RestartDeviceAppAsync(code),
        $"{code} 플레이어를 다시 시작하도록 보냈습니다.",
        "명령을 보내지 못했습니다");

    /// <summary>
    /// 플레이어에게 지금 새 판을 확인하라고 시킨다.
    ///
    /// 서버는 파일을 나르지 않는다 — 릴리스 조회와 판정을 플레이어가 스스로
    /// 한다. 새 판이 없으면 아무 일도 일어나지 않으므로 확인 창을 두지 않는다.
    /// </summary>
    private Task UpdateNowAsync(string code) => RunAsync(
        () => Api.UpdateDeviceNowAsync(code),
        $"{code} 에 새 판 확인을 보냈습니다. 새 판이 없으면 그대로 둡니다.",
        "명령을 보내지 못했습니다");

    /// <summary>
    /// 그만 듣는다. <b>빠뜨리면 죽은 회로를 계속 붙들고 있게 된다</b> —
    /// 연결은 프로세스에 하나뿐이라 구독 목록만 쌓인다.
    /// </summary>
    public void Dispose() => _statusWatch?.Dispose();
}
