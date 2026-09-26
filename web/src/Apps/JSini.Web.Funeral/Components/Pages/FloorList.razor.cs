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
        // 셋을 나란히. 조건(_buildingId)은 이미 화면에 있는 값이라 앞의 조회
        // 결과를 쓰지 않는다.
        //
        // 건물 목록은 편집 폼의 고르개가 쓴다. 나란히 받아도 **셋이 다 온 뒤에**
        // 담으므로, 첫 등록에서 빈 고르개가 뜨지 않는 것은 그대로다.
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

    /// <summary>
    /// 새 층의 기본값.
    ///
    /// 조건줄에서 고른 건물을 그대로 넣는다. 안 넣으면 어느 건물에도 속하지
    /// 않은 층이 만들어져 목록에서 사라진다. 정렬은 마지막 뒤에 붙인다 —
    /// 0 으로 두면 새 층이 맨 앞에 끼어든다.
    /// </summary>
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
