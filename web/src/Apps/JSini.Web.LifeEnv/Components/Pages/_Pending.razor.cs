using Microsoft.AspNetCore.Components;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class _Pending
{
    /// <summary>
    /// 이 업무 접두사 뒤에 붙은 나머지 경로.
    ///
    /// <b>반드시 있어야 한다.</b> 라우트에 <c>{*rest}</c> 를 적어 놓고 받는
    /// 속성이 없으면 Blazor 가 값을 넣을 곳을 못 찾아 예외를 던진다 —
    /// 화면이 안내 대신 오류로 뜬다. 실제로 그렇게 되어 있었다.
    ///
    /// 값 자체는 쓰지 않는다 — 안내 화면은 <c>NavigationManager</c> 에서
    /// 주소를 직접 읽는다. <b>받아 주기 위해서만</b> 있는 속성이다.
    /// </summary>
    [Parameter] public string? Rest { get; set; }
}
