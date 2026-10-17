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
    /// <summary>지금 펴져 있나.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>여닫힘이 갈렸다. 판과 단추가 함께 듣는다.</summary>
    public event Action? Changed;

    /// <summary>여닫는다.</summary>
    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }

    /// <summary>연다.</summary>
    public void Open()
    {
        if (IsOpen) return;
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
