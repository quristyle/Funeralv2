using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 메뉴를 기준으로 권한 현황을 거꾸로 읽는 서비스.
/// </summary>
/// <remarks>
/// <c>/system/role-map</c> 은 역할에서 출발한다 — "이 역할은 어떤 메뉴를 쓰나".
/// 이쪽은 반대다 — <b>"이 메뉴는 누가 쓸 수 있나"</b>.
///
/// <para>
/// <b>쓰기는 여기서 하지 않는다.</b> 이미 있는 것을 그대로 쓴다.
/// </para>
/// <list type="bullet">
///   <item>역할↔메뉴 권한 → <c>POST /system/role-permission/roles/{roleId}/menus/save</c></item>
///   <item>역할↔회사·부서·사람 → <c>POST /system/role-scope/assign</c> · <c>/remove</c></item>
/// </list>
/// <para>
/// 같은 일을 하는 저장 경로를 두 개 만들면 한쪽에만 규칙이 붙는다.
/// 이 서비스는 <b>읽기 전용</b>으로 둔다.
/// </para>
/// </remarks>
public interface IMenuRoleService
{
    /// <summary>
    /// 메뉴 하나의 권한 현황. 메뉴를 못 찾으면 null 이다.
    /// </summary>
    Task<MenuRoleDto?> GetByMenuIdAsync(string menuId);
}
