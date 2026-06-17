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
}
