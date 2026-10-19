using Microsoft.AspNetCore.Components;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class _Pending
{
    /// <summary>이 업무 접두사 뒤의 나머지 경로. 라우트의 {*rest} 를 받는 속성이라 반드시 있어야 한다 — 없으면 Blazor 가 예외를 던져 안내 대신 오류가 뜬다.
    /// 값은 쓰지 않는다(주소는 NavigationManager 에서 읽는다).</summary>
    [Parameter] public string? Rest { get; set; }
}
