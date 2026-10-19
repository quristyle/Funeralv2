namespace JSini.Web.Components.Layout;

/// <summary>알림함 서랍을 바깥에서 여닫고, 읽은 것을 종에게 되알리는 손잡이. 서랍은 휴대폰에서 화면을 다 채워야 해서 헤더 밖에 둔다 — 헤더는 z-index 1030 의 쌓임 맥락이라 안의 판은 아래 띠·메뉴 단추를 넘지 못한다.
/// 오가는 것은 셋이다: 종 → 서랍(열어라), 서랍 → 종(읽었으니 숫자를 다시 세라), 종 → 서랍(바깥이 달라졌으니 펴 둔 목록을 다시 읽어라). scoped — 한 사람이 열었다고 모두의 서랍이 열리면 안 된다.</summary>
public sealed class NotificationDrawer
{
    /// <summary>같은 자리에 겹쳐 서는 오른쪽 판 무리. 펴기 전에 다른 판을 접는다(RightDrawerGroup).</summary>
    private readonly RightDrawerGroup _group;

    public NotificationDrawer(RightDrawerGroup group)
    {
        _group = group;
        _group.Register(this, Close);
    }

    /// <summary>여닫으라는 요청. 서랍이 받는다.</summary>
    public event Action<bool>? OpenRequested;

    /// <summary>읽음이 찍혔다는 알림. 종(<c>HeaderTools</c>)이 받아 다시 센다.</summary>
    public event Func<Task>? Read;

    /// <summary>바깥 사정이 달라졌다는 알림. 서랍이 펴져 있을 때만 목록을 다시 읽는다 — 접혀 있으면 펼 때 어차피 새로 읽는다.</summary>
    public event Func<Task>? Changed;

    /// <summary>서랍을 편다. <b>다른 오른쪽 판을 먼저 접는다.</b></summary>
    public void Open()
    {
        _group.CloseOthers(this);
        OpenRequested?.Invoke(true);
    }

    /// <summary>서랍을 접는다.</summary>
    public void Close() => OpenRequested?.Invoke(false);

    /// <summary>읽음을 찍었다고 알린다.</summary>
    public Task NotifyReadAsync() => Read?.Invoke() ?? Task.CompletedTask;

    /// <summary>바깥에서 달라진 것이 있다고 알린다.</summary>
    public Task NotifyChangedAsync() => Changed?.Invoke() ?? Task.CompletedTask;
}
