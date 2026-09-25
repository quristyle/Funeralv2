namespace JSini.Web.Components.Layout;

/// <summary>
/// 사용자 메뉴를 <b>바깥에서</b> 여닫는 손잡이.
/// </summary>
public sealed class UserMenuDrawer
{
    public event Action<bool>? OpenRequested;
    public void Open() => OpenRequested?.Invoke(true);
    public void Close() => OpenRequested?.Invoke(false);
}
