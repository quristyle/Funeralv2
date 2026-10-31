using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Settings;
using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Funeral.Components.Pages;

public partial class MyNotificationSettingPage
{
    [Inject] private NotificationClient Api { get; set; } = default!;

    /// <summary>받을 수 있는 알림. 아직 못 읽었으면 <c>null</c>.</summary>
    private List<MyNotificationEventDto>? _events;

    /// <summary>저장이 도는 동안 스위치를 잠근다 — 연달아 누르면 응답 차례가 뒤집혀 옛 값이 이긴다.</summary>
    private bool _saving;

    protected override Task OnInitializedAsync() => LoadAsync(async () =>
    {
        _events = [.. await Api.GetMyEventsAsync()];

        // 빈 목록은 화면의 안내가 말한다. 토스트까지 뜨면 같은 말이 두 번 난다.
        return 1;
    }, failMessage: "받을 수 있는 알림을 읽지 못했습니다");

    private async Task SaveAsync(MyNotificationEventDto item, bool? push, bool? email)
    {
        if (_saving) return;

        _saving = true;
        try
        {
            MyNotificationEventDto? saved = null;

            var ok = await RunAsync(
                async () => saved = await Api.SaveMyEventAsync(item.Code, push, email),
                $"「{item.Name}」 알림을 저장했습니다.", "저장하지 못했습니다");

            // 못 저장했으면 스위치를 되돌린다 — 그대로 두면 켜진 것처럼 보이는데 서버는 옛 값이다.
            if (ok && saved is not null)
            {
                item.PushEnabled = saved.PushEnabled;
                item.EmailEnabled = saved.EmailEnabled;
            }
        }
        finally
        {
            _saving = false;
        }
    }
}
