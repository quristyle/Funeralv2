using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;

namespace JSini.Web.Components.Security;

public partial class PermissionView
{
    [Inject] private IPermissionContext Permissions { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>권한이 있을 때 그릴 내용.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 권한이 없을 때 그릴 내용. 보통 비워 둔다(= 아무것도 안 보인다).
    /// 자리를 남겨야 하는 표 안에서만 쓴다.
    /// </summary>
    [Parameter] public RenderFragment? NotAllowed { get; set; }

    /// <summary>따질 동작. 기본은 열람.</summary>
    [Parameter] public MenuAction Action { get; set; } = MenuAction.View;

    /// <summary>
    /// 권한을 따질 화면 경로. 비우면 지금 열려 있는 화면의 경로를 쓴다.
    /// </summary>
    [Parameter] public string? Path { get; set; }

    private bool _allowed;

    protected override void OnParametersSet()
    {
        // 경로를 꺼내는 규칙은 `PermissionPath` 한 곳에 둔다. 화면이 스스로
        // 판정해야 하는 자리(끌어 옮기기 따위)가 생겨서 두 벌이 되었고,
        // 두 벌이 갈리면 감춘 단추와 화면의 판정이 어긋난다.
        var path = string.IsNullOrWhiteSpace(Path) ? PermissionPath.Current(Navigation) : Path;
        _allowed = Permissions.Can(path, Action);
    }
}
