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

    /// <summary>
    /// 여러 메뉴의 부모와 순서를 한 번에 반영합니다.
    /// 트리에서 드래그로 자리를 옮기면 형제 여러 개의 순번이 함께 바뀌므로,
    /// 화면이 계산한 결과를 그대로 받아 한 번의 트랜잭션으로 저장합니다.
    /// </summary>
    /// <param name="items">변경할 메뉴의 ID·부모·순번 목록</param>
    Task<bool> ReorderMenusAsync(List<MenuOrderDto> items);

    /// <summary>
    /// 로그인한 사용자가 메뉴별로 실제 가진 권한을 조회합니다.
    /// 여러 역할에 속해 있으면 역할들의 권한을 OR 로 합칩니다.
    /// </summary>
    /// <param name="userId">사용자(계정) 아이디</param>
    Task<List<MenuPermissionDto>> GetMenuPermissionsAsync(string userId);
}
