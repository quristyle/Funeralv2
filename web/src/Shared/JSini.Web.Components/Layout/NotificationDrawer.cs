namespace JSini.Web.Components.Layout;

/// <summary>
/// 알림함 서랍을 <b>바깥에서</b> 여닫고, 읽은 것을 <b>종에게 되알리는</b> 손잡이.
/// </summary>
/// <remarks>
/// <para>
/// 서랍과 그것을 여는 종(<c>HeaderTools</c>)이 <b>같은 자리에 있을 수 없어서</b>
/// 생긴 사이다. 서랍은 휴대폰에서 화면을 다 채워야 하는데, 헤더 안에 두면
/// 그럴 수가 없다 — <c>.jsini-header</c> 가 <c>z-index: 1030</c> 으로 제
/// 쌓임 맥락을 만들고, 그 안의 것은 아무리 큰 <c>z-index</c> 를 줘도 맥락
/// 바깥의 형제(떠다니는 메뉴 단추 1031 · 아래 띠 1030)를 넘지 못한다.
/// <b>서랍은 펴졌는데 그 위로 띠와 단추가 그대로 떠 있었다.</b>
/// </para>
///
/// <para>
/// 그래서 서랍은 격자 바깥(<c>MainLayout</c> 맨 끝)으로 옮기고, 종은 헤더에
/// 남았다. 둘을 잇는 것이 이것이다 — <c>ThemeDrawer</c> 가 테마 서랍과
/// 사용자 메뉴를 이은 것과 같은 구도다.
/// </para>
///
/// <para>
/// <b>오가는 것이 두 방향이다.</b> 종 → 서랍은 <see cref="Open"/>(열어라),
/// 서랍 → 종은 <see cref="NotifyReadAsync"/>(읽었으니 숫자를 다시 세라)다. 뒤엣것이
/// 없으면 서랍에서 모두 읽음을 눌러도 <b>종의 빨간 숫자가 그대로 남는다.</b>
/// </para>
///
/// <para>scoped 다. 한 사람이 열었다고 모두의 서랍이 열리면 안 된다.</para>
/// </remarks>
public sealed class NotificationDrawer
{
    /// <summary>여닫으라는 요청. 서랍이 받는다.</summary>
    public event Action<bool>? OpenRequested;

    /// <summary>읽음이 찍혔다는 알림. 종(<c>HeaderTools</c>)이 받아 다시 센다.</summary>
    public event Func<Task>? Read;

    /// <summary>서랍을 편다.</summary>
    public void Open() => OpenRequested?.Invoke(true);

    /// <summary>서랍을 접는다.</summary>
    public void Close() => OpenRequested?.Invoke(false);

    /// <summary>읽음을 찍었다고 알린다.</summary>
    public Task NotifyReadAsync() => Read?.Invoke() ?? Task.CompletedTask;
}
