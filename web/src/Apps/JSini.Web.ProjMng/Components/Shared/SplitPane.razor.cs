using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class SplitPane
{
    /// <summary>왼쪽 창의 너비 비율(%). 좁은 화면에서는 위아래로 반씩 나눈다.</summary>
    [Parameter] public int Size { get; set; } = 35;

    [Parameter] public RenderFragment? First { get; set; }

    [Parameter] public RenderFragment? Second { get; set; }

    private bool _isNarrow;
}
