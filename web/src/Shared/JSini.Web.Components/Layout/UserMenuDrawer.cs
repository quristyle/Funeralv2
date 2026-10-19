namespace JSini.Web.Components.Layout;

/// <summary>사용자 메뉴를 바깥에서 여닫는 손잡이. 닫는 쪽이 중요하다 — 휴대폰에서 이 판은 헤더 아래를 통째로 덮고 덮개는 헤더를 안 덮어서, 판이 펴진 채 헤더 단추가 눌리면 열린 것이 판 뒤로 들어간다. 그래서 오른쪽에서 나오는 판을 여는 쪽이 먼저 이 판을 접는다(HeaderTools · AiChatDrawer · ThemeToggle · MainLayout).
/// 화면을 옮겨 가는 길은 판 자신이 LocationChanged 로 듣는다 — 옮기는 자리를 하나씩 적으면 빠뜨린다. scoped 다 — 한 사람이 열었다고 모두의 판이 열리면 안 된다.</summary>
public sealed class UserMenuDrawer
{
    public event Action<bool>? OpenRequested;
    public event Action? ToggleRequested;

    public void Open() => OpenRequested?.Invoke(true);
    public void Close() => OpenRequested?.Invoke(false);
    public void Toggle() => ToggleRequested?.Invoke();
}
