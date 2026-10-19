using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class CommMoreFlyout<TItem>
{
    /// <summary>판의 상태. 표가 들고 있다가 「⋮」 를 누를 때 바꾼다.</summary>
    [Parameter, EditorRequired] public CommMoreMenu Menu { get; set; } = default!;

    /// <summary>판 안에 놓을 단추들. 그 줄의 자료와 차례 번호를 받는다.</summary>
    [Parameter, EditorRequired] public RenderFragment<CommMoreRow<TItem>> Items { get; set; } = default!;

    /// <summary>
    /// 판이 열리거나 닫힌 것을 표에 알린다. 표가 「⋮」 의 강조를 다시 그리려면
    /// 이 알림이 있어야 한다 — 판만 다시 그리면 칸의 강조가 남는다.
    /// </summary>
    [Parameter] public EventCallback Changed { get; set; }

    private async Task OnOpenChangedAsync(bool open)
    {
        Menu.SetOpen(open);
        await Changed.InvokeAsync();
    }

    private async Task CloseAsync()
    {
        Menu.Close();
        await Changed.InvokeAsync();
    }
}
