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

    /// <summary>창의 높이.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>창의 최대 높이. 없으면 폼이 화면보다 길어질 때 바닥 단추가 화면 밖으로 나간다.</summary>
    [Parameter] public string MaxHeight { get; set; } = "86vh";

    /// <summary>본문만 구르게 한다. 머리와 바닥은 제자리에 남는다.</summary>
    [Parameter] public bool Scrollable { get; set; } = true;

    /// <summary>바닥 띠를 보일지. 단추를 <see cref="FooterContentTemplate"/> 에 넣으면 켠다.</summary>
    [Parameter] public bool ShowFooter { get; set; } = true;

    /// <summary>바깥을 눌렀을 때 닫을지. 기본은 닫지 않는다 — 편집 창에서 닫히면 쓰던 내용이 사라진다.</summary>
    [Parameter] public bool CloseOnOutsideClick { get; set; }

    /// <summary>Esc 로 닫을지. 되돌릴 수 있는 창은 켜 두는 편이 손에 맞다.</summary>
    [Parameter] public bool CloseOnEscape { get; set; } = true;

    /// <summary>데스크톱에서 창 모서리를 잡아 크기를 조절할 수 있게 할지. 기본은 true 다.</summary>
    [Parameter] public bool AllowResize { get; set; } = true;

    /// <summary>창의 최소 너비.</summary>
    [Parameter] public string? MinWidth { get; set; }

    /// <summary>창의 최소 높이.</summary>
    [Parameter] public string? MinHeight { get; set; }

    /// <summary>휴대폰(767px 이하)에서 창이 화면을 꽉 채울지. 기본은 true 다. 확인 창(ConfirmDialog)처럼 짧은 대화창은 끈다.</summary>
    [Parameter] public bool FullOnMobile { get; set; } = true;

    /// <summary>바깥에서 덧붙일 CSS 클래스.</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <inheritdoc cref="CssClass"/>
    [Parameter] public string? HeaderCssClass { get; set; }

    /// <summary>창에 실제로 붙는 클래스. jsini-popup 은 항상 붙이고 모바일 꽉 채움일 때 jsini-popup--full-mobile 을 더한다. 휴대폰에서 창 너비가 픽셀로 박혀 있으면 화면보다 넓어져 양옆이 잘린다 — 화면마다 min() 으로 고쳐 적는 길도 있었지만 창이 서른 개가 넘어 빠뜨리면 휴대폰으로 그 창을 여는 사람이 나올 때까지 모른다. 그래서 표시만 붙이고 폭은 app.css 가 한 규칙으로 가둔다. DevExpress 자신의 달력·드롭다운은 좁은 화면을 따로 다루므로 같이 가두지 않는다.</summary>
    private string PopupClass
    {
        get
        {
            var baseClass = FullOnMobile ? "jsini-popup jsini-popup--full-mobile" : "jsini-popup";
            return string.IsNullOrWhiteSpace(CssClass)
                ? baseClass
                : $"{baseClass} {CssClass}";
        }
    }

    /// <summary>머리에 실제로 붙는 클래스. jsini-drag-head 는 항상 붙이고 화면이 준 것을 뒤에 잇는다. DevExpress 는 끌 수 있는 머리에 표시를 남기지 않는다 — 테마 규칙이 겹친 구조를 전제하는데 지금은 두 클래스가 한 요소에 있어 어느 것도 안 걸린다. 그래서 잡는 자리라는 표시는 우리가 붙이고, 화면이 준 HeaderCssClass 를 덮지 않는 것은 덮으면 그 화면의 머리 모양이 말없이 사라지기 때문이다.</summary>
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

    /// <summary>닫힘을 화면에 알린다. bind-Visible 로 묶은 화면은 VisibleChanged 가 값을 되돌려 주어야 창이 닫힌 것을 안다 — 안 넘기면 X 를 눌러도 화면의 편집 상태가 true 로 남아 다시 열리지 않는다.</summary>
    private Task OnVisibleChangedAsync(bool visible)
    {
        Visible = visible;
        return VisibleChanged.InvokeAsync(visible);
    }
}
