using Microsoft.AspNetCore.Components;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class PushDashboard
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => $"최근 {_days} 일";

    private static readonly int[] DayOptions = [7, 14, 30, 90];

    private int _days = 7;
    private PushStatsDto? _stats;
    private IReadOnlyList<PushTrendPointDto> _trend = [];
    private IReadOnlyList<PushFailureReasonDto> _reasons = [];
    private PushEngagementDto? _engagement;
    private IReadOnlyList<PushMessageStatDto> _messages = [];
    private IReadOnlyList<PushUserStatDto> _users = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 여섯을 나란히. 서로 기다릴 이유가 없다.
        var stats = Api.GetPushStatsAsync(_days);
        var trend = Api.GetPushTrendAsync(days: _days);
        var reasons = Api.GetPushFailureReasonsAsync(_days);
        var engagement = Api.GetPushEngagementAsync(_days);
        var messages = Api.GetTopPushMessagesAsync();
        var users = Api.GetPushUserStatsAsync();

        await Task.WhenAll(stats, trend, reasons, engagement, messages, users);

        _stats = stats.Result;
        _trend = trend.Result;
        _reasons = reasons.Result;
        _engagement = engagement.Result;
        _messages = messages.Result;
        _users = users.Result;

        return (_stats?.TotalSent ?? 0) + _trend.Count;
    }, "그 기간에 발송 내역이 없습니다.", "푸시 현황을 읽지 못했습니다");
}
