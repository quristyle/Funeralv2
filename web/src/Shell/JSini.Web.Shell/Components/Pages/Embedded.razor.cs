using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Shell.Components.Pages;

public partial class Embedded
{
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>
    /// 접두사 뒤에 붙은 메뉴 경로 (<c>projmng/external/jsini</c>).
    /// 라우트가 포괄이라 <b>받는 속성이 반드시 있어야 한다</b> — 없으면
    /// Blazor 가 값 넣을 곳을 못 찾아 예외를 던진다.
    /// </summary>
    [Parameter] public string? MenuPath { get; set; }

    /// <summary>지금 주소. 사이드바가 건 링크와 같은 값이다.</summary>
    private string Href => EmbeddedRoute.HrefFor(MenuPath ?? string.Empty);

    /// <summary>
    /// 이 주소가 가리키는 메뉴.
    ///
    /// <para>
    /// <c>Trail</c> 을 쓰는 이유는 그것이 <b>거르기 전 원본</b>에서 찾기
    /// 때문이다. 걸러진 목록에서 찾으면 메뉴에서 숨긴 화면을 주소로 열었을 때
    /// 주소를 못 찾아 빈 칸이 뜬다. 줄기의 마지막 칸이 이 화면이다.
    /// </para>
    /// </summary>
    private MenuNode? Menu => Menus.Trail(Href).LastOrDefault();

    /// <summary>
    /// 띄울 주소. <b>정본은 메뉴관리다</b>(<c>scom.system_menus.iframe_src</c>).
    /// 화면은 아무 주소도 알고 있지 않다.
    /// </summary>
    private string? Source =>
        Menu?.IframeSrc is { Length: > 0 } src ? src : null;

    /// <summary>
    /// 새 창으로 연다. <c>noopener</c> 를 붙여 열린 창이 <c>window.opener</c> 로
    /// 이 포털을 건드리지 못하게 한다.
    /// </summary>
    private async Task OpenAsync()
    {
        if (Source is { } src)
        {
            await Js.InvokeVoidAsync("open", src, "_blank", "noopener");
        }
    }
}
