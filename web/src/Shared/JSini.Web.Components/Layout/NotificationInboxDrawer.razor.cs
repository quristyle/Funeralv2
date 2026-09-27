using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using JSini.Web.Models;
using JSini.Web.Components.Settings;

namespace JSini.Web.Components.Layout;

public partial class NotificationInboxDrawer : IDisposable
{
    [Inject] private NotificationClient Api { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private NotificationDrawer Drawer { get; set; } = default!;

    private IReadOnlyList<NotificationDto> _notifications = [];

    /// <summary>펴져 있는가. 펴라는 말은 헤더의 종이 <see cref="Drawer"/> 로 보낸다.</summary>
    private bool _open;

    /// <summary>못 읽어 온 까닭. 있으면 「알림이 없다」 대신 이것을 보여 준다.</summary>
    private string? _error;

    /// <summary>읽어 오는 중. 첫 그림에서 「알림이 없다」가 스쳐 지나가지 않게 한다.</summary>
    private bool _loading;

    protected override void OnInitialized()
    {
        Drawer.OpenRequested += OnOpenRequested;
    }

    public void Dispose()
    {
        Drawer.OpenRequested -= OnOpenRequested;
    }

    /// <summary>
    /// <b>펼 때마다 목록을 새로 읽어 온다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 한동안 이 일을 <c>VisibleChanged</c> 콜백이 했는데 <b>거기서는 한 번도
    /// 불리지 않았다.</b> 그때 이 판은 <c>CommPopup</c> 이었고, <c>DxPopup</c> 의
    /// <c>VisibleChanged</c> 는 창이 <b>제 손으로</b> 여닫힐 때(바깥 클릭·Esc·
    /// 닫기 단추)만 울린다. 이 판을 여는 것은 상단 띠의 종 단추라, 부모가
    /// <c>Visible</c> 을 <c>true</c> 로 바꿔 넣는 길뿐이고 그때는 그 콜백이
    /// 울리지 않았다.
    /// </para>
    /// <para>
    /// 그래서 <b>판은 뜨는데 목록이 늘 비어 있었다</b> — 종에 숫자는 붙어 있으니
    /// (그쪽은 따로 난 <c>unread-count</c> 길이다) 「자료가 없다」가 아니라
    /// 「자료를 안 읽어 왔다」인데 화면에는 똑같이 보인다.
    /// </para>
    /// <para>
    /// 지금은 여는 요청이 <see cref="NotificationDrawer"/> 한 곳으로만 들어오므로
    /// 그 자리에서 읽는다. <b>펼 때마다 새로 읽는다</b> — 접어 둔 사이에 온
    /// 알림이 안 보이면 그것도 고장으로 읽힌다.
    /// </para>
    /// </remarks>
    private async void OnOpenRequested(bool open)
    {
        if (_open == open) return;

        _open = open;
        if (open)
        {
            await LoadUnreadAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// 안 읽은 알림을 읽어 온다. <b>못 읽어 오면 그렇다고 말한다</b> — 빈 목록으로
    /// 두면 실패가 「새 알림이 없습니다」로 보여서, 알림이 분명히 와 있는 사람이
    /// 무엇이 잘못됐는지 알 길이 없다.
    /// </summary>
    private async Task LoadUnreadAsync()
    {
        _loading = true;
        _error = null;
        StateHasChanged();
        try
        {
            _notifications = await Api.GetMyNotificationsAsync(unreadOnly: true);
        }
        catch (Exception ex)
        {
            _notifications = [];
            _error = $"알림을 읽지 못했습니다. ({ex.Message})";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// 앱 알림에 떴던 그림. <b>없으면 앱 아이콘</b>이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 떨어지는 자리가 <c>push-sw.js</c> 와 <b>같아야 한다</b> — 그쪽도
    /// <c>data.icon || '/pwa-icon-192.png'</c> 다. 여기만 사람 형상 그림자로
    /// 떨어뜨리면 날씨 특보처럼 사람을 지목하지 않는 알림이 <b>휴대폰에서는
    /// 회사 로고, 알림함에서는 얼굴 모양</b>으로 갈려 보인다.
    /// </para>
    /// <para>
    /// 값이 비는 갈래가 둘이다 — 지목한 사람이 없는 알림, 그리고 <b>이 칸이
    /// 생기기 전에 보낸 줄</b>. 둘을 가릴 방법이 없고 가릴 까닭도 없다.
    /// </para>
    /// </remarks>
    private static string IconOf(NotificationDto n) =>
        n.Icon is { Length: > 0 } icon ? icon : "/pwa-icon-192.png";

    /// <summary>
    /// <b>이 한 건만 읽음으로 찍는다.</b> 화면을 옮기지 않는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 카드를 누르는 것(<see cref="OpenAsync"/>)과 가른 까닭은 <b>하려는 일이
    /// 다르기 때문</b>이다. 안 읽은 알림이 백 건씩 쌓이는 사람에게 목록에서
    /// 가장 잦은 동작은 「보러 간다」가 아니라 「이건 봤다, 치워라」인데,
    /// 그 길이 카드 누르기뿐이면 <b>치울 때마다 남의 화면으로 끌려간다.</b>
    /// </para>
    /// <para>
    /// 「모두 읽음 처리」로도 안 된다 — 그쪽은 아직 볼 것이 남아 있어도
    /// 통째로 지운다.
    /// </para>
    /// </remarks>
    private async Task MarkOneReadAsync(NotificationDto n)
    {
        try
        {
            await Api.MarkInboxReadAsync(n.Id);
            await Drawer.NotifyReadAsync();
        }
        catch (Exception ex)
        {
            _error = $"읽음 처리를 하지 못했습니다. ({ex.Message})";
            return;
        }

        // **목록을 다시 읽지 않고 그 줄만 뺀다.** 백 건짜리 목록을 왕복으로
        // 다시 받아 오면 한 건 치울 때마다 판이 깜박이고, 치우는 사이에 온
        // 알림이 끼어들어 누르려던 다음 줄이 손가락 밑에서 움직인다.
        _notifications = [.. _notifications.Where(x => x.Id != n.Id)];
    }

    private async Task MarkAllReadAsync()
    {
        try
        {
            await Api.MarkAllInboxReadAsync();
            await LoadUnreadAsync();
            await Drawer.NotifyReadAsync();
        }
        catch (Exception ex)
        {
            _error = $"읽음 처리를 하지 못했습니다. ({ex.Message})";
        }
    }

    /// <summary>
    /// 한 건을 누른다. 읽음으로 찍고, <b>갈 곳이 있으면</b> 서랍을 접고 옮겨 간다.
    /// </summary>
    /// <remarks>
    /// 갈 곳이 없는 알림(메일로만 간 것·주소를 안 실은 것)도 <b>읽음으로는
    /// 찍는다.</b> 예전에는 주소가 없으면 그 자리에서 되돌아가서, 그런 줄은
    /// 눌러도 아무 일이 없고 목록에서 영영 안 빠졌다.
    /// </remarks>
    private async Task OpenAsync(NotificationDto n)
    {
        var url = OpenUrl(n.Url);

        try
        {
            await Api.MarkInboxReadAsync(n.Id);
            await Drawer.NotifyReadAsync();
        }
        catch (Exception ex)
        {
            _error = $"읽음 처리를 하지 못했습니다. ({ex.Message})";
            return;
        }

        if (url is null)
        {
            // 서랍은 펴 둔 채 목록만 다시 읽는다 — 방금 누른 줄이 빠진다.
            await LoadUnreadAsync();
            return;
        }

        Drawer.Close();
        Navigation.NavigateTo(url);
    }

    private static string? OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        var match = AiTaskListLink().Match(trimmed);
        return match.Success ? $"/projmng/ai/task/{match.Groups["key"].Value}" : trimmed;
    }

    [GeneratedRegex(@"^/projmng/ai/tasks\?(?:task|key)=(?<key>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AiTaskListLink();
}
