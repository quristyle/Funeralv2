using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Shell.Components.Pages;

public partial class Embedded
{
    [Inject] private IMenuProvider Menus { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>접두사 뒤에 붙은 메뉴 경로(<c>projmng/external/jsini</c>). 라우트가 포괄이라 받는 속성이 반드시 있어야 한다.</summary>
    [Parameter] public string? MenuPath { get; set; }

    /// <summary>지금 주소. 사이드바가 건 링크와 같은 값이다.</summary>
    private string Href => EmbeddedRoute.HrefFor(MenuPath ?? string.Empty);

    /// <summary>이 주소가 가리키는 메뉴. Trail 은 거르기 전 원본에서 찾는다. 걸러진 목록에서 찾으면 메뉴에서 숨긴 화면을 주소로 열었을 때 빈 칸이 뜬다.</summary>
    private MenuNode? Menu => Menus.Trail(Href).LastOrDefault();

    /// <summary>띄울 주소. 정본은 메뉴관리(scom.system_menus.iframe_src)다. 화면은 아무 주소도 알고 있지 않다.</summary>
    private string? Source =>
        Menu?.IframeSrc is { Length: > 0 } src ? src : null;

    /// <summary>새 창으로 연다. noopener 를 붙여 열린 창이 window.opener 로 이 포털을 건드리지 못하게 한다.</summary>
    private async Task OpenAsync()
    {
        if (Source is { } src)
        {
            await Js.InvokeVoidAsync("open", src, "_blank", "noopener");
        }
    }
}
