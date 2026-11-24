using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class CommSchItem
{
    /// <summary>칸 이름. 비우면 라벨 없이 입력 칸만 그린다.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>입력 칸. 폭은 여기 넣는 부품이 정한다(<c>Style="width: 240px"</c>).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    // 「~」 라벨은 앞 칸과 이어지는 기간의 뒷날짜다 — 테두리에 얹지 않고 앞 칸 쪽에 붙인다(tabler.css).
    private string ItemClass => Label?.Trim() == "~" ? "commsch__item commsch__item--joint" : "commsch__item";
}
