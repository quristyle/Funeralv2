using Microsoft.AspNetCore.Components;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class RequestGrid : RequestRowList
{
    /// <summary>
    /// 한 쪽에 깔리는 줄 수. 화면이 휴대폰의 「더보기」와 <b>같은 수</b>를
    /// 준다 — 「다음 쪽」이 기기마다 다르면 같은 조건으로 띄운 두 화면이
    /// 서로 다른 자리에서 끊긴다.
    /// </summary>
    [Parameter] public int PageSize { get; set; } = 25;

    /// <summary>
    /// 표의 모습(칸 너비 · 정렬 · 칸별 검색)과 구른 자리를 맡기는 이름.
    /// 화면이 쓰는 것과 같은 이름이다.
    /// </summary>
    [Parameter] public string? StateKey { get; set; }

    /// <summary>오른쪽 클릭 창의 「다시 읽기」.</summary>
    [Parameter] public EventCallback Reload { get; set; }

    /// <summary>보고 있던 줄. 떠날 때 번호로 맡겨 두는 그 값이다.</summary>
    [Parameter] public ImprovementRequest? Selected { get; set; }

    [Parameter] public EventCallback<ImprovementRequest?> SelectedChanged { get; set; }
}
