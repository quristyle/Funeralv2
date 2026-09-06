namespace JSini.Web.Components.Layout;

/// <summary>
/// 테마 서랍을 <b>바깥에서</b> 여닫는 손잡이.
/// </summary>
/// <remarks>
/// <para>
/// 서랍의 여닫힘은 <c>ThemeToggle</c> 안의 상태였고, 그 부품의 팔레트 단추만
/// 그것을 만질 수 있었다. 사용자 메뉴에 「테마·크기」 항목을 넣으려면 다른
/// 부품이 열 수 있어야 한다.
/// </para>
///
/// <para>
/// <b>부품끼리 참조하게 두지 않는다.</b> <c>UserMenu</c> 가 <c>ThemeToggle</c> 을
/// <c>@ref</c> 로 잡으려면 둘이 같은 부모 마크업 안에 있어야 하고, 그러면
/// 헤더의 배치가 바뀔 때마다 이 연결이 끊긴다. 사이에 scoped 서비스 하나를
/// 두면 자리와 무관해진다 — 잠금화면(<c>ScreenLock</c>)과 같은 구도다.
/// </para>
///
/// <para>scoped 다. 한 사람이 열었다고 모두의 서랍이 열리면 안 된다.</para>
/// </remarks>
public sealed class ThemeDrawer
{
    /// <summary>여닫으라는 요청. <c>ThemeToggle</c> 이 받는다.</summary>
    public event Action<bool>? OpenRequested;

    /// <summary>서랍을 연다.</summary>
    public void Open() => OpenRequested?.Invoke(true);

    /// <summary>서랍을 닫는다.</summary>
    public void Close() => OpenRequested?.Invoke(false);
}
