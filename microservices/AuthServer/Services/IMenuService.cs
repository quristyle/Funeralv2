using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 프론트엔드 내비게이션 메뉴 제공을 위한 서비스 인터페이스
/// </summary>
public interface IMenuService
{
    /// <summary>
    /// 특정 사용자의 권한에 맞는 모든 메뉴 목록을 조회합니다.
    /// </summary>
    /// <param name="userId">사용자 아이디</param>
    /// <returns>메뉴 DTO 리스트</returns>
    Task<List<MenuDto>> GetAllMenusAsync(string userId);
    /// <summary>
    /// 메뉴의 위치(부모)와 순서를 변경합니다.
    /// </summary>
    /// <param name="menuId">변경할 메뉴 ID</param>
    /// <param name="newParentId">새 부모 메뉴 ID (최상위는 null)</param>
    /// <param name="newOrderNo">새 순서 번호</param>
    Task<bool> MoveMenuAsync(string menuId, string? newParentId, int newOrderNo);
}
