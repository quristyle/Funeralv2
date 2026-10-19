using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class _Pending
{
    /// <summary>
    /// 라우트의 {*rest} 를 받는 속성. 없으면 Blazor 가 예외를 던져 안내 대신 오류가 뜬다. 값은 쓰지 않는다 — 안내 화면은 NavigationManager 에서 주소를 직접 읽는다.
    /// </summary>
    [Parameter] public string? Rest { get; set; }
}
