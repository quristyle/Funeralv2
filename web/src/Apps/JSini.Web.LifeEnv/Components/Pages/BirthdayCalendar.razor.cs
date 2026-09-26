using Microsoft.AspNetCore.Components;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class BirthdayCalendar
{
    [Inject] private BirthdayClient Api { get; set; } = default!;

    private DateTime _anchor = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private IReadOnlyList<BirthdayCalendarEvent> _events = [];
    private IReadOnlyList<BirthdayMonthStat> _stats = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>
    /// 이 달을 덮는 칸들. 월요일 시작으로 앞뒤 달 며칠이 섞여 나온다 —
    /// 격자를 7의 배수로 채워야 줄이 어긋나지 않는다.
    /// </summary>
    private IEnumerable<DateTime> Days
    {
        get
        {
            var first = _anchor;

            // 월요일이 0 이 되게 옮긴다. C# 의 DayOfWeek 는 일요일이 0 이다.
            var offset = ((int)first.DayOfWeek + 6) % 7;
            var start = first.AddDays(-offset);

            var last = first.AddMonths(1).AddDays(-1);
            var total = (last - start).Days + 1;
            var cells = (int)Math.Ceiling(total / 7d) * 7;

            for (var i = 0; i < cells; i++)
            {
                yield return start.AddDays(i);
            }
        }
    }

    private Task MoveAsync(int months)
    {
        _anchor = _anchor.AddMonths(months);
        return ReloadAsync();
    }

    private Task MoveToTodayAsync()
    {
        _anchor = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 격자에 보이는 범위를 그대로 넘긴다. 달 경계만 넘기면 앞뒤 달 칸이 빈다.
        var days = Days.ToList();

        var events = Api.GetCalendarAsync(days[0], days[^1]);
        var stats = Api.GetStatsAsync();

        await Task.WhenAll(events, stats);

        _events = events.Result;
        _stats = stats.Result;
        return _events.Count;
    }, "이 달에 생일자가 없습니다.", "캘린더를 읽지 못했습니다");
}
