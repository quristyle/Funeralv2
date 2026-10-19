using Microsoft.AspNetCore.Components;
using JSini.Web.Models;

namespace JSini.Web.Components.Settings;

public partial class NoteWritePopup
{
    /// <summary>창이 열려 있는가. <c>@@bind-Visible</c> 로 묶는다.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <inheritdoc cref="Visible"/>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>
    /// 보내고 난 뒤. 상단 띠의 안 읽은 수를 다시 세는 쪽이 받는다.
    /// </summary>
    [Parameter] public EventCallback<NoteSendResultDto> OnSent { get; set; }

    /// <summary>받는 사람 칸에 미리 넣어 둘 아이디들. 답장이 쓴다. 판은 처음 만들어질 때 한 번만 읽으므로, 상대가 매번 달라지는 자리는 창 자체를 새로 만든다(@if 로 감싸거나 @key).</summary>
    [Parameter] public IReadOnlyList<string>? DefaultTo { get; set; }

    /// <summary>화면을 꽉 채울까. 휴대폰에서 참이다 — 상단 띠의 ✉ 만 이 값을 주고(IsPhone), 다른 자리는 안 주므로 전처럼 창으로 뜬다.</summary>
    [Parameter] public bool Fill { get; set; }

    /// <summary>창에 입히는 클래스. 채우는 모습에서만 하나가 더 붙는다.</summary>
    private string PopupClass => Fill
        ? "jsini-note-popup jsini-note-popup--full"
        : "jsini-note-popup";

    private Task OnVisibleChangedAsync(bool visible)
    {
        Visible = visible;
        return VisibleChanged.InvokeAsync(visible);
    }

    /// <summary>보냈으면 창을 닫는다. 결과는 토스트가 이미 말했다 — 창을 열어 둔 채 두면 사람이 쪽지를 한 번 더 보낸다.</summary>
    private async Task OnSentAsync(NoteSendResultDto result)
    {
        await OnVisibleChangedAsync(false);
        await OnSent.InvokeAsync(result);
    }
}
