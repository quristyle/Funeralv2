namespace JSini.Web.Components.Layout;

/// <summary>
/// 사용자 메뉴를 <b>바깥에서</b> 여닫는 손잡이.
/// </summary>
/// <remarks>
/// <para>
/// [<b>여는 쪽보다 닫는 쪽이 중요하다</b>]
/// </para>
///
/// <para>
/// 여는 자리는 둘이다 — 헤더의 얼굴과 휴대폰 아래 띠의 프로필 칸
/// (<c>MobileBottomNav</c>). 닫으라고 부르는 자리는 그보다 훨씬 많고,
/// <b>그쪽이 이 손잡이가 있는 까닭</b>이다.
/// </para>
///
/// <para>
/// 휴대폰에서 이 판은 헤더 아래를 <b>통째로 덮는다</b>(<c>app.css</c> 의
/// <c>max-width: 767px</c>). 덮개(<c>.jsini-user__scrim</c>)가 헤더를 덮지
/// 않으므로 판을 펴 둔 채로 헤더의 다른 단추가 눌리는데, 그때 이 판이 그대로
/// 남아 있으면 <b>열린 것이 판 뒤로 들어가 보이지 않는다</b> — 「빠른 지시를
/// 눌렀는데 아무 일도 안 난다」로 읽힌다.
/// </para>
///
/// <para>
/// 그래서 <b>오른쪽에서 나오는 판을 여는 쪽이 먼저 이 판을 접는다.</b>
/// </para>
///
/// <list type="table">
///   <item><term><c>HeaderTools</c></term>
///     <description>⚡ 빠른 지시(서랍·화면) · 노란 번개 요청 · ✉ 쪽지 · 🔔 알림함</description></item>
///   <item><term><c>AiChatDrawer</c></term>
///     <description>🤖 AI 대화 서랍</description></item>
///   <item><term><c>ThemeToggle</c></term>
///     <description>🎨 테마 서랍</description></item>
///   <item><term><c>MainLayout</c></term>
///     <description>☰ 사이드바를 펼 때(<c>OpenSidebarAsync</c>)</description></item>
/// </list>
///
/// <para>
/// <b>화면을 옮겨 가는 길은 여기로 오지 않는다.</b> 그쪽은 판 자신이
/// <c>NavigationManager.LocationChanged</c> 로 듣는다 — 옮기는 자리가 너무
/// 많아 하나씩 적으면 반드시 빠뜨리고, 빠뜨린 자리에서만 판이 남는다.
/// </para>
///
/// <para>scoped 다. 한 사람이 열었다고 모두의 판이 열리면 안 된다.</para>
/// </remarks>
public sealed class UserMenuDrawer
{
    public event Action<bool>? OpenRequested;
    public event Action? ToggleRequested;

    public void Open() => OpenRequested?.Invoke(true);
    public void Close() => OpenRequested?.Invoke(false);
    public void Toggle() => ToggleRequested?.Invoke();
}
