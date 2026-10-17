namespace JSini.Web.Components.Layout;

/// <summary>
/// 테마 서랍을 <b>바깥에서</b> 여닫는 손잡이.
/// </summary>
/// <remarks>
/// <b>펴기 전에 다른 오른쪽 판을 접는다</b>(<see cref="RightDrawerGroup"/>) —
/// 넷이 같은 자리에 겹쳐 서기 때문이다.
/// </remarks>
public sealed class ThemeDrawer
{
    private readonly RightDrawerGroup _group;

    public ThemeDrawer(RightDrawerGroup group)
    {
        _group = group;
        _group.Register(this, Close);
    }

    public event Action<bool>? OpenRequested;

    public bool IsOpen { get; private set; }

    public void Open()
    {
        // 먼저 접고 편다. 뒤에 접으면 방금 편 것까지 함께 접힌다.
        _group.CloseOthers(this);

        IsOpen = true;
        OpenRequested?.Invoke(true);
    }

    public void Close()
    {
        IsOpen = false;
        OpenRequested?.Invoke(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }
}
