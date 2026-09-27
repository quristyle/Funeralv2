using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using JSini.Web.Models;
using JSini.Web.Components.Settings;

namespace JSini.Web.Components.Layout;

public partial class NotificationInboxPopup
{
    [Inject] private NotificationClient Api { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public EventCallback OnRead { get; set; }

    private IReadOnlyList<NotificationDto> _notifications = [];

    /// <summary>못 읽어 온 까닭. 있으면 「알림이 없다」 대신 이것을 보여 준다.</summary>
    private string? _error;

    /// <summary>한 번 연 동안 이미 읽어 왔는가. 닫으면 풀린다.</summary>
    private bool _loaded;

    /// <summary>읽어 오는 중. 첫 그림에서 「알림이 없다」가 스쳐 지나가지 않게 한다.</summary>
    private bool _loading;

    /// <summary>
    /// <b>열릴 때 목록을 읽어 온다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 한동안 이 일을 <see cref="OnVisibleChangedAsync"/> 가 했는데 <b>거기서는
    /// 한 번도 불리지 않았다.</b> <c>DxPopup</c> 의 <c>VisibleChanged</c> 는
    /// 창이 <b>제 손으로</b> 여닫힐 때(바깥 클릭·Esc·닫기 단추)만 울린다. 이 창을
    /// 여는 것은 상단 띠의 종 단추라, 부모가 <c>Visible</c> 을 <c>true</c> 로
    /// 바꿔 넣는 길뿐이고 그때는 그 콜백이 울리지 않는다.
    /// </para>
    /// <para>
    /// 그래서 <b>창은 뜨는데 목록이 늘 비어 있었다</b> — 종에 숫자는 붙어 있으니
    /// (그쪽은 따로 난 <c>unread-count</c> 길이다) 「자료가 없다」가 아니라
    /// 「자료를 안 읽어 왔다」인데 화면에는 똑같이 보인다.
    /// </para>
    /// <para>
    /// 파라미터가 들어올 때마다 읽으면 그릴 때마다 부르게 되므로
    /// <see cref="_loaded"/> 로 한 번만 읽는다. 닫으면 풀어 두어 <b>다시 열 때는
    /// 새로 읽는다</b> — 열어 둔 사이에 온 알림이 안 보이면 그것도 고장으로 읽힌다.
    /// 기기 편집 창이 <c>_loadedFor</c> 로 하는 것과 같은 방식이다.
    /// </para>
    /// </remarks>
    protected override async Task OnParametersSetAsync()
    {
        if (!Visible)
        {
            _loaded = false;
            return;
        }

        if (_loaded) return;

        _loaded = true;
        await LoadUnreadAsync();
    }

    private async Task OnVisibleChangedAsync(bool visible)
    {
        if (Visible == visible) return;

        Visible = visible;
        if (!visible)
        {
            _loaded = false;
        }

        if (VisibleChanged.HasDelegate)
        {
            await VisibleChanged.InvokeAsync(visible);
        }
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

    private async Task MarkAllReadAsync()
    {
        try
        {
            await Api.MarkAllInboxReadAsync();
            await LoadUnreadAsync();
            if (OnRead.HasDelegate)
            {
                await OnRead.InvokeAsync();
            }
        }
        catch (Exception ex)
        {
            _error = $"읽음 처리를 하지 못했습니다. ({ex.Message})";
        }
    }

    /// <summary>
    /// 한 건을 누른다. 읽음으로 찍고, <b>갈 곳이 있으면</b> 창을 닫고 옮겨 간다.
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
            if (OnRead.HasDelegate)
            {
                await OnRead.InvokeAsync();
            }
        }
        catch (Exception ex)
        {
            _error = $"읽음 처리를 하지 못했습니다. ({ex.Message})";
            return;
        }

        if (url is null)
        {
            // 창은 열어 둔 채 목록만 다시 읽는다 — 방금 누른 줄이 빠진다.
            await LoadUnreadAsync();
            return;
        }

        await OnVisibleChangedAsync(false);
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
