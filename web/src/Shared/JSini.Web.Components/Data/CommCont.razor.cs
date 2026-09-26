using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class CommCont
{
    /// <summary>
    /// 구역 이름. <b>자료 영역이 하나뿐이면 비워 둔다</b> — 화면 제목과
    /// 같은 말이 두 줄이 된다.
    /// </summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>제목 옆에 옅게 붙는 한마디(건수 · 기준 시각 따위).</summary>
    [Parameter] public string? Hint { get; set; }

    /// <summary>제목 줄 오른쪽 끝에 놓을 단추들. 표 자체의 단추는 여기가 아니라
    /// <c>CommGrd</c> 의 아래 띠에 선다.</summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>담을 내용. 보통 <c>CommGrd</c> 하나다.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>바깥에서 덧붙일 CSS 클래스.</summary>
    [Parameter] public string? CssClass { get; set; }
}
