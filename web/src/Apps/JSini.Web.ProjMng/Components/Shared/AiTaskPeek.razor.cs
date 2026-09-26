using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class AiTaskPeek
{
    /// <summary>
    /// 볼 작업. <b>부모가 목록에서 다시 집어 넘긴다</b> — 그래서 돌고 있는
    /// 동안 상태와 총작업시간이 저절로 새것이 된다.
    /// </summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>창이 열려 있는가. <c>@@bind-Visible</c> 로 묶는다.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <inheritdoc cref="Visible"/>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>재시도·이어서 지시·확인·삭제가 일어났을 때 부모에게 알린다.</summary>
    [Parameter] public EventCallback<AiTaskDto> OnRetried { get; set; }

    private string HeadText => Item?.Title is { Length: > 0 } title ? title : "보낸 것";

    private Task OnChangedAsync(AiTaskDto task) => OnRetried.InvokeAsync(task);

    /// <summary>
    /// 확인을 마쳤거나 지웠다. <b>창을 닫는다</b> — 그 건은 목록에서 빠지므로
    /// 그대로 열어 두면 「그 건을 놓쳤습니다」만 남는다.
    /// </summary>
    private async Task OnConfirmedAsync(AiTaskDto task)
    {
        await CloseAsync();
        await OnRetried.InvokeAsync(task);
    }

    private Task CloseAsync() => OnVisibleChangedAsync(false);

    private Task OnVisibleChangedAsync(bool visible)
    {
        Visible = visible;

        return VisibleChanged.InvokeAsync(visible);
    }
}
