using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class WeatherForecast
{
    [Inject] private LifeEnvClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        _locations.FirstOrDefault(l => l.Id == _selectedLocationId)?.Name ?? "지역을 고르세요";

    private List<WeatherLocation> _locations = [];
    private List<WeatherTimelinePoint> _timeline = [];

    /// <summary>주간(오늘~10일) 예보. 시간대별 표가 못 보여 주는 폭이다.</summary>
    private List<MidTermForecast> _weekly = [];

    private int _selectedLocationId;

    protected override Task OnInitializedAsync() => LoadAsync(async () =>
    {
        _locations = [.. await Client.GetLocationsAsync()];

        if (_locations.Count == 0)
        {
            return 0;
        }

        _selectedLocationId = _locations[0].Id;
        await LoadPointAsync();

        return _timeline.Count;
    }, "등록된 관측 지역이 없습니다. [관측 지역 관리]에서 먼저 등록하십시오.", "예보를 읽지 못했습니다");

    private Task OnLocationChanged(int id)
    {
        _selectedLocationId = id;
        return LoadForecastAsync();
    }

    private Task LoadForecastAsync() => LoadAsync(async () =>
    {
        if (_selectedLocationId == 0)
        {
            _timeline = [];
            return -1;
        }

        await LoadPointAsync();
        return _timeline.Count;
    }, "그 지역의 예보가 아직 없습니다.", "예보를 읽지 못했습니다");

    /// <summary>
    /// 한 지역의 시간대별과 주간을 함께 읽는다.
    ///
    /// <para>
    /// <b>주간이 실패해도 시간대별은 보여 준다.</b> 중기 예보는 기상청 쪽
    /// 자료가 늦게 들어오는 때가 있어, 그것 때문에 화면 전체가 비면
    /// 「예보가 없다」로 읽힌다.
    /// </para>
    /// </summary>
    private async Task LoadPointAsync()
    {
        _timeline = [.. await Client.GetForecastAsync(_selectedLocationId)];

        try
        {
            _weekly = [.. await Client.GetMidTermForecastAsync(_selectedLocationId)];
        }
        catch (ApiException)
        {
            _weekly = [];
        }
    }
}
