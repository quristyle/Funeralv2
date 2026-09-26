using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class PageNotice
{
    /// <summary>안내 문구. 비면 아무것도 그리지 않는다.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>문구 대신 마크업을 넣을 자리. 있으면 <see cref="Text"/> 보다 우선한다.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>안내의 성격. 색만 바뀐다.</summary>
    [Parameter] public NoticeTone Tone { get; set; } = NoticeTone.Info;
}
