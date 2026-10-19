using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class _Pending
{
    /// <summary>이 업무 접두사 뒤에 붙은 나머지 경로. 라우트의 {*rest} 를 받는 속성이 반드시 있어야 한다 — 없으면 Blazor 가 예외를 던진다. 값은 쓰지 않고 받아 주기 위해서만 있다.</summary>
    [Parameter] public string? Rest { get; set; }
}
