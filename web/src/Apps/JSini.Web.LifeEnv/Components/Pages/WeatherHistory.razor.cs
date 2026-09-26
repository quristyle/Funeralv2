using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class WeatherHistory
{
    [Inject] private LifeEnvClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _locations.FirstOrDefault(l => l.Id == _locationId)?.Name ?? SchSummary.Any,
        $"최근 {_days}일",
        _byHour ? $"{_hour} 시 비교" : null);

    private IReadOnlyList<WeatherLocation> _locations = [];
    private IReadOnlyList<WeatherInfo> _history = [];
    private IReadOnlyList<HourlyTemp> _hourly = [];

    private int? _locationId;
    private int _days = 7;

    private bool _byHour;
    private int _hour = 14;

    private static readonly object[] DayOptions =
    [
        new { Value = 3, Text = "최근 3일" },
        new { Value = 7, Text = "최근 7일" },
        new { Value = 14, Text = "최근 14일" },
        new { Value = 30, Text = "최근 30일" },
        new { Value = 90, Text = "최근 90일" },
    ];

    private static readonly int[] Hours = [.. Enumerable.Range(0, 24)];

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(async () =>
        {
            _locations = await Client.GetLocationsAsync();

            // 첫 지역을 골라 둔다. 안 고르면 빈 표만 보이고 무엇을 해야
            // 하는지 알 수 없다.
            _locationId = _locations.FirstOrDefault()?.Id;

            return _locations.Count;
        }, "등록된 관측 지역이 없습니다.", "관측 지역을 읽지 못했습니다");

        if (_locationId is not null)
        {
            await ReloadAsync();
        }
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        if (_locationId is not { } id)
        {
            _history = [];
            _hourly = [];
            return 0;
        }

        if (_byHour)
        {
            _hourly = await Client.GetHourlyHistoryAsync(id, _hour, _days);
            return _hourly.Count;
        }

        // 실측 조회는 지역 **이름**으로 받는다. 식별자가 아니다.
        var name = _locations.FirstOrDefault(l => l.Id == id)?.Name;
        _history = name is null ? [] : await Client.GetHistoryAsync(name, _days);

        return _history.Count;
    }, "그 기간에 기록이 없습니다.", "실측 이력을 읽지 못했습니다");

    /// <summary>
    /// 전날 같은 시각과의 차이.
    ///
    /// 목록은 최근이 위다. 그래서 「전날」은 <b>다음 줄</b>이다 —
    /// 순서를 잘못 보면 부호가 뒤집힌다.
    /// </summary>
    private double? DiffFromPreviousDay(HourlyTemp row)
    {
        var index = _hourly.ToList().FindIndex(x => x.Date == row.Date);

        return index >= 0 && index + 1 < _hourly.Count
            ? Math.Round(row.Temp - _hourly[index + 1].Temp, 1)
            : null;
    }
}
