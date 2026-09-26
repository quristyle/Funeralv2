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

    /// <summary>
    /// 받는 사람 칸에 미리 넣어 둘 아이디들. 「답장」이 쓴다.
    /// </summary>
    /// <remarks>
    /// <b>판은 이 값을 처음 만들어질 때 한 번만 읽는다</b>(<c>OnInitialized</c>).
    /// 창을 하나 두고 상대만 바꿔 열면 두 번째부터 옛 사람이 그대로 남으므로,
    /// 답장처럼 상대가 매번 달라지는 자리는 <b>창 자체를 새로 만든다</b>
    /// (<c>@@if</c> 로 감싸거나 <c>@@key</c> 를 준다).
    /// </remarks>
    [Parameter] public IReadOnlyList<string>? DefaultTo { get; set; }

    private Task OnVisibleChangedAsync(bool visible)
    {
        Visible = visible;
        return VisibleChanged.InvokeAsync(visible);
    }

    /// <summary>
    /// 보냈으면 창을 닫는다.
    /// </summary>
    /// <remarks>
    /// <b>결과는 토스트가 이미 말했다.</b> 창을 열어 둔 채로 두면 사람은 같은
    /// 쪽지를 한 번 더 보낸다 — `NotifySendPopup` 이 막힌 경우에도 닫는 것과
    /// 같은 까닭이다.
    /// </remarks>
    private async Task OnSentAsync(NoteSendResultDto result)
    {
        await OnVisibleChangedAsync(false);
        await OnSent.InvokeAsync(result);
    }
}
