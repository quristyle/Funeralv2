using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

namespace JSini.Web.Shell.Components;

public partial class App
{
    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;

    /// <summary>이 화면을 회로로 그릴지 정적 SSR 로 그릴지. 셸에만 이 분기가 있다. 쿠키를 굽는 화면(로그인)이 셸에만 있어서다. 해당 화면은 스스로 [ExcludeFromInteractiveRouting] 을 달고, 여기서는 그 표시를 본다. 화면 이름을 나열하면 새 화면에서 빠뜨려 로그인이 조용히 안 된다.</summary>
    private IComponentRenderMode? RenderModeForPage =>
        HttpContext.AcceptsInteractiveRouting() ? RenderMode.InteractiveServer : null;
}
