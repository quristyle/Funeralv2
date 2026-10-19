using Microsoft.AspNetCore.Components;

namespace JSini.Web.Funeral.Components.Pages;

public partial class _Pending
{
    /// <summary>포괄 라우트 {*rest} 를 받는 속성. 값은 쓰지 않는다. 없으면 Blazor 가 예외를 던진다.</summary>
    [Parameter] public string? Rest { get; set; }
}
