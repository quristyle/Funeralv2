using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Models;
using JSini.Web.Components.Settings;

namespace JSini.Web.Components.Layout;

public partial class NotificationInboxDrawer : IDisposable
{
    [Inject] private NotificationClient Api { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private NotificationDrawer Drawer { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>손짓을 대는 브라우저 쪽. 한 번 받아 두고 계속 쓴다.</summary>
    private IJSObjectReference? _swipeModule;

    /// <summary>그쪽이 우리를 부를 손잡이. 반드시 버려야 한다(회로마다 하나씩 샌다).</summary>
    private DotNetObjectReference<NotificationInboxDrawer>? _swipeRef;

    private IReadOnlyList<NotificationDto> _notifications = [];

    /// <summary>휴대폰인가. 레이아웃이 내려 준다. 고정핀을 그릴지와 알림을 눌러 화면을 옮길 때 접을지가 이 값으로 갈린다.</summary>
    [Parameter] public bool IsPhone { get; set; }

    /// <summary>펴져 있는가. 펴라는 말은 헤더의 종이 <see cref="Drawer"/> 로 보낸다.</summary>
    private bool _open;

    /// <summary>고정핀이 꽂혀 있나. 판이 아니라 여기가 들고 있다(RightSideDrawer.IsPinned 머리말).</summary>
    private bool _pinned;

    /// <summary>못 읽어 온 까닭. 있으면 「알림이 없다」 대신 이것을 보여 준다.</summary>
    private string? _error;

    /// <summary>읽어 오는 중. 첫 그림에서 「알림이 없다」가 스쳐 지나가지 않게 한다.</summary>
    private bool _loading;

    protected override void OnInitialized()
    {
        Drawer.OpenRequested += OnOpenRequested;

        // 바깥에서 달라졌으면(다른 장비에서 읽었다 · 방금 푸시가 왔다) 펴 둔 목록도 따라가야 한다 — 안 들으면 종의 숫자만 맞고 카드는 없는 알림으로 남는다.
        Drawer.Changed += OnChangedAsync;
    }

    public void Dispose()
    {
        Drawer.OpenRequested -= OnOpenRequested;
        Drawer.Changed -= OnChangedAsync;
        _swipeRef?.Dispose();
    }

    /// <summary>바깥 사정이 달라졌다. 펴져 있을 때만 다시 읽는다 — 접힌 서랍은 펼 때 어차피 새로 읽는다.</summary>
    private async Task OnChangedAsync()
    {
        if (!_open) return;

        await InvokeAsync(async () =>
        {
            await LoadUnreadAsync();
            StateHasChanged();
        });
    }

    /// <summary>카드를 오른쪽으로 밀면 읽음, 왼쪽으로 밀면 삭제가 되게 한다. 거는 자리는 목록이 아니라 판(#jsini-note-drawer)이다 — 목록은 한 건 치울 때마다 다시 그려져 걸어 둔 것이 사라진다. 판은 셸이 사는 내내 있으므로 한 번만 건다. 회로가 닫혔으면 조용히 넘어간다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            _swipeModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/note-swipe.js");
            _swipeRef ??= DotNetObjectReference.Create(this);

            await _swipeModule.InvokeVoidAsync("attachNoteSwipe", "#jsini-note-drawer", _swipeRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
        }
    }

    /// <summary>오른쪽으로 민 것을 읽음으로 찍는다. ✓ 단추와 하는 일이 같다 — 손짓은 그 단추로 가는 빠른 길이다.</summary>
    [JSInvokable]
    public async Task MarkReadSwipedAsync(string id)
    {
        var note = _notifications.FirstOrDefault(x => x.Id == id);
        if (note is null) return;

        await MarkOneReadAsync(note);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>왼쪽으로 민 것을 알림함에서 치운다. 읽음으로 갈음할 수 없다 — 읽음은 「봤다」라서 안 읽은 것만 풀면 도로 나온다. 화면에서 먼저 빼고 서버에 간다 — 아니면 몇 백 ms 동안 카드가 제자리에 남아 한 번 더 밀게 된다. 못 치웠으면 목록을 다시 읽어 되돌린다. 종의 숫자도 함께 센다.</summary>
    [JSInvokable]
    public async Task DeleteSwipedAsync(string id)
    {
        if (!_notifications.Any(x => x.Id == id)) return;

        _notifications = [.. _notifications.Where(x => x.Id != id)];
        _error = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            await Api.DeleteInboxAsync(id);
            await Drawer.NotifyReadAsync();
        }
        catch (Exception ex)
        {
            _error = $"알림을 지우지 못했습니다. ({ex.Message})";
            await LoadUnreadAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>펼 때마다 목록을 새로 읽는다. VisibleChanged 콜백으로 하던 시절에는 한 번도 불리지 않았다 — 부모가 Visible 을 바꿔 넣는 길에서는 그 콜백이 울리지 않는다. 그래서 판은 뜨는데 목록이 늘 비어 있었다. 접어 둔 사이에 온 알림이 안 보이면 그것도 고장으로 읽힌다.</summary>
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

    /// <summary>안 읽은 알림을 읽어 온다. 못 읽어 오면 그렇다고 말한다 — 빈 목록으로 두면 실패가 「새 알림이 없습니다」로 보인다.</summary>
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

    /// <summary>앱 알림에 떴던 그림. 없으면 앱 아이콘이다. 떨어지는 자리는 push-sw.js 와 같아야 한다 — 여기만 사람 그림자로 떨어지면 같은 알림이 휴대폰에서는 로고, 알림함에서는 얼굴로 갈려 보인다. 비는 갈래는 지목한 사람이 없는 알림과 이 칸이 생기기 전에 보낸 줄이다.</summary>
    private static string IconOf(NotificationDto n) =>
        n.Icon is { Length: > 0 } icon ? icon : "/pwa-icon-192.png";

    /// <summary>이 한 건만 읽음으로 찍는다. 화면을 옮기지 않는다. 카드를 누르는 것(OpenAsync)과 가른 까닭은 하려는 일이 다르기 때문이다 — 안 읽은 알림이 쌓인 사람이 가장 자주 하는 것은 「보러 간다」가 아니라 「봤다, 치워라」다. 모두 읽음 처리로는 볼 것이 남아도 통째로 지워진다.</summary>
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

        // 목록을 다시 읽지 않고 그 줄만 뺀다 — 백 건을 다시 받으면 한 건마다 판이 깜박이고 손가락 밑에서 다음 줄이 움직인다.
        _notifications = [.. _notifications.Where(x => x.Id != n.Id)];
    }

    /// <summary>이 한 건만 알림함에서 치운다(삭제). 화면을 옮기지 않는다. 데스크톱은 손짓 대신 휴지통 아이콘으로 치운다. 화면에서 먼저 빼고 서버에 가며, 못 치웠으면 목록을 다시 읽어 되돌린다.</summary>
    private async Task DeleteOneAsync(NotificationDto n)
    {
        _notifications = [.. _notifications.Where(x => x.Id != n.Id)];
        _error = null;

        try
        {
            await Api.DeleteInboxAsync(n.Id);
            await Drawer.NotifyReadAsync();
        }
        catch (Exception ex)
        {
            _error = $"알림을 지우지 못했습니다. ({ex.Message})";
            await LoadUnreadAsync();
        }
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

    /// <summary>한 건을 누른다. 읽음으로 찍고, 갈 곳이 있으면 서랍을 접고 옮겨 간다. 갈 곳이 없는 알림도 읽음으로 찍는다 — 예전에는 그런 줄이 눌러도 아무 일이 없고 목록에서 영영 안 빠졌다. 못 박아 두었으면 펴 둔 채 옮긴다(본문이 판 폭만큼 밀려 있어 새 화면을 덮지 않는다).</summary>
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

        if (_pinned && !IsPhone)
        {
            await LoadUnreadAsync();
        }
        else
        {
            Drawer.Close();
        }

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
