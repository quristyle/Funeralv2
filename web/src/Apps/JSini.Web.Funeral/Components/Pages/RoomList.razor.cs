using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class RoomList
{
    [Inject] private FuneralApi Api { get; set; } = default!;
    [Inject] private CommonCodeClient Codes { get; set; } = default!;

    private string? _buildingId;
    private string? _floorId;

    private IReadOnlyList<Room> _rooms = [];
    private IReadOnlyList<Building> _buildings = [];
    private IReadOnlyList<Floor> _floors = [];
    private IReadOnlyList<CommonCode> _roomTypes = [];

    /// <summary>코드값 → 이름. 표가 쓴다.</summary>
    private Func<string?, string> _roomTypeLabel = v => v ?? string.Empty;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 넷을 나란히. 서로 기다릴 이유가 없다 — 조건(_buildingId · _floorId)은
        // 이미 화면에 있는 값이라 앞의 조회 결과를 쓰지 않는다.
        var buildings = Api.GetBuildingsAsync();
        var floors = Api.GetFloorsAsync();
        var types = Codes.GetAsync("ROOM_TYPE");
        var rooms = Api.GetRoomsAsync(buildingId: _buildingId, floorId: _floorId);

        await Task.WhenAll(buildings, floors, types, rooms);

        _buildings = buildings.Result;
        _floors = floors.Result;
        _roomTypes = types.Result;
        _rooms = rooms.Result;

        // **옮기개를 따로 부르지 않는다.** `LabelerAsync` 는 같은 목록을 다시
        // 받는데, 차례로 부를 때는 통에 맞아 공짜지만 나란히 부르면 둘 다
        // 통을 지나쳐 두 번 받는다.
        _roomTypeLabel = CommonCodeClient.Labeler(_roomTypes);

        return _rooms.Count;
    }, "조건에 맞는 호실이 없습니다.", "호실 목록을 읽지 못했습니다");

    /// <summary>고른 건물의 층만. 건물을 안 골랐으면 전부.</summary>
    private IReadOnlyList<Floor> FloorsOf(string? buildingId) =>
        string.IsNullOrEmpty(buildingId)
            ? _floors
            : [.. _floors.Where(f => f.BuildingId == buildingId)];

    private void FillNew(Room r)
    {
        // 조건줄에서 고른 것을 그대로 물려준다. 안 넣으면 어느 층에도 속하지
        // 않은 호실이 만들어져 목록에서 사라진다.
        r.BuildingId = _buildingId;
        r.FloorId = _floorId;
        r.Status = "ACTIVE";
        r.RoomType = _roomTypes.FirstOrDefault()?.CodeValue ?? string.Empty;

        // 마지막 뒤에 붙인다. 0 으로 두면 새 호실이 목록 맨 앞에 끼어든다.
        r.SortOrder = _rooms.Count == 0 ? 1 : _rooms.Max(x => x.SortOrder) + 1;
    }

    private Task SaveAsync((Room Item, bool IsNew) e)
    {
        if (string.IsNullOrWhiteSpace(e.Item.FloorId))
        {
            throw new ApiException("층을 고르십시오. 층이 없으면 목록에 나오지 않습니다.");
        }

        return e.IsNew
            ? Api.CreateRoomAsync(e.Item)
            : Api.UpdateRoomAsync(e.Item.Id, e.Item);
    }

    private Task DeleteAsync(Room r) => Api.DeleteRoomAsync(r.Id);
}
