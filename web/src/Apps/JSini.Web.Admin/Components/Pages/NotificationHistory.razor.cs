using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class NotificationHistory
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Period(_from, _to),
        SchSummary.On(_unreadOnly, "안 읽은 것만"));

    /// <summary>
    /// 조회 기간. <b>기본이 최근 한 달이다.</b>
    /// </summary>
    /// <remarks>
    /// 알림은 지우지 않으므로 자라기만 한다. 전에는 조건 없이 불러
    /// <b>그 사람의 알림을 전부</b> 받아 왔다(서버에 페이징이 없다 —
    /// <c>GetMyNotificationsAsync</c> 머리말). 발송 이력(이레)보다 길게 잡은
    /// 것은 자기 알림함은 드물게 열어 보기 때문이다.
    /// </remarks>
    private DateTime? _from = DateTime.Today.AddMonths(-1);
    private DateTime? _to = DateTime.Today;

    private bool _unreadOnly;
    private IReadOnlyList<NotificationDto> _all = [];

    private IReadOnlyList<NotificationDto> Shown =>
        _unreadOnly ? [.. _all.Where(n => !n.IsRead)] : _all;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetMyNotificationsAsync(_from, _to);
        return _all.Count;
    }, "받은 알림이 없습니다.", "알림을 읽지 못했습니다");

    private async Task MarkReadAsync(NotificationDto n)
    {
        if (await RunAsync(() => Api.MarkNotificationReadAsync(n.Id),
                "읽음으로 표시했습니다.", "표시하지 못했습니다"))
        {
            n.IsRead = true;
        }
    }
}
