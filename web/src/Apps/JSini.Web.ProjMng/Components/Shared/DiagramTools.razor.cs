using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class DiagramTools
{
    /// <summary>그림을 그려 줄 부품. 도구상자는 그 안을 모르고 칸을 채워 달라고만 한다.</summary>
    [Parameter] public DiagramViewer? Viewer { get; set; }

    /// <summary>그린 뒤 빈 칸을 채운다. 렌더마다 부른다 — 거르개나 묶음이 바뀌면 새 칸이 생기고, 그려 둔 칸은 건너뛴다.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Open && Viewer is not null)
        {
            await Viewer.PaintPreviewsAsync();
        }
    }

    /// <summary>미니맵이 켜져 있는가. 그림 부품이 정본이다 — 따로 들고 있으면 단추와 미니맵이 어긋난다.</summary>
    private bool MinimapOn => Viewer?.MinimapOn ?? false;

    /// <summary>미니맵을 켜고 끈다. 누르면 다시 그려지므로 단추의 불빛이 따라온다.</summary>
    private async Task ToggleMinimapAsync()
    {
        if (Viewer is null)
        {
            return;
        }

        await Viewer.ToggleMinimapAsync();
    }

    /// <summary>고를 수 있는 바탕. Kind 는 JS 가 아는 값이라 바꾸면 diagram-viewer.js 의 setBackground 도 함께 고친다.</summary>
    private static readonly (string Kind, string Label, string Tip)[] Backgrounds =
    [
        ("none", "없음", "바탕을 비웁니다 — 도형을 아무 자리에나 놓습니다"),
        ("grid", "격자", "격자를 깔고, 도형이 그 칸에 자석처럼 붙습니다 (Alt 를 누른 채 끌면 잠시 안 붙습니다)"),
        ("dots", "점", "점을 찍고, 도형이 그 점에 자석처럼 붙습니다 (Alt 를 누른 채 끌면 잠시 안 붙습니다)"),
    ];

    /// <summary>지금 바탕. 미니맵과 같은 이유로 그림 부품이 정본이다.</summary>
    private string Background => Viewer?.Background ?? "none";

    /// <summary>바탕을 고른다.</summary>
    private async Task SetBackgroundAsync(string kind)
    {
        if (Viewer is null)
        {
            return;
        }

        await Viewer.SetBackgroundAsync(kind);
    }

    /// <summary>찾는 글자. 비어 있으면 전부 보여 준다.</summary>
    private string _filter = string.Empty;

    /// <summary>찾는 글자에 걸리는 도형. 이름과 묶음 이름을 함께 본다 — 「순서도」로 묶음째 좁히는 일이 흔하다.</summary>
    private IReadOnlyList<DiagramViewer.DiagramShape> Matched =>
        string.IsNullOrWhiteSpace(_filter)
            ? Shapes
            : [.. Shapes.Where(s =>
                  (s.Label?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false)
                  || (s.Group?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false))];

    /// <summary>묶음을 펼쳐 둘지. 찾는 중이면 전부 펼친다 — 걸린 것이 접힌 묶음 안에 있으면 없다고 보인다. 평소엔 우리 묶음만 펼친다(stencil 묶음은 백여든 개라 접는다).</summary>
    private bool IsOpen(string? group) =>
        !string.IsNullOrWhiteSpace(_filter)
        || group is "기본" or "순서도" or "UML" or "ERD" or "구성도";

    /// <summary>도형 목록. 화면이 <c>DiagramViewer.ShapesAsync</c> 로 받아 넘긴다.</summary>
    [Parameter] public IReadOnlyList<DiagramViewer.DiagramShape> Shapes { get; set; } = [];

    /// <summary>선 모양 목록.</summary>
    [Parameter] public IReadOnlyList<DiagramViewer.DiagramShape> EdgeStyles { get; set; } = [];

    /// <summary>도구상자가 보이는가.</summary>
    [Parameter] public bool Open { get; set; } = true;

    [Parameter] public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>핀이 꽂혀 있는가(머리말).</summary>
    [Parameter] public bool Pinned { get; set; } = true;

    [Parameter] public EventCallback<bool> PinnedChanged { get; set; }

    /// <summary>도형을 골랐다. 값은 <c>kind</c> 다.</summary>
    [Parameter] public EventCallback<string> OnShape { get; set; }

    /// <summary>선 모양을 골랐다.</summary>
    [Parameter] public EventCallback<string> OnEdgeStyle { get; set; }
}
