using Microsoft.AspNetCore.Components;

namespace JSini.Web.Funeral.Components.Shared;

public partial class DeviceField
{
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>한 줄을 통째로 쓰는 칸인가.</summary>
    [Parameter] public bool Wide { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
