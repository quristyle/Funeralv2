using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 사용자 프로필 및 상세 정보를 관리하는 서비스 인터페이스
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 특정 사용자의 상세 정보를 조회합니다.
    /// </summary>
    /// <param name="userIdOrKey">사용자 아이디 또는 고유 키</param>
    /// <returns>사용자 정보 DTO, 찾을 수 없는 경우 null</returns>
    Task<UserInfoDto?> GetUserInfoAsync(string userIdOrKey);
}
