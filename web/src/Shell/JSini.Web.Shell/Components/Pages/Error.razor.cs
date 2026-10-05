using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Diagnostics;

namespace JSini.Web.Shell.Components.Pages;

public partial class Error
{
    [CascadingParameter] private HttpContext? HttpContext { get; set; }

    private string? _requestId;

    /// <summary>
    /// 화면에 적는 번호. <b>기록을 남기는 쪽과 같은 함수로 짓는다</b> —
    /// 둘이 갈라지면 사용자가 불러 주는 번호로 표를 못 찾는다
    /// (<see cref="TraceNumber"/> 머리말).
    /// </summary>
    protected override void OnInitialized() => _requestId = TraceNumber.Of(HttpContext);
}
