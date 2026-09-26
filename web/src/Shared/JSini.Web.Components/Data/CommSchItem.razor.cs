using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class CommSchItem
{
    /// <summary>칸 이름. 비우면 라벨 없이 입력 칸만 그린다.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>입력 칸. 폭은 여기 넣는 부품이 정한다(<c>Style="width: 240px"</c>).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
