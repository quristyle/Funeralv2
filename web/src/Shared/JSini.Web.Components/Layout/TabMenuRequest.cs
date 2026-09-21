using Microsoft.AspNetCore.Components.Web;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 「보고 있는 탭의 메뉴를 여기서 열어 달라」는 부탁. 휴대폰에서 헤더의
/// 브레드크럼이 하고 탭 줄(<c>TabBar</c>)이 받는다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 필요한가]
/// </para>
///
/// <para>
/// 휴대폰에서는 탭 줄을 통째로 감춘다(<c>app.css</c> 의 ≤767px). 360px 짜리
/// 화면에서 탭 열둘을 늘어놓을 자리가 없어서인데, 그 바람에 <b>탭 메뉴로만
/// 할 수 있는 일</b>(새로 고침 · 고정 · 즐겨찾기 · URL 복사 · 닫기)까지 함께
/// 사라졌다. 열린 화면의 이름은 로고 옆에 남아 있으므로 여는 길을 그리로 옮긴다.
/// </para>
///
/// <para>
/// [<c>MenuReveal</c> 과 같은 까닭으로 통을 둔다]
/// </para>
///
/// <para>
/// 브레드크럼은 헤더 안이고 탭 줄은 본문 판 안이라 <b>형제도 부모 자식도
/// 아니다.</b> 파라미터로 이으려면 레이아웃이 「지금 메뉴를 열어야 하나」를
/// 상태로 들고 있어야 하는데, 그러면 레이아웃이 남의 화면 상태를 대신 드는
/// 꼴이 된다(<see cref="MenuReveal"/> 머리말).
/// </para>
///
/// <para>
/// [창을 하나 더 만들지 않는다]
/// </para>
///
/// <para>
/// 브레드크럼이 <c>DxContextMenu</c> 를 따로 그리는 길도 있었다. 그러면 같은
/// 항목 여남은 개가 두 벌이 되고, 한쪽만 고치는 날 <b>같은 자리에서 할 수
/// 있는 일이 어느 쪽으로 열었느냐에 따라 달라진다</b> — 탭 줄이 오른쪽 클릭과
/// 「⋯」 에 창 하나를 나눠 쓰는 것과 같은 이유다(<c>TabBar.razor</c> 머리말).
/// </para>
///
/// <para>
/// [누른 자리를 함께 넘긴다]
/// </para>
///
/// <para>
/// 받는 쪽이 <c>ShowAsync(MouseEventArgs)</c> 로 <b>손가락이 닿은 자리</b>에
/// 연다. 탭 줄의 「⋯」 에 붙여 둔 자리(<c>PositionTarget</c>)는 휴대폰에서
/// 감춰진 상자라 그것으로 열면 창이 엉뚱한 구석에 뜬다.
/// </para>
///
/// <para>
/// scoped 다 — 회로 하나가 곧 사용자 한 명의 창 하나다.
/// </para>
/// </remarks>
public sealed class TabMenuRequest
{
    /// <summary>탭 메뉴를 열어 달라. 값은 누른 자리(마우스·손가락)다.</summary>
    public event Action<MouseEventArgs>? Requested;

    /// <summary>
    /// 누른 자리에 탭 메뉴를 연다.
    /// </summary>
    /// <remarks>
    /// 탭이 하나도 없으면 탭 줄 자체가 그려지지 않아 <b>아무 일도 일어나지
    /// 않는다.</b> 그런 상태로 부를 일은 없다 — 부르는 쪽(브레드크럼)과 탭을
    /// 만드는 쪽(<c>MainLayout.Track</c>)이 <b>같은 조건</b>(메뉴 줄기를
    /// 찾았는가)을 보기 때문에, 이름이 보이면 그 탭은 이미 열려 있다.
    /// </remarks>
    public void Request(MouseEventArgs args) => Requested?.Invoke(args);
}
