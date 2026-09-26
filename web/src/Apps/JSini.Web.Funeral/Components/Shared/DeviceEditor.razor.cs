using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Shared;

public partial class DeviceEditor
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private sealed record Option(string Code, string Name);

    private static readonly Option[] Types =
    [
        new("FUNERAL_PORTRAIT", "영정사진"),
        new("MULTIMEDIA", "멀티미디어"),
        new("ROOM_GUIDE", "층별 안내"),
        new("ENTRANCE_GUIDE", "입구 안내"),
        new("KIOSK", "키오스크"),
        new("DID", "DID"),
    ];

    [Parameter] public bool Visible { get; set; }

    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>고칠 기기. <c>null</c> 이면 새로 등록한다.</summary>
    [Parameter] public string? DeviceId { get; set; }

    [Parameter] public EventCallback OnSaved { get; set; }

    private Device? _form;
    private string? _loadedFor;

    private IReadOnlyList<Building> _buildings = [];
    private IReadOnlyList<Floor> _floors = [];
    private IReadOnlyList<Room> _rooms = [];

    private bool IsNew => string.IsNullOrEmpty(DeviceId);

    protected override async Task OnParametersSetAsync()
    {
        if (!Visible)
        {
            _loadedFor = null;
            return;
        }

        var key = DeviceId ?? "(new)";
        if (string.Equals(_loadedFor, key, StringComparison.Ordinal))
        {
            return;
        }

        _loadedFor = key;
        await OpenAsync();
    }

    private Task OpenAsync() => LoadAsync(async () =>
    {
        if (_buildings.Count == 0)
        {
            _buildings = await Api.GetBuildingsAsync();
        }

        _form = IsNew
            ? new Device { DeviceType = "FUNERAL_PORTRAIT" }
            : await Api.GetDeviceAsync(DeviceId!) ?? new Device { Id = DeviceId! };

        // 고른 기기가 이미 붙어 있는 자리에 맞춰 아래 목록을 채운다.
        await LoadFloorsAsync(_form.BuildingId);
        await LoadRoomsAsync(_form.BuildingId, _form.FloorId);

        return 1;
    }, string.Empty, "기기 자료를 읽지 못했습니다");

    private async Task LoadFloorsAsync(string? buildingId)
    {
        _floors = string.IsNullOrEmpty(buildingId)
            ? []
            : await Api.GetFloorsAsync(buildingId);
    }

    private async Task LoadRoomsAsync(string? buildingId, string? floorId)
    {
        // **이름을 붙여 넘긴다.** 첫 인자가 `companyId` 라, 자리로 넘기면
        // 건물이 회사 자리에 들어가 목록이 통째로 빈다.
        _rooms = string.IsNullOrEmpty(buildingId)
            ? []
            : await Api.GetRoomsAsync(buildingId: buildingId, floorId: floorId);
    }

    private async Task PickBuildingAsync(string? buildingId)
    {
        _form!.BuildingId = buildingId;

        // 위를 바꾸면 아래를 푼다. 남겨 두면 다른 건물의 층·호실에 기기가
        // 붙고, 저장은 되며 현황판에서만 이상해 보인다.
        _form.FloorId = null;
        _form.RoomId = null;

        await LoadFloorsAsync(buildingId);
        await LoadRoomsAsync(buildingId, null);
    }

    private async Task PickFloorAsync(string? floorId)
    {
        _form!.FloorId = floorId;
        _form.RoomId = null;

        await LoadRoomsAsync(_form.BuildingId, floorId);
    }

    private async Task SaveAsync()
    {
        if (_form is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_form.Name))
        {
            Say("기기명을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        if (IsNew && string.IsNullOrWhiteSpace(_form.Code))
        {
            Say("코드를 넣으십시오. 원격 명령이 이 값을 씁니다.", NoticeTone.Warning);
            return;
        }

        var saved = await RunAsync(
            () => IsNew
                ? Api.CreateDeviceAsync(_form)
                : Api.UpdateDeviceAsync(DeviceId!, _form),
            IsNew ? "등록했습니다." : "저장했습니다.",
            IsNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (saved)
        {
            await OnVisibleChanged(false);
            await OnSaved.InvokeAsync();
        }
    }

    private async Task OnVisibleChanged(bool visible)
    {
        Visible = visible;

        if (!visible)
        {
            _form = null;
            _loadedFor = null;
        }

        await VisibleChanged.InvokeAsync(visible);
    }
}
