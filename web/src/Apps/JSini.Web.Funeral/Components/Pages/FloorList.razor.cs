using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class FloorList
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private string? _buildingId;

    private IReadOnlyList<Floor> _floors = [];
    private IReadOnlyList<Building> _buildings = [];

    /// <summary>층별 호실 수. 지우기 전에 비어 있는지 본다.</summary>
    private Dictionary<string, int> _roomCount = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 셋을 나란히 받는다. 건물 목록은 편집 폼 고르개가 쓰므로 셋이 다 온 뒤에 담는다.
        var buildings = Api.GetBuildingsAsync();
        var floors = Api.GetFloorsAsync(_buildingId);
        var roomsTask = Api.GetRoomsAsync(_buildingId);

        await Task.WhenAll(buildings, floors, roomsTask);

        _buildings = buildings.Result;
        _floors = floors.Result;

        var rooms = roomsTask.Result;
        _roomCount = rooms
            .Where(r => r.FloorId is not null)
            .GroupBy(r => r.FloorId!)
            .ToDictionary(g => g.Key, g => g.Count());

        return _floors.Count;
    }, "조건에 맞는 층이 없습니다.", "층 목록을 읽지 못했습니다");

    /// <summary>새 층의 기본값. 조건줄의 건물을 넣고(빼면 목록에서 사라진다), 정렬은 마지막 뒤에 붙인다(0 이면 맨 앞에 끼어든다).</summary>
    private void FillNew(Floor f)
    {
        f.BuildingId = _buildingId ?? _buildings.FirstOrDefault()?.Id ?? string.Empty;
        f.SortOrder = _floors.Count == 0 ? 1 : _floors.Max(x => x.SortOrder) + 1;
    }

    private Task SaveAsync((Floor Item, bool IsNew) e)
    {
        if (string.IsNullOrWhiteSpace(e.Item.BuildingId))
        {
            throw new ApiException("건물을 고르십시오.");
        }

        return e.IsNew
            ? Api.CreateFloorAsync(e.Item)
            : Api.UpdateFloorAsync(e.Item.Id, e.Item);
    }

    private async Task DeleteAsync(Floor f)
    {
        if (_roomCount.GetValueOrDefault(f.Id) is > 0 and var rooms)
        {
            throw new ApiException($"이 층에 호실이 {rooms}개 있습니다. 호실을 먼저 정리하십시오.");
        }

        await Api.DeleteFloorAsync(f.Id);
    }
}
