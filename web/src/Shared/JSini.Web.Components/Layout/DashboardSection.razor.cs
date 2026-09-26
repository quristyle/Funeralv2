using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class DashboardSection
{
    [Parameter] public string? Title { get; set; }
    [Parameter] public string? Hint { get; set; }
    [Parameter] public string? CssClass { get; set; }
    [Parameter] public RenderFragment? Actions { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
