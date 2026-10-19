using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>메뉴를 기준으로 권한 현황을 거꾸로 읽는 서비스 — 이 메뉴는 누가 쓸 수 있나. 쓰기는 여기서 하지 않는다.</summary>
public interface IMenuRoleService
{
    /// <summary>메뉴 하나의 권한 현황. 메뉴를 못 찾으면 null 이다.</summary>
    Task<MenuRoleDto?> GetByMenuIdAsync(string menuId);
}
