namespace JSini.Web.Components.Layout;

/// <summary>
/// 테마 서랍을 <b>바깥에서</b> 여닫는 손잡이.
/// </summary>
public sealed class ThemeDrawer
{
    public event Action<bool>? OpenRequested;

    public bool IsOpen { get; private set; }

    public void Open()
    {
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
