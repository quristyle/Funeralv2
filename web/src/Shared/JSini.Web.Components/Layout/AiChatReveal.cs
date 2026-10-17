namespace JSini.Web.Components.Layout;

/// <summary>
/// AI 대화 서랍을 헤더 단추에서 여닫는 손잡이.
/// </summary>
/// <remarks>
/// <para>
/// 단추는 헤더에 있고 판은 격자 바깥(<c>MainLayout</c> 맨 끝)에 있다.
/// <c>.jsini-header</c> 안에 서랍을 두면 헤더의 쌓임 맥락(z-index 1030)과
/// 트랜스폼에 갇혀 본문 영역에 가려지므로 판을 격자 바깥으로 분리하고
/// 둘을 잇는 통으로 이것을 둔다(<c>ThemeDrawer</c>·<c>QuickAskReveal</c>와 같은 구도).
/// </para>
/// <para>scoped 다. 한 사람이 열었다고 모두의 서랍이 열리면 안 된다.</para>
/// </remarks>
public sealed class AiChatReveal
{
    /// <summary>
    /// 같은 자리에 겹쳐 서는 오른쪽 판 무리. <b>펴기 전에 다른 판을 접는다</b>
    /// (<see cref="RightDrawerGroup"/>).
    /// </summary>
    private readonly RightDrawerGroup _group;

    public AiChatReveal(RightDrawerGroup group)
    {
        _group = group;
        _group.Register(this, Close);
    }

    /// <summary>지금 펴져 있나.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>여닫힘이 갈렸다. 판과 단추가 함께 듣는다.</summary>
    public event Action? Changed;

    /// <summary>여닫는다. <b>펴지는 쪽일 때만</b> 다른 오른쪽 판을 접는다.</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        Open();
    }

    /// <summary>연다. <b>다른 오른쪽 판을 먼저 접는다.</b></summary>
    public void Open()
    {
        if (IsOpen) return;

        // 먼저 접고 편다. 뒤에 접으면 방금 편 것까지 함께 접힌다.
        _group.CloseOthers(this);

        IsOpen = true;
        Changed?.Invoke();
    }

    /// <summary>닫는다. 이미 닫혀 있으면 아무 일도 하지 않는다.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Changed?.Invoke();
    }
}
