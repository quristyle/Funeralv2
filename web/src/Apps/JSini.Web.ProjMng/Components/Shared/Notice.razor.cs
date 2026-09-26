using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class Notice
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>안내의 성격. 색만 바뀐다.</summary>
    [Parameter] public NoticeTone Tone { get; set; } = NoticeTone.Info;
}
