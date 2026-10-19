using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Shared;

public partial class BuildingFilter
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    [Parameter] public string? BuildingId { get; set; }
    [Parameter] public EventCallback<string?> BuildingIdChanged { get; set; }

    [Parameter] public string? FloorId { get; set; }
    [Parameter] public EventCallback<string?> FloorIdChanged { get; set; }

    [Parameter] public string? RoomId { get; set; }
    [Parameter] public EventCallback<string?> RoomIdChanged { get; set; }

    [Parameter] public bool ShowFloor { get; set; }
    [Parameter] public bool ShowRoom { get; set; }

    /// <summary>조건줄 오른쪽에 화면별 조건을 더 붙일 자리.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>이 화면만의 단추(등록·일괄처리 따위). 조건이 아니라 조작이라 오른쪽 끝 묶음에 붙는다. 조건 사이에 끼우면 조건이 늘 때마다 밀린다.</summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>조회 단추를 눌렀을 때.</summary>
    [Parameter] public EventCallback Search { get; set; }

    /// <summary>휴대폰에서 접혔을 때 머리줄에 더 적을 글. 건물·층·호실은 이 판이 적고, 화면이 더한 조건은 RenderFragment 라 여기서 받는다.</summary>
    [Parameter] public string? SummaryExtra { get; set; }

    /// <summary>접힌 조회줄에 적을 지금 조건. 안 고른 칸은 「전체」다.</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.NameOf(_buildings, b => b.Id, b => b.Name, BuildingId),
        ShowFloor ? SchSummary.NameOf(_floors, f => f.Id, f => f.Name, FloorId) : null,
        ShowRoom ? SchSummary.NameOf(_rooms, r => r.Id, r => r.Name, RoomId) : null,
        SummaryExtra);

    private IReadOnlyList<Building> _buildings = [];
    private IReadOnlyList<Floor> _floors = [];
    private IReadOnlyList<Room> _rooms = [];

    private bool _hasLoaded;
    private string? _loadedBuildingId;
    private string? _loadedFloorId;

    protected override async Task OnInitializedAsync()
        => _buildings = await Api.GetBuildingsAsync();

    /// <summary>건물이 바뀌면 층·호실을 다시 읽는다. _loadedBuildingId 로 한 번만 읽는다. OnParametersSetAsync 는 부모가 그릴 때마다 불려 그냥 두면 게이트웨이로 계속 나간다.</summary>
    protected override async Task OnParametersSetAsync()
    {
        if (!ShowFloor && !ShowRoom) return;

        var buildingChanged = !_hasLoaded || _loadedBuildingId != BuildingId;
        var floorChanged = !_hasLoaded || _loadedFloorId != FloorId;

        if (!buildingChanged && !floorChanged) return;

        _hasLoaded = true;

        if (buildingChanged)
        {
            await LoadFloorsAndRoomsAsync();
        }
        else if (floorChanged && ShowRoom)
        {
            await LoadRoomsAsync();
        }
    }

    private async Task OnBuildingChangedAsync(string? value)
    {
        if (BuildingId == value) return;

        BuildingId = value;
        await BuildingIdChanged.InvokeAsync(value);

        if (FloorId is not null)
        {
            FloorId = null;
            await FloorIdChanged.InvokeAsync(null);
        }

        if (RoomId is not null)
        {
            RoomId = null;
            await RoomIdChanged.InvokeAsync(null);
        }

        await LoadFloorsAndRoomsAsync();
    }

    private async Task OnFloorChangedAsync(string? value)
    {
        if (FloorId == value) return;

        FloorId = value;
        await FloorIdChanged.InvokeAsync(value);

        if (ShowRoom)
        {
            if (RoomId is not null)
            {
                RoomId = null;
                await RoomIdChanged.InvokeAsync(null);
            }

            await LoadRoomsAsync();
        }
    }

    private async Task OnRoomChangedAsync(string? value)
    {
        if (RoomId == value) return;

        RoomId = value;
        await RoomIdChanged.InvokeAsync(value);
    }

    private async Task LoadFloorsAndRoomsAsync()
    {
        _loadedBuildingId = BuildingId;
        _loadedFloorId = FloorId;

        var floorsTask = ShowFloor ? Api.GetFloorsAsync(BuildingId) : null;
        var roomsTask = ShowRoom
            ? Api.GetRoomsAsync(buildingId: BuildingId, floorId: FloorId)
            : null;

        await Task.WhenAll(new Task?[] { floorsTask, roomsTask }.OfType<Task>());

        if (floorsTask is not null) _floors = floorsTask.Result;
        if (roomsTask is not null) _rooms = roomsTask.Result;
    }

    private async Task LoadRoomsAsync()
    {
        _loadedFloorId = FloorId;
        _rooms = await Api.GetRoomsAsync(buildingId: BuildingId, floorId: FloorId);
    }
}
