using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class WeatherEvents
{
    [Inject] private LifeEnvClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _locations.FirstOrDefault(l => l.Id == _locationId)?.Name ?? SchSummary.Any,
        SchSummary.Period(_from, _to),
        SchSummary.On(_onlyUnsent, "미발송만"));

    private IReadOnlyList<WeatherEventRecord> _events = [];
    private IReadOnlyList<WeatherLocation> _locations = [];
    private int _total;

    private int? _locationId;
    private DateTime? _from;
    private DateTime? _to;
    private bool _onlyUnsent;

    /// <summary>
    /// 「미발송만」은 받아 온 것 안에서만 거른다 — 서버에 그 조건이 없어, 전체가 많으면 기간을 좁혀야 제대로 보인다.
    /// </summary>
    private IReadOnlyList<WeatherEventRecord> Shown =>
        _onlyUnsent ? [.. _events.Where(e => !e.IsNotified)] : _events;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 지역 목록은 고르개와 이름 표시에 함께 쓴다. 한 번만 읽고, 읽을 때는 기록과 나란히 받는다.
        var locations = _locations.Count == 0 ? Client.GetLocationsAsync() : null;

        var events = Client.GetEventsAsync(
            1, 200,
            _from?.ToString("yyyy-MM-dd"),
            _to?.ToString("yyyy-MM-dd"),
            _locationId);

        await Task.WhenAll(new Task?[] { locations, events }.OfType<Task>());

        if (locations is not null) _locations = locations.Result;

        var page = events.Result;
        _events = page?.Items ?? [];
        _total = page?.TotalCount ?? _events.Count;

        // 잘렸으면 반드시 말한다 — 「전부다」로 읽으면 없는 것을 찾게 된다. 결과 자체라 안내 줄이 아니라 토스트로 낸다.
        if (_total > _events.Count)
        {
            Say($"전체 {_total}건 중 {_events.Count}건입니다. 기간이나 지역으로 좁히십시오.");
        }

        return _events.Count;
    }, "조건에 맞는 이벤트가 없습니다.", "이벤트 기록을 읽지 못했습니다");

    private string LocationName(int? id) =>
        id is null ? "-" : _locations.FirstOrDefault(l => l.Id == id)?.Name ?? "-";

    private static string DeleteMessage(WeatherEventRecord _) =>
        "이 특보 기록을 지웁니다. 이미 나간 알림은 되돌아가지 않습니다.";

    private Task DeleteAsync(WeatherEventRecord e) => Client.DeleteEventAsync(e.Id);
}
