using JSini.Web.Abstractions;
using JSini.Web.Components.Security;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

/// <summary>
/// 포털 화면의 공통 뿌리. 화면마다 되풀이되던 주입과 권한 판정을 한곳에 둔다.
/// </summary>
/// <remarks>
/// <para>
/// 상속 줄기는 <c>BasePage</c> → <see cref="DataPage"/> → <see cref="AutoRefreshPage"/> 다.
/// 조회·저장을 하는 화면은 <see cref="DataPage"/> 를, 그렇지 않은 화면은 이것을 상속한다.
/// </para>
/// <para>
/// 권한은 <see cref="Can"/> 으로 묻는다. <c>PermissionView</c> 와 <b>같은 경로 규칙</b>
/// (<see cref="PermissionPath"/>)을 쓰므로, 감춘 단추와 화면의 판정이 갈리지 않는다.
/// </para>
/// </remarks>
public abstract class BasePage : ComponentBase
{
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    [Inject] protected IPermissionContext Permissions { get; set; } = default!;

    /// <summary>권한을 따지는 지금 화면의 경로(쿼리스트링·앵커를 뗀 것).</summary>
    protected string CurrentPath => PermissionPath.Current(Navigation);

    /// <summary>지금 화면에서 이 동작을 할 수 있는가.</summary>
    protected bool Can(MenuAction action) => Permissions.Can(CurrentPath, action);
}
