using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class RequestCards : RequestRowList
{
    /// <summary>
    /// 줄이 하나도 없을 때 적을 말. <c>CommGrd</c> 의 빈 표와 <b>같은 글자</b>를
    /// 기본값으로 둔다 — 기기를 바꿨다고 없다는 말이 달라지면 안 된다.
    /// </summary>
    [Parameter] public string EmptyText { get; set; } = "표시할 자료가 없습니다.";

    /// <summary>
    /// 누름쇠로도 열린다 — Enter 와 Space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 줄은 <c>li</c> 라 저절로 눌리지 않는다(<c>role="button"</c> ·
    /// <c>tabindex="0"</c> 로 눌리는 자리라고 알릴 뿐이다). 그래서 진짜 단추가
    /// 저절로 해 주던 일을 여기서 해 준다.
    /// </para>
    /// <para>
    /// <b>줄을 진짜 단추로 만들지 않은 까닭</b>은 안에 또 눌리는 것(그림)이
    /// 있어서다 — 단추 안의 단추는 HTML 이 금하고, 브라우저마다 다르게 풀린다.
    /// </para>
    /// </remarks>
    private Task OnKeyDownAsync(KeyboardEventArgs e, ImprovementRequest r) =>
        e.Key is "Enter" or " " ? RowClick.InvokeAsync(r) : Task.CompletedTask;
}
