using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;

namespace JSini.Web.Components.Data;

public partial class CommPopup
{
    /// <summary>창이 열려 있는가. <c>@@bind-Visible</c> 로 묶는다.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <inheritdoc cref="Visible"/>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>머리에 쓸 글자. 머리를 직접 그리려면 <see cref="HeaderContentTemplate"/> 를 쓴다.</summary>
    [Parameter] public string? HeaderText { get; set; }

    /// <summary>창의 너비(<c>520</c> · <c>min(640px, calc(100vw - 2rem))</c>).</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>
    /// 창의 최대 높이. 기본값이 있는 이유는 위 「키가 큰 폼」 주석에 적었다 —
    /// 없으면 폼이 화면보다 길어질 때 바닥 단추가 화면 밖으로 나간다.
    /// </summary>
    [Parameter] public string MaxHeight { get; set; } = "86vh";

    /// <summary>본문만 구르게 한다. 머리와 바닥은 제자리에 남는다.</summary>
    [Parameter] public bool Scrollable { get; set; } = true;

    /// <summary>바닥 띠를 보일지. 단추를 <see cref="FooterContentTemplate"/> 에 넣으면 켠다.</summary>
    [Parameter] public bool ShowFooter { get; set; } = true;

    /// <summary>
    /// 바깥을 눌렀을 때 닫을지. <b>기본은 닫지 않는다</b> — 편집 창에서
    /// 그것은 쓰던 내용이 사라지는 일이다.
    /// </summary>
    [Parameter] public bool CloseOnOutsideClick { get; set; }

    /// <summary>Esc 로 닫을지. 되돌릴 수 있는 창은 켜 두는 편이 손에 맞다.</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    /// <summary>바깥에서 덧붙일 CSS 클래스.</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <inheritdoc cref="CssClass"/>
    [Parameter] public string? HeaderCssClass { get; set; }

    /// <summary>
    /// 창에 실제로 붙는 클래스. <c>jsini-popup</c> 을 <b>항상</b> 붙이고
    /// 화면이 준 것을 뒤에 잇는다.
    ///
    /// <para>
    /// <b>휴대폰에서 창이 화면보다 넓다.</b> 창 너비는 화면마다 픽셀로 박혀
    /// 있고(420 · 560 · 880 …) 그 값은 데스크톱을 보고 고른 것이다. 폭이
    /// 360px 인 기기에서 420px 짜리 창이 뜨면 DevExpress 가 그것을 가운데
    /// 두므로 <b>양옆이 똑같이 잘린다</b> — 글도 단추도 화면 밖이다.
    /// </para>
    ///
    /// <para>
    /// 화면마다 <c>min(420px, calc(100vw - 2rem))</c> 으로 고쳐 적는 길도
    /// 있었지만(그렇게 해 둔 화면이 넷 있다) 창이 서른 개가 넘어 반드시
    /// 몇 개를 빠뜨리고, 빠뜨린 창은 <b>휴대폰으로 그 창을 여는 사람이
    /// 나올 때까지 아무도 모른다.</b> 그래서 표시만 붙이고 폭은 app.css 가
    /// 한 규칙으로 가둔다.
    /// </para>
    ///
    /// <para>
    /// DevExpress 자신의 창(달력·목록 드롭다운, 바닥에서 올라오는 판)까지
    /// 한꺼번에 겨누지 않는 이유는 그쪽이 이미 좁은 화면을 따로 다루기
    /// 때문이다 — 같이 가두면 바닥 판이 화면을 다 못 채운다.
    /// </para>
    /// </summary>
    private string PopupClass => string.IsNullOrWhiteSpace(CssClass)
        ? "jsini-popup"
        : $"jsini-popup {CssClass}";

    /// <summary>
    /// 머리에 실제로 붙는 클래스. <c>jsini-drag-head</c> 를 <b>항상</b> 붙이고
    /// 화면이 준 것을 뒤에 잇는다.
    ///
    /// <para>
    /// <b>DevExpress 는 끌 수 있는 머리에 아무 표시도 남기지 않는다.</b>
    /// 테마 CSS 에 <c>.dxbl-drag</c> 를 겨눈 규칙이 있어서 그것을 붙여 주는
    /// 줄 알았는데, 떠 있는 화면에서 재어 보니 머리의 클래스는
    /// <c>dxbl-modal-header dxbl-popup-header</c> 뿐이었다. 그 규칙들은
    /// <c>.dxbl-popup-header &gt; .dxbl-modal-header</c> 처럼 <b>겹친 구조</b>를
    /// 전제하는데 지금은 두 클래스가 한 요소에 함께 있어, 어느 것도 걸리지
    /// 않는다.
    /// </para>
    ///
    /// <para>
    /// 그러니 잡는 자리라는 표시는 <b>우리가 붙여야 한다</b>. 화면이 준
    /// <see cref="HeaderCssClass"/> 를 덮지 않고 잇는 이유는, 덮으면 그 화면의
    /// 머리 모양이 말없이 사라지기 때문이다.
    /// </para>
    /// </summary>
    private string HeaderClass => string.IsNullOrWhiteSpace(HeaderCssClass)
        ? "jsini-drag-head"
        : $"jsini-drag-head {HeaderCssClass}";

    /// <inheritdoc cref="CssClass"/>
    [Parameter] public string? FooterCssClass { get; set; }

    /// <summary>머리를 직접 그린다. 주면 <see cref="HeaderText"/> 대신 이것이 나온다.</summary>
    [Parameter] public RenderFragment<IPopupElementInfo>? HeaderContentTemplate { get; set; }

    /// <summary>창의 본문. 보통 <c>DxFormLayout</c> 하나다.</summary>
    [Parameter] public RenderFragment<IPopupElementInfo>? BodyContentTemplate { get; set; }

    /// <summary>바닥 띠의 단추들. 저장이 왼쪽, 닫기가 오른쪽이다.</summary>
    [Parameter] public RenderFragment<IPopupElementInfo>? FooterContentTemplate { get; set; }

    /// <summary>
    /// 닫힘을 화면에 알린다.
    ///
    /// <para>
    /// <c>@@bind-Visible</c> 로 묶은 화면은 <see cref="VisibleChanged"/> 가
    /// 값을 되돌려 주어야 창이 닫힌 것을 안다. 그것을 안 넘기면 X 를 눌러도
    /// 화면의 <c>_editing</c> 이 <c>true</c> 로 남아 다시 열리지 않는다.
    /// </para>
    /// </summary>
    private Task OnVisibleChangedAsync(bool visible)
    {
        Visible = visible;
        return VisibleChanged.InvokeAsync(visible);
    }
}
