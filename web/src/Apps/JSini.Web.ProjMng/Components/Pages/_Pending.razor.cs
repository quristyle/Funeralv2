using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class _Pending
{
    /// <summary>이 업무 접두사 뒤에 붙은 나머지 경로. 라우트의 {*rest} 를 받는 속성이라 반드시 있어야 한다 — 없으면 Blazor 가 값을 넣을 곳을 못 찾아 안내 대신 오류가 뜬다. 값은 쓰지 않는다(안내 화면은 NavigationManager 에서 주소를 읽는다).</summary>
    [Parameter] public string? Rest { get; set; }
}
