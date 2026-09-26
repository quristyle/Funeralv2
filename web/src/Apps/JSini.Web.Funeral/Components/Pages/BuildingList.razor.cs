using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class BuildingList
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private IReadOnlyList<Building> _all = [];

    /// <summary>건물별 층 수. 지우기 전에 「비어 있는 건물인가」를 보여 준다.</summary>
    private Dictionary<string, int> _floorCount = [];

    private string? _keyword;

    private IReadOnlyList<Building> Shown
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return _all;
            }

            var k = _keyword.Trim();
            return [.. _all.Where(b =>
                b.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                || (b.Address?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false))];
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 층은 건물 전체를 한 번에 받아 세어 둔다. 건물마다 따로 물으면
        // 건물 수만큼 왕복이 생긴다.
        //
        // 그리고 건물과 나란히 부른다 — 층 조회가 건물 목록을 쓰지 않으므로
        // 기다릴 이유가 없다.
        var buildings = Api.GetBuildingsAsync();
        var floorsTask = Api.GetFloorsAsync();

        await Task.WhenAll(buildings, floorsTask);

        _all = buildings.Result;

        var floors = floorsTask.Result;
        _floorCount = floors
            .GroupBy(f => f.BuildingId)
            .ToDictionary(g => g.Key, g => g.Count());

        return _all.Count;
    }, "등록된 건물이 없습니다.", "건물 목록을 읽지 못했습니다");

    /// <summary>사진 칸에 적을 글자. 없으면 「미등록」이라고 말한다.</summary>
    private static string PhotoText(List<string>? photos) =>
        photos is { Count: > 0 } ? $"{photos.Count}장" : "미등록";

    private void FillNew(Building b)
    {
        // 회사 식별자는 서버가 토큰에서 정한다. 화면이 넣으면 남의 회사에
        // 건물을 만들 수 있는 길이 열린다.
        b.CompanyId = string.Empty;
    }

    private Task SaveAsync((Building Item, bool IsNew) e) => e.IsNew
        ? Api.CreateBuildingAsync(e.Item)
        : Api.UpdateBuildingAsync(e.Item.Id, e.Item);

    private async Task DeleteAsync(Building b)
    {
        // 층이 남아 있으면 지우지 않는다. 서버도 막지만 여기서 먼저 말해 주는
        // 편이 낫다 — 「삭제하지 못했습니다」만 보면 왜인지 알 수 없다.
        //
        // 예외로 알린다. CommGrd 가 그것을 받아 안내로 바꾸고 목록을 그대로 둔다.
        if (_floorCount.GetValueOrDefault(b.Id) is > 0 and var floors)
        {
            throw new ApiException($"이 건물에 층이 {floors}개 있습니다. 층을 먼저 정리하십시오.");
        }

        await Api.DeleteBuildingAsync(b.Id);
    }
}
