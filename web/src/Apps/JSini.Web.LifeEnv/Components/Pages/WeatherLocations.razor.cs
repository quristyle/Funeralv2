using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Layout;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class WeatherLocations
{
    [Inject] private LifeEnvClient Client { get; set; } = default!;

    private IReadOnlyList<WeatherLocation> _items = [];
    private IReadOnlyList<WeatherWarningZone> _zones = [];

    private string? _search;
    private IReadOnlyList<GridCoordinate> _found = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 둘을 나란히. 특보구역은 고르개가 쓰는 별개의 목록이라 관측 지역을
        // 기다릴 이유가 없다.
        var locations = Client.GetLocationsAsync();
        var zones = Client.GetWarningZonesAsync();

        await Task.WhenAll(locations, zones);

        _items = [.. locations.Result.OrderBy(x => x.SortOrder)];
        _zones = zones.Result;

        return _items.Count;
    }, "등록된 관측 지역이 없습니다.", "관측 지역을 읽지 못했습니다");

    private void FillNew(WeatherLocation loc)
    {
        loc.IsActive = true;
        loc.SortOrder = _items.Count == 0 ? 1 : _items.Max(x => x.SortOrder) + 1;

        // 지난 검색 결과가 남아 있으면 새 지역에 엉뚱한 좌표가 붙는다.
        _search = null;
        _found = [];
    }

    private async Task SearchGridAsync()
    {
        if (string.IsNullOrWhiteSpace(_search))
        {
            Say("찾을 지역 이름을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        await LoadAsync(async () =>
        {
            _found = await Client.SearchGridAsync(_search.Trim());
            return _found.Count;
        }, "그 이름의 격자를 찾지 못했습니다.", "격자를 찾지 못했습니다");
    }

    /// <summary>
    /// 검색 결과를 폼에 옮긴다.
    ///
    /// **격자와 이름뿐이다.** 중기예보·특보구역 코드는 이 검색이 주지 않아
    /// 아래에서 따로 골라야 한다. 주는 줄 알고 비워 두면 그 지역만 중기예보가
    /// 조용히 안 들어온다.
    ///
    /// 이름은 <b>비어 있을 때만</b> 채운다. 고쳐 쓰던 이름을 검색 한 번으로
    /// 되돌리면 사용자가 쓴 것이 사라진다.
    /// </summary>
    private void Apply(WeatherLocation loc, GridCoordinate g)
    {
        loc.Nx = g.Nx;
        loc.Ny = g.Ny;

        if (string.IsNullOrWhiteSpace(loc.Name))
        {
            loc.Name = GridName(g);
        }
    }

    private static string GridName(GridCoordinate g) =>
        string.Join(" ", new[] { g.Region1, g.Region2, g.Region3 }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    private Task SaveAsync((WeatherLocation Item, bool IsNew) e)
    {
        if (e.Item.Nx == 0 && e.Item.Ny == 0)
        {
            throw new ApiException("격자 좌표가 없습니다. 좌표 검색으로 지역을 고르십시오.");
        }

        return e.IsNew
            ? Client.CreateLocationAsync(e.Item)
            : Client.UpdateLocationAsync(e.Item.Id, e.Item);
    }

    private Task DeleteAsync(WeatherLocation loc) => Client.DeleteLocationAsync(loc.Id);
}
