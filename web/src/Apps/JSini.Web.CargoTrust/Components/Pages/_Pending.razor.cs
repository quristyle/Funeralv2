using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class _Pending
{
    /// <summary>
    /// 포괄 라우트의 나머지 경로. {*rest} 를 받으려면 이 속성이 있어야 한다(없으면 Blazor 가 예외를 던진다). 값은 쓰지 않는다.
    /// </summary>
    [Parameter] public string? Rest { get; set; }
}
