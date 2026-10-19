using Microsoft.AspNetCore.Components.Web;

namespace JSini.Web.Components.Layout;

/// <summary>보고 있는 탭의 메뉴를 여기서 열어 달라는 부탁. 휴대폰에서 헤더의 브레드크럼이 하고 탭 줄(TabBar)이 받는다. 휴대폰은 탭 줄을 통째로 감추는데 그 바람에 탭 메뉴로만 할 수 있는 일(새로 고침 · 고정 · 즐겨찾기 · URL 복사 · 닫기)까지 사라졌다 — 그래서 열린 화면의 이름이 있는 로고 옆에 여는 길을 옮겼다.
/// 브레드크럼과 탭 줄은 형제도 부모 자식도 아니라 파라미터로 잇지 못해 MenuReveal 과 같은 까닭으로 통을 둔다. 창을 하나 더 만들지 않는다 — 같은 항목 두 벌이 생기면 같은 자리에서 할 수 있는 일이 여는 쪽에 따라 달라진다. 누른 자리를 함께 넘긴다: 탭 줄의 「⋯」 자리는 휴대폰에서 감춰진 상자라 그것으로 열면 창이 엉뚱한 구석에 뜬다. scoped — 회로 하나가 사용자 한 명이다.</summary>
public sealed class TabMenuRequest
{
    /// <summary>탭 메뉴를 열어 달라. 값은 누른 자리(마우스·손가락)다.</summary>
    public event Action<MouseEventArgs>? Requested;

    /// <summary>누른 자리에 탭 메뉴를 연다. 탭이 하나도 없으면 탭 줄이 그려지지 않아 아무 일도 일어나지 않는다 — 부르는 쪽과 탭을 만드는 쪽이 같은 조건(메뉴 줄기를 찾았는가)을 보므로 이름이 보이면 그 탭은 이미 열려 있다.</summary>
    public void Request(MouseEventArgs args) => Requested?.Invoke(args);
}
